using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable IdentifierTypo

namespace AppDomainAlternative.Serializer.Default
{
    /// <summary>
    /// When serializing an <see cref="Type"/> this enum value can be used as short hand for common .Net types.
    /// </summary>
    internal enum ShortTypes : byte
    {
        Unknown,

        Type,
        Types,

        DateTime,
        NullableDateTime,
        DateTimes,

        Guid,
        NullableGuid,
        Guids,

        TimeSpan,
        NullableTimeSpan,
        TimeSpans,

        Boolean,
        NullableBoolean,
        Booleans,

        Byte,
        NullableByte,
        Bytes,

        Char,
        NullableChar,
        Chars,

        Decimal,
        NullableDecimal,
        Decimals,

        Double,
        NullableDouble,
        Doubles,

        Float,
        NullableFloat,
        Floats,

        Int,
        NullableInt,
        Ints,

        Long,
        NullableLong,
        Longs,

        Obj,
        Objs,

        SByte,
        NullableSByte,
        SBytes,

        Short,
        NullableShort,
        Shorts,

        Str,
        Strs,

        UInt,
        NullableUInt,
        UInts,

        ULong,
        NullableULong,
        ULongs,

        UShort,
        NullableUShort,
        UShorts
    }

    internal static class TypeReaderWriter
    {
        private static readonly Dictionary<Type, ShortTypes> map = new Dictionary<Type, ShortTypes>
        {
            { typeof(DateTime), ShortTypes.DateTime },
            { typeof(DateTime?), ShortTypes.NullableDateTime },
            { typeof(DateTime[]), ShortTypes.DateTimes },

            { typeof(Guid), ShortTypes.Guid },
            { typeof(Guid?), ShortTypes.NullableGuid },
            { typeof(Guid[]), ShortTypes.Guids },

            { typeof(TimeSpan), ShortTypes.TimeSpan },
            { typeof(TimeSpan?), ShortTypes.NullableTimeSpan },
            { typeof(TimeSpan[]), ShortTypes.TimeSpans },

            { typeof(Type), ShortTypes.Type },
            { typeof(Type[]), ShortTypes.Types },

            { typeof(bool), ShortTypes.Boolean },
            { typeof(bool?), ShortTypes.NullableBoolean },
            { typeof(bool[]), ShortTypes.Booleans },

            { typeof(byte), ShortTypes.Byte },
            { typeof(byte?), ShortTypes.NullableByte },
            { typeof(byte[]), ShortTypes.Bytes },

            { typeof(char), ShortTypes.Char },
            { typeof(char?), ShortTypes.NullableChar },
            { typeof(char[]), ShortTypes.Chars },

            { typeof(decimal), ShortTypes.Decimal },
            { typeof(decimal?), ShortTypes.NullableDecimal },
            { typeof(decimal[]), ShortTypes.Decimals },

            { typeof(double), ShortTypes.Double },
            { typeof(double?), ShortTypes.NullableDouble },
            { typeof(double[]), ShortTypes.Doubles },

            { typeof(float), ShortTypes.Float },
            { typeof(float?), ShortTypes.NullableFloat },
            { typeof(float[]), ShortTypes.Floats },

            { typeof(int), ShortTypes.Int },
            { typeof(int?), ShortTypes.NullableInt },
            { typeof(int[]), ShortTypes.Ints },

            { typeof(long), ShortTypes.Long },
            { typeof(long?), ShortTypes.NullableLong },
            { typeof(long[]), ShortTypes.Longs },

            { typeof(object), ShortTypes.Obj },
            { typeof(object[]), ShortTypes.Objs },

            { typeof(sbyte), ShortTypes.SByte },
            { typeof(sbyte?), ShortTypes.NullableSByte },
            { typeof(sbyte[]), ShortTypes.SBytes },

            { typeof(short), ShortTypes.Short },
            { typeof(short?), ShortTypes.NullableShort },
            { typeof(short[]), ShortTypes.Shorts },

            { typeof(string), ShortTypes.Str },
            { typeof(string[]), ShortTypes.Strs },

            { typeof(uint), ShortTypes.UInt },
            { typeof(uint?), ShortTypes.NullableUInt },
            { typeof(uint[]), ShortTypes.UInts },

            { typeof(ulong), ShortTypes.ULong },
            { typeof(ulong?), ShortTypes.NullableULong },
            { typeof(ulong[]), ShortTypes.ULongs },

            { typeof(ushort), ShortTypes.UShort },
            { typeof(ushort?), ShortTypes.NullableUShort },
            { typeof(ushort[]), ShortTypes.UShorts }
        };

        public static Type ReadType(this BinaryReader reader)
        {
            var shortType = (ShortTypes)reader.ReadByte();
            return shortType == ShortTypes.Unknown ? Type.GetType(reader.ReadString(), true) : map.First(item => item.Value == shortType).Key;
        }

        public static void Write(this BinaryWriter writer, Type type, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.Type);
            }
            if (map.TryGetValue(type, out var shortType))
            {
                writer.Write((byte)shortType);
            }
            else
            {
                writer.Write((byte)ShortTypes.Unknown);
                writer.Write(type.AssemblyQualifiedName ?? throw new ArgumentException("Unable to serialize type."));
            }
        }
    }
}
