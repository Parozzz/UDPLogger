using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPLogger.UDP
{
    public static class UDPConstants
    {
        public const byte STX = 0x02; //START
        public const byte ETX = 0x03; //END
        public const byte DNE = 0x04; //DATA NAME ESCAPE
        public const byte DVE = 0x05; //DATA VALUE ESCAPE

        public const byte CMD_STOP = 1;
        public const byte CMD_CONN = 2;
        public const byte CMD_PING = 3;

        public const int PING_TIMEOUT = 1000; //ms
        public const int PACKET_TIMEOUT = 2500; //ms
    }
}
