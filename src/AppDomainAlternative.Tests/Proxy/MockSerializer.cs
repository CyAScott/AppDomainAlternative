using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Serializer;

namespace AppDomainAlternative.Proxy
{
    public class MockSerializer : IAmASerializer
    {
        public Task Serialize(BinaryWriter writer, Type valueType, object value, IResolveProxyIds resolver) => throw new NotSupportedException();

        public Task<object> Deserialize(BinaryReader reader, Type valueType, IResolveProxyIds resolver, CancellationToken token) => throw new NotSupportedException();

        public Func<Type, bool> CanSerializeInterceptor = _ => true;
        public bool CanSerialize(Type type) => CanSerializeInterceptor(type);

        public string Name { get; } = nameof(MockSerializer);
    }
}
