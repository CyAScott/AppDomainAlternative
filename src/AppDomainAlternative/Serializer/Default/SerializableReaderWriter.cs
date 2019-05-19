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
        private static ConstructorInfo getCtor(this Type type) =>
            serializableConstructors.GetOrAdd(type, @class => @class
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(it => new
                {
                    ctor = it,
                    @params = it.GetParameters()
                })
                .FirstOrDefault(it => it.@params.Length == 2 &&
                                      it.@params[0].ParameterType == typeof(SerializationInfo) &&
                                      it.@params[1].ParameterType == typeof(StreamingContext))
                ?.ctor ?? throw new InvalidOperationException("Unable to find serialization constructor."));
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
        private static readonly ConcurrentDictionary<Type, ConstructorInfo> serializableConstructors = new ConcurrentDictionary<Type, ConstructorInfo>();
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> serializableFields = new ConcurrentDictionary<Type, FieldInfo[]>();

        public static async Task<object> ReadSerializable(this BinaryReader reader, IResolveProxyIds resolver, Type type = null)
        {
            type = type ?? reader.ReadType();

            var useCustomSerializer = reader.ReadBoolean();

            if (useCustomSerializer)
            {
                var context = new StreamingContext(StreamingContextStates.All, resolver);
                var info = new SerializationInfo(type, new DefaultFormatterConverter());
                var length = reader.ReadInt32();

                for (var index = 0; index < length; index++)
                {
                    var itemName = reader.ReadString();
                    var itemType = reader.ReadType();
                    var item = await reader.ReadObject(resolver).ConfigureAwait(false);

                    info.AddValue(itemName, item, itemType);
                }

                var ctor = type.getCtor();

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

                type.getCtor();

                var context = new StreamingContext(StreamingContextStates.All, resolver);
                var info = new SerializationInfo(type, new DefaultFormatterConverter());
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
}
