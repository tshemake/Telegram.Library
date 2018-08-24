using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BigMath.Utils;

namespace Telegram.Net.Core.MTProto
{
    public class Serializers
    {
        public static class Bytes
        {
            public static byte[] Read(BinaryReader binaryReader)
            {
                byte firstByte = binaryReader.ReadByte();
                int len, padding;
                if (firstByte == 254)
                {
                    len = binaryReader.ReadByte() | (binaryReader.ReadByte() << 8) | (binaryReader.ReadByte() << 16);
                    padding = len % 4;
                }
                else {
                    len = firstByte;
                    padding = (len + 1) % 4;
                }

                byte[] data = binaryReader.ReadBytes(len);
                if (padding > 0)
                {
                    padding = 4 - padding;
                    binaryReader.ReadBytes(padding);
                }

                return data;
            }

            public static BinaryWriter Write(BinaryWriter binaryWriter, byte[] buffer, int offset, int count)
            {
                int padding;
                if (count < 254)
                {
                    padding = (count + 1) % 4;
                    if (padding != 0)
                    {
                        padding = 4 - padding;
                    }

                    binaryWriter.Write((byte)count);
                    binaryWriter.Write(buffer, offset, count);
                }
                else
                {
                    padding = count % 4;
                    if (padding != 0)
                    {
                        padding = 4 - padding;
                    }

                    binaryWriter.Write((byte)254);
                    binaryWriter.Write((byte)count);
                    binaryWriter.Write((byte)(count >> 8));
                    binaryWriter.Write((byte)(count >> 16));
                    binaryWriter.Write(buffer, offset, count);
                }


                for (int i = 0; i < padding; i++)
                {
                    binaryWriter.Write((byte)0);
                }

                return binaryWriter;
            }

            public static BinaryWriter Write(BinaryWriter binaryWriter, byte[] data)
            {
                return Write(binaryWriter, data, 0, data.Length);
            }
        }

        public static class String
        {
            public static string Read(BinaryReader reader)
            {
                byte[] data = Bytes.Read(reader);
                return Encoding.UTF8.GetString(data, 0, data.Length);
            }

            public static BinaryWriter Write(BinaryWriter writer, string str)
            {
                return Bytes.Write(writer, Encoding.UTF8.GetBytes(str));
            }
        }

        public static class Int32
        {
            public static int Read(BinaryReader reader)
            {
                return reader.ReadBytes(4).ToInt32(0, true);
            }

            public static BinaryWriter Write(BinaryWriter writer, int src)
            {
                writer.Write(src.ToBytes(true));
                return writer;
            }
        }

        public class UInt32
        {
            public static uint Read(BinaryReader reader)
            {
                return reader.ReadBytes(4).ToUInt32(0, true);
            }
            public static BinaryWriter Write(BinaryWriter writer, uint src)
            {
                writer.Write(src.ToBytes(true));
                return writer;
            }
        }

        public static class Int64
        {
            public static long Read(BinaryReader reader)
            {
                return reader.ReadBytes(8).ToInt64(0, true);
            }

            public static BinaryWriter Write(BinaryWriter writer, long src)
            {
                writer.Write(src.ToBytes(true));
                return writer;
            }
        }

        public static class Int128
        {
            public static BigMath.Int128 Read(BinaryReader reader)
            {
                return reader.ReadBytes(16).ToInt128(0, true);
            }

            public static BinaryWriter Write(BinaryWriter writer, BigMath.Int128 src)
            {
                writer.Write(src.ToBytes(true));
                return writer;
            }
        }

        public static class Int256
        {
            public static BigMath.Int256 Read(BinaryReader reader)
            {
                return reader.ReadBytes(16).ToInt256(0, true);
            }

            public static BinaryWriter Write(BinaryWriter writer, BigMath.Int256 src)
            {
                writer.Write(src.ToBytes(true));
                return writer;
            }
        }

        public static class Double
        {
            public static double Read(BinaryReader reader)
            {
                return reader.ReadDouble();
            }

            public static BinaryWriter Write(BinaryWriter writer, double src)
            {
                writer.Write(src);
                return writer;
            }
        }

        public static class Bool
        {
            public const uint TRUE_CONSTRUCTOR_ID = TLBoolTrue.CONSTRUCTOR_ID;
            public const uint FALSE_CONSTRUCTOR_ID = TLBoolFalse.CONSTRUCTOR_ID;

            public static bool Read(BinaryReader reader)
            {
                uint constructorId = reader.ReadUInt32();
                if (constructorId == TLBoolTrue.CONSTRUCTOR_ID)
                    return true;
                else if (constructorId == TLBoolFalse.CONSTRUCTOR_ID)
                    return false;
                else
                    throw new Exception($"Wrong TLBool constructor id. Found 0x{constructorId:X2}"
                        + $", expected: 0x{TLBoolTrue.CONSTRUCTOR_ID:X2}"
                        + $" or 0x{TLBoolFalse.CONSTRUCTOR_ID:X2}");
            }

            public static BinaryWriter Write(BinaryWriter writer, bool value)
            {
                writer.Write(value ? TLBoolTrue.CONSTRUCTOR_ID : TLBoolFalse.CONSTRUCTOR_ID);
                return writer;
            }

            private class TLBoolTrue
            {
                public const uint CONSTRUCTOR_ID = 0x997275b5;

                public override string ToString()
                {
                    return $"boolTrue#{CONSTRUCTOR_ID:x2}";
                }
            }

            private class TLBoolFalse
            {
                public const uint CONSTRUCTOR_ID = 0xbc799737;

                public override string ToString()
                {
                    return $"boolFalse#{CONSTRUCTOR_ID:x2}";
                }
            }
        }

        public static string VectorToString<T>(List<T> list)
        {
            string[] tokens = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                tokens[i] = list[i].ToString();
            }
            return "[" + string.Join(", ", tokens) + "]";
        }
    }
}
