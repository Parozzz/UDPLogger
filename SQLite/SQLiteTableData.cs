using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPLogger.SQLite
{
    public static class SQLiteTableData
    {

        public const string TABLE_NAME = "T_LOG";
        public const string COLUMN_NAME = "C_NAME";
        public const string COLUMN_IDENTIFIER = "C_IDENTIFIER";
        public const string COLUMN_VALUE = "C_VALUE";
        public const string COLUMN_TIME = "C_TIME";

        public static string CREATE_TABLE { get => $"CREATE TABLE IF NOT EXISTS {TABLE_NAME} (ID INTEGER PRIMARY KEY, {COLUMN_NAME} NVARCHAR(20), {COLUMN_IDENTIFIER} INTEGER, {COLUMN_VALUE} NVARCHAR(255), {COLUMN_TIME} DATETIME)"; }
        public static string INSERT_VALUE { get => $"INSERT INTO {TABLE_NAME} ({COLUMN_NAME}, {COLUMN_IDENTIFIER}, {COLUMN_VALUE}, {COLUMN_TIME}) VALUES ($v1, $v2, $v3, $v4)"; }
        public static string SELECT_DISTINC_NAMES { get => $"SELECT DISTINCT {COLUMN_NAME} FROM {TABLE_NAME} ORDER BY ID DESC"; }
        public static string SELECT_LAST_VALUES { get => $"SELECT * FROM {TABLE_NAME} WHERE {COLUMN_NAME} = $v1 ORDER BY ID DESC LIMIT 1"; }
        public static string PURGE_OLD_VALUES { get => $"DELETE FROM {TABLE_NAME} WHERE C_TIME < $v1"; }
        public static string VACUUM_DATABASE { get => $"VACUUM"; }
    }
}
