using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Linq;
using Windows.Gaming.Preview.GamesEnumeration;
using static UDPLogger.UDP.UDPSocketHandler;

namespace UDPLogger.SQLite
{
    public class SQLiteHandler()
    {
        //SELECT * FROM T_LOG ORDER BY C_TIME DESC LIMIT 1
        //SELECT DISTINCT C_NAME FROM T_LOG ORDER BY ID DESC
        public record DatabaseQueryResult(List<DatabaseRecord> RecordList);


        public delegate void QueryResultEventHandler(object? sender, QueryResultEventArgs args);
        public record QueryResultEventArgs(List<DatabaseRecord> RecordList);


        public record DatabaseRecord(string Name, int Identifier, string StringValue, DateTime Time);

        public event QueryResultEventHandler QueryResult = delegate { };

        private readonly ConcurrentQueue<ISQLiteRequest> requestQueue = [];

        private SqliteConnection? connection;
        private volatile bool queryLastValues;

        public void AddRecord(DatabaseRecord record)
        {
            requestQueue.Enqueue(new SQLiteInsertRequest(record));
        }

        public void PurgeBeforeOf(DateTime time)
        {
            requestQueue.Enqueue(new SQLitePurgeRequest(time));
        }

        public void Vacuum()
        {
            requestQueue.Enqueue(new SQLiteVacuumRequest());
        }

        public void QueryLastValues()
        {
            queryLastValues = true;
        }

        public void StartWorker(string databasePath)
        {
            var connectionString = "Data Source=" + databasePath;

            BackgroundWorker sqliteWorker = new() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            sqliteWorker.DoWork += (sender, args) =>
            {
                bool tableCreated = false;
                while (!sqliteWorker.CancellationPending)
                {
                    try
                    {
                        if (connection == null)
                        {
                            connection = new SqliteConnection(connectionString);
                            connection.Open();
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerTXT.AddException("SQLite Connection Exception", ex);
                        CloseConnection();

                        Thread.Sleep(100);
                    }

                    try
                    {
                        if (connection != null)
                        {
                            if (!tableCreated)
                            {
                                var command = connection.CreateCommand();
                                command.CommandText = SQLiteTableData.CREATE_TABLE;
                                command.ExecuteNonQuery();

                                tableCreated = true;
                            }
                            else if (queryLastValues)
                            {
                                var nameList = SelectAllDistinctNames(connection);
                                var recordList = SelectLastValuesForNames(connection, nameList);

                                sqliteWorker.ReportProgress(0, new DatabaseQueryResult(recordList));

                                queryLastValues = false;
                            }
                            else if (!requestQueue.IsEmpty && requestQueue.TryDequeue(out ISQLiteRequest? request))
                            {

                                if(request is SQLiteInsertRequest insertRequest)
                                {
                                    var record = insertRequest.Record;

                                    var command = connection.CreateCommand();
                                    command.CommandText = SQLiteTableData.INSERT_VALUE;
                                    command.Parameters.AddWithValue("$v1", record.Name);
                                    command.Parameters.AddWithValue("$v2", record.Identifier);
                                    command.Parameters.AddWithValue("$v3", record.StringValue);
                                    command.Parameters.AddWithValue("$v4", record.Time);
                                    command.ExecuteNonQuery();
                                }
                                else if(request is SQLitePurgeRequest purgeRequest)
                                {
                                    var command = connection.CreateCommand();
                                    command.CommandText = SQLiteTableData.PURGE_OLD_VALUES;
                                    command.Parameters.AddWithValue("$v1", purgeRequest.Time);
                                    command.ExecuteNonQuery();
                                }
                                else if(request is SQLiteVacuumRequest vacuumRequest)
                                {
                                    var command = connection.CreateCommand();
                                    command.CommandText = SQLiteTableData.VACUUM_DATABASE;
                                    command.ExecuteNonQuery();
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerTXT.AddException("SQLite Exception", ex);
                        CloseConnection();

                        Thread.Sleep(100);
                    }

                    Thread.Sleep(2);
                }
            };

            sqliteWorker.ProgressChanged += (sender, args) =>
            {
                if (args.UserState is DatabaseQueryResult fetchResult)
                {
                    QueryResult(this, new(fetchResult.RecordList));
                }
            };

            sqliteWorker.RunWorkerAsync();
        }

        private void CloseConnection()
        {
            connection?.Close();
            connection = null;
        }

        private static List<string> SelectAllDistinctNames(SqliteConnection connection)
        {
            List<string> nameList = [];

            var command = connection.CreateCommand();
            command.CommandText = SQLiteTableData.SELECT_DISTINC_NAMES;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                nameList.Add(reader.GetString(0));
            }

            return nameList;
        }

        private static List<DatabaseRecord> SelectLastValuesForNames(SqliteConnection connection, List<string> nameList)
        {
            List<DatabaseRecord> recordList = [];
            foreach (var name in nameList)
            {
                var command = connection.CreateCommand();
                command.CommandText = SQLiteTableData.SELECT_LAST_VALUES;
                command.Parameters.AddWithValue("$v1", name);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    if (reader.IsDBNull(2) || reader.IsDBNull(3) || reader.IsDBNull(4))
                    {
                        continue;
                    }

                    var identifier = reader.GetInt16(2);
                    var value = reader.GetString(3);
                    var dateTime = reader.GetDateTime(4);
                    recordList.Add(new(name, identifier, value, dateTime));
                }
            }
            return recordList;
        }

    }
}
