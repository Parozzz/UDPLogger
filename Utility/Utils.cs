using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPLogger.Utility
{
    public static class Utils
    {
        public static long Now()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public static bool GetBit(this byte b, int bitNumber)
        {
            return (b & 1 << bitNumber) != 0;
        }

        /// <summary>
        /// Returns a formatted string representing the size, up to TB, of the file defined in the FileInfo instance (i.e. 10 KB).
        /// </summary>
        /// <param name="info">The FileInfo instance.</param>
        /// <returns></returns>
        public static string FormatBytes(this FileInfo info)
        {
            long bytes = info.Length;
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }
            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }
    }
}
