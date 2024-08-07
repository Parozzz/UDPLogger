using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UDPLogger
{
    public class LoggerTXT
    {
        public enum LoggerLevelEnum
        {
            DEBUG,
            WARN,
            ERROR
        }

        private record LoggerEntry(string prefix, string message);

        private static readonly Lazy<LoggerTXT> lazy = new(() => new LoggerTXT(), isThreadSafe: true);
        public static LoggerTXT INSTANCE { get => lazy.Value; }

        public static void AddDebug(string message)
        {
            INSTANCE.AddMessage(message, LoggerLevelEnum.DEBUG);
        }

        public static void AddWarn(string warning)
        {
            INSTANCE.AddMessage(warning, LoggerLevelEnum.WARN);
        }

        public static void AddError(string error)
        {
            INSTANCE.AddMessage(error, LoggerLevelEnum.ERROR);
        }

        public static void AddException(string caption, Exception exception)
        {
            var msg = caption + Environment.NewLine + exception.ToString() + Environment.NewLine + new string('#', 40);
            INSTANCE.AddMessage(msg, LoggerLevelEnum.ERROR);
        }

        private readonly ConcurrentBag<LoggerEntry> loggerEntryBag;
        public LoggerTXT()
        {
            loggerEntryBag = [];

            StartThread();
        }

        private void StartThread()
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (!loggerEntryBag.TryTake(out LoggerEntry? entry) || entry == null)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        var path = Directory.GetCurrentDirectory() + "\\log.txt";

                        var str = $"{entry.prefix} {entry.message}{Environment.NewLine}";
                        File.AppendAllText(path, str);
#if DEBUG
                        Debug.WriteLine(entry.message);
#endif
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LoggerTXT Exception{Environment.NewLine}{ex}");
                    }
                }
            }).Start();
        }

        public void AddMessage(string message, LoggerLevelEnum levelEnum)
        {
            var prefix = CreatePrefixText(levelEnum);
            loggerEntryBag.Add(new(prefix, message));
        }

        private static string CreatePrefixText(LoggerLevelEnum levelEnum)
        {
            var now = DateTime.Now;

            var levelEnumStr = Enum.GetName(typeof(LoggerLevelEnum), levelEnum);
            var shortMonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(now.Month);
            return $"[{levelEnumStr},{now.Day} {shortMonthName} {now.ToLongTimeString()}]";
        }

    }
}
