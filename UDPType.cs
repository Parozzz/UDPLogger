using System;
using System.Buffers.Binary;

namespace UDPLogger
{
    public abstract class UDPType
    {
        //This values are choosen so the do not interfere with chars and do not interfere with key bytes for comm
        public const byte TYPE_IDENTIFIER_BOOL = 0xA0;
        public const byte TYPE_IDENTIFIER_UINT = 0xA1;
        public const byte TYPE_IDENTIFIER_INT = 0xA2;
        public const byte TYPE_IDENTIFIER_REAL = 0xA3;
        public const byte TYPE_IDENTIFIER_STRING = 0xA4;

        public static readonly UDPType TYPE_BOOL = new UDPTypeBool();
        public static readonly UDPType TYPE_UINT = new UDPTypeUInt();
        public static readonly UDPType TYPE_INT = new UDPTypeInt();
        public static readonly UDPType TYPE_REAL = new UDPTypeReal();
        public static readonly UDPType TYPE_STRING = new UDPTypeString();

        public static UDPType? GetByIdentifier(byte identifier)
        {
            return identifier switch
            {
                TYPE_IDENTIFIER_BOOL => TYPE_BOOL,
                TYPE_IDENTIFIER_UINT => TYPE_UINT,
                TYPE_IDENTIFIER_INT => TYPE_INT,
                TYPE_IDENTIFIER_REAL => TYPE_REAL,
                TYPE_IDENTIFIER_STRING => TYPE_STRING,
                _ => null
            };
        }

        protected UDPType() { }

        public abstract object? Convert(ReadOnlySpan<byte> dataBuffer);
    }

    public class UDPTypeBool : UDPType
    {
        public override object? Convert(ReadOnlySpan<byte> dataBuffer)
        {
            return (dataBuffer[0] == 1);
        }
    }

    public class UDPTypeUInt : UDPType
    {
        public override object? Convert(ReadOnlySpan<byte> dataBuffer)
        {
            return dataBuffer.Length switch
            {
                1 => (byte)dataBuffer[0],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(dataBuffer),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(dataBuffer),
                8 => BinaryPrimitives.ReadUInt64LittleEndian(dataBuffer),
                _ => null
            };
        }
    }

    public class UDPTypeInt : UDPType
    {
        public override object? Convert(ReadOnlySpan<byte> dataBuffer)
        {
            return dataBuffer.Length switch
            {
                1 => (sbyte)dataBuffer[0],
                2 => BinaryPrimitives.ReadInt16LittleEndian(dataBuffer),
                4 => BinaryPrimitives.ReadInt32LittleEndian(dataBuffer),
                8 => BinaryPrimitives.ReadInt64LittleEndian(dataBuffer),
                _ => null
            };
        }
    }

    public class UDPTypeReal : UDPType
    {
        public override object? Convert(ReadOnlySpan<byte> dataBuffer)
        {
            return dataBuffer.Length switch
            {
                4 => BinaryPrimitives.ReadSingleLittleEndian(dataBuffer),
                8 => BinaryPrimitives.ReadDoubleLittleEndian(dataBuffer),
                _ => null
            };
        }
    }

    public class UDPTypeString : UDPType
    {
        public override object? Convert(ReadOnlySpan<byte> dataBuffer)
        {
            var str = "";
            for(int x = 0; x < dataBuffer.Length; x++)
            {
                str += (char)dataBuffer[x];
            }
            return str;
        }
    }
}
