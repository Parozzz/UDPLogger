using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Linq;
using Windows.Gaming.Preview.GamesEnumeration;
using static UDPLogger.UDPSocketHandler;

namespace UDPLogger
{
    public class SQLiteHandler
    {
        //SELECT * FROM T_LOG ORDER BY C_TIME DESC LIMIT 1
        //SELECT DISTINCT C_NAME FROM T_LOG ORDER BY ID DESC
        public record DatabaseFetchResult(List<DatabaseRecord> RecordList);


        public delegate void FetchResultEventHandler(object? sender, FetchResultEventArgs args);
        public record FetchResultEventArgs(List<DatabaseRecord> RecordList);


        public record DatabaseRecord(string Name, int Identifier, string StringValue, DateTime Time);

        private const string TABLE_NAME = "T_LOG";
        private const string COLUMN_NAME = "C_NAME";
        private const string COLUMN_IDENTIFIER = "C_IDENTIFIER";
        private const string COLUMN_VALUE = "C_VALUE";
        private const string COLUMN_TIME = "C_TIME";

        public event FetchResultEventHandler FetchResult = delegate { };

        private readonly ConfigurationFile configurationFile;
        private readonly ConcurrentQueue<DatabaseRecord> recordQueue;

        private volatile bool fetchLastValues;

        public SQLiteHandler(ConfigurationFile configurationFile)
        {
            this.configurationFile = configurationFile;
            this.recordQueue = [];

            Init();
        }

        public void AddRecord(DatabaseRecord record)
        {
            recordQueue.Enqueue(record);
        }

        public void FetchLastValues()
        {
            fetchLastValues = true;
        }

        private void Init()
        {
            
        }

        public void StartWorker()
        {
            var connectionString = "Data Source=" + configurationFile.GetFullDatabasePath();

            BackgroundWorker sqliteWorker = new() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            sqliteWorker.DoWork += (sender, args) =>
            {
                bool tableCreated = false;

                SqliteConnection? connection = null;
                while (!sqliteWorker.CancellationPending)
                {
                    try
                    {
                        if(connection == null)
                        {
                            connection = new SqliteConnection(connectionString);
                            connection.Open();
                        }

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("SQLite Connection Exception.\n" + ex.ToString());
                    }

                    try
                    {
                        if (connection != null)
                        {
                            if (!tableCreated)
                            {
                                string createTableCommandText = "CREATE TABLE IF NOT EXISTS $n (ID INTEGER PRIMARY KEY, $c1 NVARCHAR(20), $c2 INTEGER, $c3 NVARCHAR(255), $c4 DATETIME)";
                                createTableCommandText = createTableCommandText.Replace("$n", TABLE_NAME);
                                createTableCommandText = createTableCommandText.Replace("$c1", COLUMN_NAME);
                                createTableCommandText = createTableCommandText.Replace("$c2", COLUMN_IDENTIFIER);
                                createTableCommandText = createTableCommandText.Replace("$c3", COLUMN_VALUE);
                                createTableCommandText = createTableCommandText.Replace("$c4", COLUMN_TIME);

                                var command = connection.CreateCommand();
                                command.CommandText = createTableCommandText;
                                command.ExecuteNonQuery();

                                tableCreated = true;
                            }
                            else if (fetchLastValues)
                            {
                                var nameList = FetchNames(connection);
                                var recordList = FetchLastRecords(connection, nameList);

                                sqliteWorker.ReportProgress(0, new DatabaseFetchResult(recordList));

                                fetchLastValues = false;
                            }
                            else if(!recordQueue.IsEmpty && recordQueue.TryDequeue(out DatabaseRecord? record) && record != null)
                            {
                                if(record.StringValue != null)
                                {
                                    string insertCommandText = "INSERT INTO $n ($c1, $c2, $c3, $c4) VALUES ($v1, $v2, $v3, $v4)";
                                    insertCommandText = insertCommandText.Replace("$n", TABLE_NAME);
                                    insertCommandText = insertCommandText.Replace("$c1", COLUMN_NAME);
                                    insertCommandText = insertCommandText.Replace("$c2", COLUMN_IDENTIFIER);
                                    insertCommandText = insertCommandText.Replace("$c3", COLUMN_VALUE);
                                    insertCommandText = insertCommandText.Replace("$c4", COLUMN_TIME);

                                    var command = connection.CreateCommand();
                                    command.CommandText = insertCommandText;
                                    command.Parameters.AddWithValue("$v1", record.Name);
                                    command.Parameters.AddWithValue("$v2", record.Identifier);
                                    command.Parameters.AddWithValue("$v3", record.StringValue);
                                    command.Parameters.AddWithValue("$v4", record.Time);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("SQLite Exception.\n" + ex.ToString());

                        connection?.Close();
                        connection = null;
                    }

                    Thread.Sleep(2);
                }
            };

            sqliteWorker.ProgressChanged += (sender, args) =>
            {
                if(args.UserState is DatabaseFetchResult fetchResult)
                {
                    FetchResult(this, new(fetchResult.RecordList));
                }
            };

            sqliteWorker.RunWorkerAsync();
        }

        private List<string> FetchNames(SqliteConnection connection)
        {
            List<string> nameList = [];

            string commandText = "SELECT DISTINCT $c1 FROM $n ORDER BY ID DESC";
            commandText = commandText.Replace("$n", TABLE_NAME);
            commandText = commandText.Replace("$c1", COLUMN_NAME);

            var command = connection.CreateCommand();
            command.CommandText = commandText;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                nameList.Add(reader.GetString(0));
            }

            return nameList;

        }

        private List<DatabaseRecord> FetchLastRecords(SqliteConnection connection, List<string> nameList)
        {
            List<DatabaseRecord> recordList = [];
            foreach (var name in nameList)
            {
                string commandText = "SELECT * FROM $n WHERE $c1 = $v1 ORDER BY ID DESC LIMIT 1";
                commandText = commandText.Replace("$n", TABLE_NAME);
                commandText = commandText.Replace("$c1", COLUMN_NAME);

                var command = connection.CreateCommand();
                command.CommandText = commandText;
                command.Parameters.AddWithValue("$v1", name);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var identifier = reader.IsDBNull(2) ? -1 : reader.GetInt16(2);
                    var value = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    var dateTime = reader.IsDBNull(4) ? DateTime.Now : reader.GetDateTime(4);
                    recordList.Add(new(name, identifier, value, dateTime));
                }
            }
            return recordList;
        }

        /*
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
                SELECT name
                FROM user
                WHERE id = $id
            ";
        command.Parameters.AddWithValue("$id", id);

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);

                Console.WriteLine($"Hello, {name}!");
            }
        }*/
    }
}
