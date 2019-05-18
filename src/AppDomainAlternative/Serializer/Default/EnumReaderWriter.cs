using System;
using System.IO;

namespace AppDomainAlternative.Serializer.Default
{
    internal static class EnumReaderWriter
    {
        public static object ReadEnum(this BinaryReader reader, Type type = null)
        {
            var enumType = type ?? reader.ReadType();

            var integralType = Enum.GetUnderlyingType(enumType);
            object integralValue;

            if (integralType == typeof(int))
            {
                integralValue = reader.ReadInt32();
            }
            else if (integralType == typeof(byte))
            {
                integralValue = reader.ReadByte();
            }
            else if (integralType == typeof(uint))
            {
                integralValue = reader.ReadUInt32();
            }
            else if (integralType == typeof(long))
            {
                integralValue = reader.ReadInt64();
            }
            else if (integralType == typeof(ulong))
            {
                integralValue = reader.ReadUInt64();
            }
            else if (integralType == typeof(sbyte))
            {
                integralValue = reader.ReadSByte();
            }
            else if (integralType == typeof(short))
            {
                integralValue = reader.ReadInt16();
            }
            else if (integralType == typeof(ushort))
            {
                integralValue = reader.ReadUInt16();
            }
            else
            {
                //this should not be possible based on the limitations of the enum type
                throw new InvalidOperationException("Unable to serialize the enum value.");
            }

            return Enum.ToObject(enumType, integralValue);
        }

        public static void WriteEnum(this BinaryWriter writer, Type type, object value, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.Enum);
                writer.Write(type, false);
            }

            //this gets the .Net integral primitive type behind the enum (ie byte, int, etc.)
            var integralType = Enum.GetUnderlyingType(type);

            if (integralType == typeof(int))
            {
                writer.Write((int)value);
            }
            else if (integralType == typeof(byte))
            {
                writer.Write((byte)value);
            }
            else if (integralType == typeof(uint))
            {
                writer.Write((uint)value);
            }
            else if (integralType == typeof(long))
            {
                writer.Write((long)value);
            }
            else if (integralType == typeof(ulong))
            {
                writer.Write((ulong)value);
            }
            else if (integralType == typeof(sbyte))
            {
                writer.Write((sbyte)value);
            }
            else if (integralType == typeof(short))
            {
                writer.Write((short)value);
            }
            else if (integralType == typeof(ushort))
            {
                writer.Write((ushort)value);
            }
            else
            {
                //this should not be possible based on the limitations of the enum type
                throw new InvalidOperationException("Unable to serialize the enum value.");
            }
        }
    }
}
