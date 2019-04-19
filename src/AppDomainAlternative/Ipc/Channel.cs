using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Extensions;
using AppDomainAlternative.Proxy;
using AppDomainAlternative.Serializer;

#pragma warning disable AsyncFixer03 // Avoid fire & forget async void methods

namespace AppDomainAlternative.Ipc
{
    /// <summary>
    /// An IPC channel for sharing an instance across domains.
    /// </summary>
    public interface IChannel
    {
        /// <summary>
        /// If the instance is hosted from this domain.
        /// </summary>
        bool IsHost { get; }

        /// <summary>
        /// The shared instance between domains.
        /// </summary>
        object Instance { get; }
    }

    internal interface IInternalChannel : IChannel, IDisposable, IInterceptor
    {
        BinaryReader Reader { get; }
        IConnection Connection { get; }
        ReadWriteBuffer Buffer { get; }
        Task LocalStart(IGenerateProxies proxyGenerator, ConstructorInfo ctor, bool hostInstance, params object[] arguments);
        Task RemoteStart(IGenerateProxies proxyGenerator);
        bool IsDisposed { get; }
        long Id { get; }
    }

    internal sealed class Channel : IInternalChannel
    {
        private int disposed, requestCounter;
        private readonly CancellationTokenSource disposeToken = new CancellationTokenSource();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<object>> remoteRequests = new ConcurrentDictionary<int, TaskCompletionSource<object>>();
        private void throwIfDisposed()
        {
            if (disposed > 0)
            {
                throw new ObjectDisposedException(nameof(Channel));
            }
        }

        public Channel(long id, IConnection connection, IAmASerializer serializer)
        {
            Buffer = new ReadWriteBuffer(id);

            Connection = connection;
            Reader = new BinaryReader(Buffer, Encoding.Unicode);
            Id = id;
            IdBytes = BitConverter.GetBytes(id);
            Serializer = serializer;
        }

        public BinaryReader Reader { get; }
        public IAmASerializer Serializer { get; }
        public IConnection Connection { get; }
        public ReadWriteBuffer Buffer { get; }
        public Task<T> RemoteInvoke<T>(bool fireAndForget, string methodName, params Tuple<Type, object>[] args)
        {
            throwIfDisposed();

            return this.RemoteInvoke<T>(remoteRequests, () => Interlocked.Increment(ref requestCounter), fireAndForget, methodName, args);
        }
        public async Task LocalStart(IGenerateProxies proxyGenerator, ConstructorInfo ctor, bool hostInstance, params object[] arguments)
        {
            Instance = await this.LocalStart(disposeToken, proxyGenerator, ctor, hostInstance, arguments).ConfigureAwait(false);

            IsHost = hostInstance;

            this.StartListening(disposeToken, remoteRequests);
        }
        public async Task RemoteStart(IGenerateProxies proxyGenerator)
        {
            var (isHost, instance) = await this.RemoteStart(disposeToken, proxyGenerator).ConfigureAwait(false);

            IsHost = isHost;
            Instance = instance;

            this.StartListening(disposeToken, remoteRequests);
        }
        public bool IsDisposed => disposed > 0;
        public bool IsHost { get; private set; }
        public byte[] IdBytes { get; }
        public long Id { get; }
        public object Instance { get; private set; }
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
            {
                return;
            }

            using (disposeToken)
            {
                try
                {
                    disposeToken.Cancel();
                }
                catch
                {
                    // ignored
                }
            }

            Connection.Terminate(this);
            Buffer.Dispose();

            var requests = remoteRequests.Values.ToArray();
            remoteRequests.Clear();
            foreach (var request in requests)
            {
                request.TrySetCanceled();
            }

            Reader.Dispose();
        }
    }
}
