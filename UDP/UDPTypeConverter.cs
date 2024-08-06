using System;
using System.Buffers.Binary;

namespace UDPLogger.UDP
{
    public static class UDPTypeConverter
    {
        //This values are choosen so the do not interfere with chars and do not interfere with key bytes for comm
        public const byte TYPE_IDENTIFIER_BOOL = 0xA0;
        public const byte TYPE_IDENTIFIER_UINT = 0xA1;
        public const byte TYPE_IDENTIFIER_INT = 0xA2;
        public const byte TYPE_IDENTIFIER_REAL = 0xA3;
        public const byte TYPE_IDENTIFIER_STRING = 0xA4;

        public static bool? ConvertBool(ReadOnlySpan<byte> dataBuffer)
        {
            return dataBuffer[0] == 1;
        }

        public static ulong? ConvertUInt(ReadOnlySpan<byte> dataBuffer)
        {
            return dataBuffer.Length switch
            {
                1 => dataBuffer[0],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(dataBuffer),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(dataBuffer),
                8 => BinaryPrimitives.ReadUInt64LittleEndian(dataBuffer),
                _ => null
            };
        }

        public static long? ConvertInt(ReadOnlySpan<byte> dataBuffer)
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

        public static double? ConvertReal(ReadOnlySpan<byte> dataBuffer)
        {
            return dataBuffer.Length switch
            {
                4 => BinaryPrimitives.ReadSingleLittleEndian(dataBuffer),
                8 => BinaryPrimitives.ReadDoubleLittleEndian(dataBuffer),
                _ => null
            };
        }

        public static string? ConvertString(ReadOnlySpan<byte> dataBuffer)
        {
            var str = "";
            for (int x = 0; x < dataBuffer.Length; x++)
            {
                str += (char)dataBuffer[x];
            }
            return str;
        }

        public static object? Convert(byte identifier, ReadOnlySpan<byte> dataBuffer)
        {
            return identifier switch
            {
                TYPE_IDENTIFIER_BOOL => ConvertBool(dataBuffer),
                TYPE_IDENTIFIER_UINT => ConvertUInt(dataBuffer),
                TYPE_IDENTIFIER_INT => ConvertInt(dataBuffer),
                TYPE_IDENTIFIER_REAL => ConvertReal(dataBuffer),
                TYPE_IDENTIFIER_STRING => ConvertString(dataBuffer),
                _ => null
            };
        }

        public static object? ConvertFromString(byte identifier, string stringValue)
        {
            return identifier switch
            {
                TYPE_IDENTIFIER_BOOL => bool.Parse(stringValue),
                TYPE_IDENTIFIER_UINT => ulong.Parse(stringValue),
                TYPE_IDENTIFIER_INT => long.Parse(stringValue),
                TYPE_IDENTIFIER_REAL => double.Parse(stringValue),
                TYPE_IDENTIFIER_STRING => stringValue,
                _ => null
            };
        }
    }
}
