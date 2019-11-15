using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Extensions;
using AppDomainAlternative.Proxy;
using AppDomainAlternative.Serializer;
using AppDomainAlternative.Serializer.Default;

namespace AppDomainAlternative.Ipc.Channels
{
    internal class MockChannel : IInternalChannel
    {
        public MockChannel(MockConnection connection = null)
        {
            MockConnection = connection ?? new MockConnection();
            Buffer = new ReadWriteBuffer(Id = 7);
            Reader = new BinaryReader(Buffer, Encoding.UTF8);
        }

        public BinaryReader Reader { get; }
        public ConcurrentDictionary<int, TaskCompletionSource<object>> RemoteRequests { get; } = new ConcurrentDictionary<int, TaskCompletionSource<object>>();
        public IAmASerializer Serializer { get; } = new DefaultSerializer();
        public IConnection Connection => MockConnection;
        public MockConnection MockConnection { get; }
        public ReadWriteBuffer Buffer { get; }
        public Task<T> RemoteInvoke<T>(bool fireAndForget, string methodName, params Tuple<Type, object>[] args) =>
            this.RemoteInvoke<T>(RemoteRequests, () => Interlocked.Increment(ref RequestCounter), fireAndForget, methodName, args);
        public Task LocalStart(IGenerateProxies proxyGenerator, ConstructorInfo ctor, bool hostInstance, params object[] arguments) => throw new NotSupportedException();
        public Task LocalStart<T>(T instance)
            where T : class, new() => throw new NotSupportedException();
        public Task RemoteStart(IGenerateProxies proxyGenerator) => throw new NotSupportedException();
        public bool IsDisposed { get; } = false;
        public bool IsHost { get; set; }
        public int RequestCounter;
        public long Id { get; }
        public object Instance { get; set; }
        public void Dispose()
        {
        }
    }

    internal class MockConnection : IConnection
    {
        IEnumerator<IInternalChannel> IEnumerable<IInternalChannel>.GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
        Task<object> IConnection.CreateInstance(ConstructorInfo ctor, bool hostInstance, params object[] arguments) => throw new NotSupportedException();
        bool IResolveProxyIds.TryToGetInstanceId(object instance, out long id)
        {
            id = 0;
            return false;
        }
        bool IResolveProxyIds.TryToGetInstance(long id, out object instance)
        {
            instance = null;
            return false;
        }
        public event Action<Domains, IChannel> NewChannel;

        public event Action Terminations;
        public virtual void Terminate(IInternalChannel channel) => Terminations?.Invoke();

        public event Action<long, Stream> Writes;
        public virtual void Write(long channelId, Stream stream) => Writes?.Invoke(channelId, stream);

        public virtual void Dispose()
        {
        }
    }
}
