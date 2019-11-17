using System;
using System.IO;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo

namespace AppDomainAlternative.Serializer.Default
{
    /// <summary>
    /// A header byte that describes next payload of data in the stream.
    /// </summary>
    internal enum ObjectType : byte
    {
        //other common types

        Array,
        Serializable,
        Enum,
        Null,
        Proxy,
        Type,
        Types,

        //common struct types

        DateTime,
        Guid,
        TimeSpan,

        //primitive types & string

        Boolean,
        Byte,
        Bytes,
        Char,
        Decimal,
        Double,
        Float,
        Int,
        Long,
        SByte,
        Short,
        Str,
        UInt,
        ULong,
        UShort
    }

    internal static class ObjectReaderWriter
    {
        public static Task<object> ReadObject(this BinaryReader reader, IResolveProxyIds resolver) => reader.ReadObject((ObjectType)reader.ReadByte(), resolver);
        public static async Task<object> ReadObject(this BinaryReader reader, ObjectType objectType, IResolveProxyIds resolver)
        {
            switch (objectType)
            {
                case ObjectType.Array:
                    return await reader.ReadArray(resolver).ConfigureAwait(false);

                case ObjectType.Serializable:
                    return await reader.ReadSerializable(resolver).ConfigureAwait(false);

                case ObjectType.Enum:
                    return reader.ReadEnum();

                case ObjectType.Null:
                    return null;

                case ObjectType.Proxy:
                    var id = reader.ReadInt64();
                    return resolver.TryToGetInstance(id, out var instance) ? instance : null;

                case ObjectType.Type:
                    return reader.ReadType();

                case ObjectType.DateTime:
                    return reader.ReadDateTime();

                case ObjectType.Guid:
                    return await reader.ReadGuid().ConfigureAwait(false);

                case ObjectType.TimeSpan:
                    return reader.ReadTimeSpan();

                case ObjectType.Byte:
                    return reader.ReadByte();
                case ObjectType.Bytes:
                    var bytes = new byte[reader.ReadInt32()];
                    var byteIndex = 0;
                    while (byteIndex < bytes.Length)
                    {
                        byteIndex += await reader.BaseStream.ReadAsync(bytes, byteIndex, bytes.Length - byteIndex).ConfigureAwait(false);
                    }
                    return bytes;

                case ObjectType.Boolean:
                    return reader.ReadBoolean();

                case ObjectType.Char:
                    return reader.ReadChar();

                case ObjectType.Decimal:
                    return reader.ReadDecimal();

                case ObjectType.Double:
                    return reader.ReadDouble();

                case ObjectType.Float:
                    return reader.ReadSingle();

                case ObjectType.Int:
                    return reader.ReadInt32();

                case ObjectType.Long:
                    return reader.ReadInt64();

                case ObjectType.SByte:
                    return reader.ReadSByte();

                case ObjectType.Short:
                    return reader.ReadInt16();

                case ObjectType.Str:
                    return reader.ReadString();

                case ObjectType.UInt:
                    return reader.ReadUInt32();

                case ObjectType.ULong:
                    return reader.ReadUInt64();

                case ObjectType.UShort:
                    return reader.ReadUInt16();

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void WriteNull(this BinaryWriter writer) => writer.Write((byte)ObjectType.Null);
        public static void Write(this BinaryWriter writer, Type valueType, object value, IResolveProxyIds resolver, bool writeHeader = true)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (typeof(Type).IsAssignableFrom(valueType))
            {
                writer.Write((Type)value);
                return;
            }

            if (valueType.IsArray)
            {
                writer.Write(valueType, (Array)value, resolver, writeHeader);
                return;
            }

            if (valueType.IsEnum)
            {
                writer.WriteEnum(valueType, value, writeHeader);
                return;
            }

            if (valueType.IsClass &&
                !valueType.IsSealed &&
                resolver.TryToGetInstanceId(value, out var id))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Proxy);
                }
                writer.Write(id);
                return;
            }

            if (valueType == typeof(DateTime) || valueType == typeof(DateTime?))
            {
                writer.Write((DateTime)value, writeHeader);
                return;
            }

            if (valueType == typeof(Guid) || valueType == typeof(Guid?))
            {
                writer.Write((Guid)value, writeHeader);
                return;
            }

            if (valueType == typeof(TimeSpan) || valueType == typeof(TimeSpan?))
            {
                writer.Write((TimeSpan)value, writeHeader);
                return;
            }

            if (valueType == typeof(bool) || valueType == typeof(bool?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Boolean);
                }
                writer.Write((bool)value);
                return;
            }

            if (valueType == typeof(byte) || valueType == typeof(byte?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Byte);
                }
                writer.Write((byte)value);
                return;
            }

            if (valueType == typeof(byte[]))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Bytes);
                }
                var valueBytes = (byte[])value;
                writer.Write(valueBytes.Length);
                writer.BaseStream.Write(valueBytes, 0, valueBytes.Length);
                return;
            }

            if (valueType == typeof(char) || valueType == typeof(char?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Char);
                }
                writer.Write((char)value);
                return;
            }

            if (valueType == typeof(decimal) || valueType == typeof(decimal?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Decimal);
                }
                writer.Write((decimal)value);
                return;
            }

            if (valueType == typeof(double) || valueType == typeof(double?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Double);
                }
                writer.Write((double)value);
                return;
            }

            if (valueType == typeof(float) || valueType == typeof(float?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Float);
                }
                writer.Write((float)value);
                return;
            }

            if (valueType == typeof(int) || valueType == typeof(int?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Int);
                }
                writer.Write((int)value);
                return;
            }

            if (valueType == typeof(long) || valueType == typeof(long?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Long);
                }
                writer.Write((long)value);
                return;
            }

            if (valueType == typeof(sbyte) || valueType == typeof(sbyte?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.SByte);
                }
                writer.Write((sbyte)value);
                return;
            }

            if (valueType == typeof(short) || valueType == typeof(short?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Short);
                }
                writer.Write((short)value);
                return;
            }

            if (valueType == typeof(string))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.Str);
                }
                writer.Write((string)value);
                return;
            }

            if (valueType == typeof(uint) || valueType == typeof(uint?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.UInt);
                }
                writer.Write((uint)value);
                return;
            }

            if (valueType == typeof(ulong) || valueType == typeof(ulong?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.ULong);
                }
                writer.Write((ulong)value);
                return;
            }

            if (valueType == typeof(ushort) || valueType == typeof(ushort?))
            {
                if (writeHeader)
                {
                    writer.Write((byte)ObjectType.UShort);
                }
                writer.Write((ushort)value);
                return;
            }

            if (valueType.IsSerializable)
            {
                writer.WriteSerializable(resolver, valueType, value, writeHeader);
                return;
            }

            throw new ArgumentException($"Unable serialize: {value.GetType()}");
        }
    }
}
