using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using DefaultFormatterConverter = System.Runtime.Serialization.FormatterConverter;

namespace AppDomainAlternative.Serializer.Default
{
    internal static class SerializableReaderWriter
    {
        private static ConstructorInfo getCtor(this Type type)
        {
            if (!serializableConstructors.TryGetValue(type, out var ctor))
            {
                ctor = type.GetConstructor(new[]
                {
                    typeof(SerializationInfo),
                    typeof(StreamingContext)
                });
                if (ctor != null)
                {
                    serializableConstructors[type] = ctor;
                }
            }

            return ctor;
        }
        private static FieldInfo[] getFields(this Type type)
        {
            if (!serializableFields.TryGetValue(type, out var members))
            {
                var fields = FormatterServices.GetSerializableMembers(type);
                if (fields != null)
                {
                    serializableFields[type] = members = fields.Cast<FieldInfo>().ToArray();
                }
            }

            if (members == null)
            {
                throw new ArgumentException($"Unable serialize: {type}");
            }

            return members;
        }
        private static SerializationInfo createSerializationInfo(this IResolveProxyIds resolver, Type type) => new SerializationInfo(type, new FormatterConverter(resolver));
        private static readonly ConcurrentDictionary<Type, ConstructorInfo> serializableConstructors = new ConcurrentDictionary<Type, ConstructorInfo>();
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> serializableFields = new ConcurrentDictionary<Type, FieldInfo[]>();

        public static async Task<object> ReadSerializable(this BinaryReader reader, IResolveProxyIds resolver, Type type = null)
        {
            type = type ?? reader.ReadType();

            var useCustomSerializer = reader.ReadBoolean();

            if (useCustomSerializer)
            {
                var context = new StreamingContext();
                var info = resolver.createSerializationInfo(type);
                var length = reader.ReadInt32();

                for (var index = 0; index < length; index++)
                {
                    var itemName = reader.ReadString();
                    var itemType = reader.ReadType();
                    var item = await reader.ReadObject(resolver).ConfigureAwait(false);

                    info.AddValue(itemName, item, itemType);
                }

                var ctor = type.getCtor() ?? throw new InvalidOperationException("Unable to find serialization constructor.");

                return ctor.Invoke(new object[]
                {
                    info, context
                });
            }

            var returnValue = FormatterServices.GetUninitializedObject(type);

            foreach (var field in type.getFields())
            {
                field.SetValue(returnValue, await reader.ReadObject(resolver).ConfigureAwait(false));
            }

            return returnValue;
        }
        public static void WriteSerializable(this BinaryWriter writer, IResolveProxyIds resolver, Type type, object value, bool writeHeader = true)
        {
            if (writeHeader)
            {
                writer.Write((byte)ObjectType.Serializable);
                writer.Write(type, false);
            }

            if (value is ISerializable serializable)
            {
                //true for custom serialization
                writer.Write(true);

                if (type.getCtor() == null)
                {
                    throw new InvalidOperationException("Unable to find serialization constructor.");
                }

                var context = new StreamingContext();
                var info = resolver.createSerializationInfo(type);
                serializable.GetObjectData(info, context);

                var start = writer.BaseStream.Position;
                writer.Write(new byte[sizeof(int)], 0, sizeof(int));

                var length = 0;
                foreach (var entry in info)
                {
                    length++;
                    writer.Write(entry.Name);
                    writer.Write(entry.ObjectType, false);
                    writer.Write(entry.Value?.GetType() ?? entry.ObjectType, entry.Value, resolver);
                }

                var stop = writer.BaseStream.Position;
                writer.BaseStream.Position = start;
                writer.Write(length);
                writer.BaseStream.Position = stop;
            }
            else
            {
                //false for custom serialization
                writer.Write(false);

                foreach (var field in type.getFields())
                {
                    var fieldValue = field.GetValue(value);
                    writer.Write(fieldValue?.GetType() ?? field.FieldType, fieldValue, resolver);
                }
            }
        }
    }

    internal class FormatterConverter : IFormatterConverter
    {
        DateTime IFormatterConverter.ToDateTime(object value) => converter.ToDateTime(value);
        bool IFormatterConverter.ToBoolean(object value) => converter.ToBoolean(value);
        byte IFormatterConverter.ToByte(object value) => converter.ToByte(value);
        char IFormatterConverter.ToChar(object value) => converter.ToChar(value);
        decimal IFormatterConverter.ToDecimal(object value) => converter.ToDecimal(value);
        double IFormatterConverter.ToDouble(object value) => converter.ToDouble(value);
        float IFormatterConverter.ToSingle(object value) => converter.ToSingle(value);
        int IFormatterConverter.ToInt32(object value) => converter.ToInt32(value);
        long IFormatterConverter.ToInt64(object value) => converter.ToInt64(value);
        object IFormatterConverter.Convert(object value, Type type)
        {
            if (value is long ptr && resolver.TryToGetInstance(ptr, out var instance))
            {
                return instance;
            }

            if (value != null && resolver.TryToGetInstanceId(value, out ptr))
            {
                return ptr;
            }

            return converter.Convert(value, type);
        }
        object IFormatterConverter.Convert(object value, TypeCode typeCode)
        {
            if (value is long ptr && resolver.TryToGetInstance(ptr, out var instance))
            {
                return instance;
            }

            if (value != null && resolver.TryToGetInstanceId(value, out ptr))
            {
                return ptr;
            }

            return converter.Convert(value, typeCode);
        }
        sbyte IFormatterConverter.ToSByte(object value) => converter.ToSByte(value);
        short IFormatterConverter.ToInt16(object value) => converter.ToInt16(value);
        string IFormatterConverter.ToString(object value) => converter.ToString(value);
        uint IFormatterConverter.ToUInt32(object value) => converter.ToUInt32(value);
        ulong IFormatterConverter.ToUInt64(object value) => converter.ToUInt64(value);
        ushort IFormatterConverter.ToUInt16(object value) => converter.ToUInt16(value);

        private readonly IResolveProxyIds resolver;
        private static readonly IFormatterConverter converter = new DefaultFormatterConverter();

        public FormatterConverter(IResolveProxyIds resolver) => this.resolver = resolver;
    }
}
