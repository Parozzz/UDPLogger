using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPLogger
{
    public static class Utils
    {
        public static long Now()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

    }
}
