using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPLogger.UDP
{
    public record UDPReceivedData(string Name, UDPReceivedFlags Flags, int DataIdentifier, object Data);

    public record UDPReceivedFlags(bool ForceInsert);
}
