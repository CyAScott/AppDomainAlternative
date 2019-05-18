using System;
using System.IO;
using System.Threading.Tasks;

namespace AppDomainAlternative.Serializer.Default
{
    internal static class ArrayReaderWriter
    {
        private static async Task read(this BinaryReader reader, Array array, ObjectType? elementObjectType, Type elementType, bool nullable, int dimension, int[] indexArray, IResolveProxyIds resolver)
        {
            var upperBound = array.GetUpperBound(dimension);
            var lastDimension = dimension == array.Rank - 1;
            for (indexArray[dimension] = array.GetLowerBound(dimension); indexArray[dimension] <= upperBound; indexArray[dimension]++)
            {
                if (lastDimension)
                {
                    if (nullable)
                    {
                        if (reader.ReadBoolean())
                        {
                            continue;
                        }
                    }

                    object element;
                    switch (elementObjectType)
                    {
                        case null:
                            element = await reader.ReadObject(resolver).ConfigureAwait(false);
                            break;
                        case ObjectType.Enum:
                            element = reader.ReadEnum(elementType);
                            break;
                        default:
                            element = await reader.ReadObject(elementObjectType.Value, resolver).ConfigureAwait(false);
                            break;
                    }

                    array.SetValue(element, indexArray);
                }
                else
                {
                    await reader.read(array, elementObjectType, elementType, nullable, dimension + 1, indexArray, resolver).ConfigureAwait(false);
                }
            }
        }
        public static async Task<object> ReadArray(this BinaryReader reader, IResolveProxyIds resolver)
        {
            var elementType = reader.ReadType();
            var rank = (int)reader.ReadByte();

            var lengths = new int[rank];
            var lowerBounds = new int[rank];
            for (var dimension = 0; dimension < rank; dimension++)
            {
                lengths[dimension] = reader.ReadInt32();
                lowerBounds[dimension] = reader.ReadInt32();
            }

            var array = Array.CreateInstance(elementType, lengths, lowerBounds);

            var underlyingType = Nullable.GetUnderlyingType(elementType);
            var nullable = !elementType.IsValueType || underlyingType != null;

            elementType = underlyingType ?? elementType;

            ObjectType? elementObjectType;
            if (elementType.IsArray)
            {
                elementObjectType = ObjectType.Array;
            }
            else if (elementType.IsEnum)
            {
                elementObjectType = ObjectType.Enum;
            }
            else if (elementType.IsClass && !elementType.IsSealed)
            {
                elementObjectType = null;
            }
            else if (elementType == typeof(DateTime))
            {
                elementObjectType = ObjectType.DateTime;
            }
            else if (elementType == typeof(Guid))
            {
                elementObjectType = ObjectType.Guid;
            }
            else if (elementType == typeof(TimeSpan))
            {
                elementObjectType = ObjectType.TimeSpan;
            }
            else if (elementType == typeof(Type))
            {
                elementObjectType = ObjectType.Type;
            }
            else if (elementType == typeof(bool))
            {
                elementObjectType = ObjectType.Boolean;
            }
            else if (elementType == typeof(byte))
            {
                elementObjectType = ObjectType.Byte;
            }
            else if (elementType == typeof(byte[]))
            {
                elementObjectType = ObjectType.Bytes;
            }
            else if (elementType == typeof(char))
            {
                elementObjectType = ObjectType.Char;
            }
            else if (elementType == typeof(decimal))
            {
                elementObjectType = ObjectType.Decimal;
            }
            else if (elementType == typeof(double))
            {
                elementObjectType = ObjectType.Double;
            }
            else if (elementType == typeof(float))
            {
                elementObjectType = ObjectType.Float;
            }
            else if (elementType == typeof(int))
            {
                elementObjectType = ObjectType.Int;
            }
            else if (elementType == typeof(long))
            {
                elementObjectType = ObjectType.Long;
            }
            else if (elementType == typeof(sbyte))
            {
                elementObjectType = ObjectType.SByte;
            }
            else if (elementType == typeof(short))
            {
                elementObjectType = ObjectType.Short;
            }
            else if (elementType == typeof(string))
            {
                elementObjectType = ObjectType.Str;
            }
            else if (elementType == typeof(uint))
            {
                elementObjectType = ObjectType.UInt;
            }
            else if (elementType == typeof(ulong))
            {
                elementObjectType = ObjectType.ULong;
            }
            else if (elementType == typeof(ushort))
            {
                elementObjectType = ObjectType.Short;
            }
            else if (elementType == typeof(ushort))
            {
                elementObjectType = ObjectType.Short;
            }
            else if (elementType.IsSerializable)
            {
                elementObjectType = ObjectType.Serializable;
            }
            else
            {
                throw new ArgumentException($"Unable serialize array with elements of: {elementType}");
            }

            await reader.read(array, elementObjectType, elementType, nullable, 0, new int[array.Rank], resolver).ConfigureAwait(false);

            return array;
        }

        private static void write(this BinaryWriter writer, Array array, bool nullable, bool writeHeader, int dimension, int[] indexArray, IResolveProxyIds resolver)
        {
            var upperBound = array.GetUpperBound(dimension);
            var lastDimension = dimension == array.Rank - 1;
            for (indexArray[dimension] = array.GetLowerBound(dimension); indexArray[dimension] <= upperBound; indexArray[dimension]++)
            {
                if (lastDimension)
                {
                    var element = array.GetValue(indexArray);

                    if (nullable)
                    {
                        writer.Write(element == null);
                    }

                    if (element != null)
                    {
                        writer.Write(element.GetType(), element, resolver, writeHeader);
                    }
                }
                else
                {
                    writer.write(array, nullable, writeHeader, dimension + 1, indexArray, resolver);
                }
            }
        }
        public static void Write(this BinaryWriter writer, Type arrayType, Array array, IResolveProxyIds resolver, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.Array);
            }

            var elementType = arrayType.GetElementType();

            // ReSharper disable once PossibleNullReferenceException
            var nullable = !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null;

            writer.Write(elementType, false);
            writer.Write((byte)array.Rank);

            for (var dimension = 0; dimension < array.Rank; dimension++)
            {
                writer.Write(array.GetLength(dimension));
                writer.Write(array.GetLowerBound(dimension));
            }

            writer.write(array, nullable, elementType.IsClass && !elementType.IsSealed, 0, new int[array.Rank], resolver);
        }
    }
}
