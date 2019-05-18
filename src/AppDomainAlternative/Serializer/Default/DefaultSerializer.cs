using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AppDomainAlternative.Serializer.Default
{
    internal class DefaultSerializer : IAmASerializer
    {
        public Task Serialize(BinaryWriter writer, Type valueType, object value, IResolveProxyIds resolver)
        {
            writer.Write(valueType, value, resolver);
            return Task.CompletedTask;
        }

        public Task<object> Deserialize(BinaryReader reader, Type valueType, IResolveProxyIds resolver, CancellationToken token) =>
            reader.ReadObject(resolver);

        public bool CanSerialize(Type type) => true;

        public static IAmASerializer Resolve(string name) => Instance;

        public static readonly IAmASerializer Instance = new DefaultSerializer();

        public string Name { get; } = $"DefaultSerializer@{typeof(DefaultSerializer).Assembly.GetName().Version}";
    }
}
