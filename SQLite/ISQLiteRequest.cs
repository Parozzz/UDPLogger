using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UDPLogger.SQLite.SQLiteHandler;

namespace UDPLogger.SQLite
{
    public interface ISQLiteRequest { }

    public class SQLiteInsertRequest(DatabaseRecord record) : ISQLiteRequest
    {
        public DatabaseRecord Record { get; init; } = record;
    }

    public class SQLitePurgeRequest(DateTime time) : ISQLiteRequest
    {
        public DateTime Time { get; init; } = time;
    }

    public class SQLiteVacuumRequest() : ISQLiteRequest { }
}
