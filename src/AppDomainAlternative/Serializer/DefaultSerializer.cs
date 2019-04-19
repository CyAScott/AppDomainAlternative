using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace AppDomainAlternative.Serializer
{
    internal class DefaultSerializer : IAmASerializer
    {
        private readonly BinaryFormatter formatter = new BinaryFormatter();
        private readonly Dictionary<Type, ShortTypes> shortTypeMap = new Dictionary<Type, ShortTypes>
        {
            { typeof(Type), ShortTypes.Type },
            { typeof(DateTime), ShortTypes.DateTime },
            { typeof(TimeSpan), ShortTypes.TimeSpan },
            { typeof(bool), ShortTypes.Boolean },
            { typeof(byte), ShortTypes.Byte },
            { typeof(byte[]), ShortTypes.Bytes },
            { typeof(char), ShortTypes.Char },
            { typeof(decimal), ShortTypes.Decimal },
            { typeof(double), ShortTypes.Double },
            { typeof(float), ShortTypes.Float },
            { typeof(int), ShortTypes.Int },
            { typeof(long), ShortTypes.Long },
            { typeof(sbyte), ShortTypes.SByte },
            { typeof(short), ShortTypes.Short },
            { typeof(string), ShortTypes.Str },
            { typeof(uint), ShortTypes.UInt },
            { typeof(ulong), ShortTypes.ULong },
            { typeof(ushort), ShortTypes.UShort }
        };

        internal enum ShortTypes : byte
        {
            Unknown,

            Type,

            DateTime,
            TimeSpan,

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

        internal enum SerializationType : byte
        {
            Binary,
            Enum,
            Null,
            Proxy,
            Type,

            DateTime,
            TimeSpan,

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

        public Task Serialize(BinaryWriter writer, Type valueType, object value, IResolveProxyIds resolver)
        {
            if (value == null)
            {
                writer.Write((byte)SerializationType.Null);
                return Task.CompletedTask;
            }

            if (valueType.IsEnum)
            {
                writer.Write((byte)SerializationType.Enum);
                valueType = Enum.GetUnderlyingType(valueType);
                return Serialize(writer, valueType, Convert.ChangeType(value, valueType), resolver);
            }

            switch (value)
            {
                case Type valueAsType:
                    writer.Write((byte)SerializationType.Type);
                    if (shortTypeMap.TryGetValue(valueAsType, out var shortType))
                    {
                        writer.Write((byte)shortType);
                    }
                    else
                    {
                        writer.Write((byte)ShortTypes.Unknown);
                        writer.Write(valueAsType.AssemblyQualifiedName ?? throw new ArgumentException("Unable to serialize type."));
                    }
                    break;
                case DateTime valueDateTime:
                    writer.Write((byte)SerializationType.DateTime);
                    writer.Write((byte)valueDateTime.Kind);
                    writer.Write(valueDateTime.Ticks);
                    break;
                case TimeSpan valueTimeSpan:
                    writer.Write((byte)SerializationType.TimeSpan);
                    writer.Write(valueTimeSpan.Ticks);
                    break;
                case bool valueBool:
                    writer.Write((byte)SerializationType.Boolean);
                    writer.Write(valueBool);
                    break;
                case byte valueByte:
                    writer.Write((byte)SerializationType.Byte);
                    writer.Write(valueByte);
                    break;
                case byte[] valueBytes:
                    writer.Write((byte)SerializationType.Bytes);
                    writer.Write(valueBytes.Length);
                    writer.BaseStream.Write(valueBytes, 0, valueBytes.Length);
                    break;
                case char valueChar:
                    writer.Write((byte)SerializationType.Char);
                    writer.Write(valueChar);
                    break;
                case decimal valueDecimal:
                    writer.Write((byte)SerializationType.Decimal);
                    writer.Write(valueDecimal);
                    break;
                case double valueDouble:
                    writer.Write((byte)SerializationType.Double);
                    writer.Write(valueDouble);
                    break;
                case float valueFloat:
                    writer.Write((byte)SerializationType.Float);
                    writer.Write(valueFloat);
                    break;
                case int valueInt:
                    writer.Write((byte)SerializationType.Int);
                    writer.Write(valueInt);
                    break;
                case long valueLong:
                    writer.Write((byte)SerializationType.Long);
                    writer.Write(valueLong);
                    break;
                case sbyte valueSByte:
                    writer.Write((byte)SerializationType.SByte);
                    writer.Write(valueSByte);
                    break;
                case short valueShort:
                    writer.Write((byte)SerializationType.Short);
                    writer.Write(valueShort);
                    break;
                case string valueStr:
                    writer.Write((byte)SerializationType.Str);
                    writer.Write(valueStr);
                    break;
                case uint valueUInt:
                    writer.Write((byte)SerializationType.UInt);
                    writer.Write(valueUInt);
                    break;
                case ulong valueULong:
                    writer.Write((byte)SerializationType.ULong);
                    writer.Write(valueULong);
                    break;
                case ushort valueUShort:
                    writer.Write((byte)SerializationType.UShort);
                    writer.Write(valueUShort);
                    break;
                default:
                    if (resolver.TryToGetInstanceId(value, out var id))
                    {
                        writer.Write((byte)SerializationType.Proxy);
                        writer.Write(id);
                    }
                    else if (value.GetType().IsSerializable)
                    {
                        writer.Write((byte)SerializationType.Binary);
                        formatter.Serialize(writer.BaseStream, value);
                    }
                    else
                    {
                        throw new ArgumentException($"Unable serialize: {value.GetType()}");
                    }
                    break;
            }

            return Task.CompletedTask;
        }

        private async Task<object> deserialize(BinaryReader reader, Type valueType, SerializationType type, IResolveProxyIds resolver)
        {
            switch (type)
            {
                case SerializationType.Binary:
                    return formatter.Deserialize(reader.BaseStream);

                case SerializationType.Enum:
                    return Enum.ToObject(valueType, await deserialize(reader, valueType, (SerializationType)reader.ReadByte(), resolver).ConfigureAwait(false));

                case SerializationType.Null:
                    return null;

                case SerializationType.Proxy:
                    var id = reader.ReadInt64();
                    if (resolver.TryToGetInstance(id, out var instance))
                    {
                        return instance;
                    }
                    throw new InvalidProgramException($"Unable to find instance with id: {id}");

                case SerializationType.Type:
                    var shortType = (ShortTypes)reader.ReadByte();
                    return shortType == ShortTypes.Unknown ? Type.GetType(reader.ReadString(), true) : shortTypeMap.First(item => item.Value == shortType).Key;

                case SerializationType.DateTime:
                    var kind = (DateTimeKind)reader.ReadByte();
                    var ticks = reader.ReadInt64();
                    return new DateTime(ticks, kind);
                case SerializationType.TimeSpan:
                    return new TimeSpan(reader.ReadInt64());

                case SerializationType.Byte:
                    return reader.ReadByte();
                case SerializationType.Bytes:
                    var bytes = new byte[reader.ReadInt32()];
                    var index = 0;
                    while (index < bytes.Length)
                    {
                        index += await reader.BaseStream.ReadAsync(bytes, index, bytes.Length - index).ConfigureAwait(false);
                    }
                    return bytes;
                case SerializationType.Boolean:
                    return reader.ReadBoolean();
                case SerializationType.Char:
                    return reader.ReadChar();
                case SerializationType.Decimal:
                    return reader.ReadDecimal();
                case SerializationType.Double:
                    return reader.ReadDouble();
                case SerializationType.Float:
                    return reader.ReadSingle();
                case SerializationType.Int:
                    return reader.ReadInt32();
                case SerializationType.Long:
                    return reader.ReadInt64();
                case SerializationType.SByte:
                    return reader.ReadSByte();
                case SerializationType.Short:
                    return reader.ReadInt16();
                case SerializationType.Str:
                    return reader.ReadString();
                case SerializationType.UInt:
                    return reader.ReadUInt32();
                case SerializationType.ULong:
                    return reader.ReadUInt64();
                case SerializationType.UShort:
                    return reader.ReadUInt16();

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        public Task<object> Deserialize(BinaryReader reader, Type valueType, IResolveProxyIds resolver, CancellationToken token) =>
            deserialize(reader, valueType, (SerializationType)reader.ReadByte(), resolver);

        public bool CanSerialize(Type type) =>
            type.IsEnum ||
            type.IsPrimitive ||
            type == typeof(string) ||
            type.IsSerializable ||
            Attribute.IsDefined(type, typeof(PassByProxyAttribute));

        public static IAmASerializer Resolve(string name) => Instance;

        public string Name { get; } = $"DefaultSerializer@{typeof(DefaultSerializer).Assembly.GetName().Version}";

        public static readonly IAmASerializer Instance = new DefaultSerializer();
    }
}
