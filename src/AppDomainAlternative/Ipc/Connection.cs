using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Proxy;
using AppDomainAlternative.Serializer;

#pragma warning disable AsyncFixer03 // Avoid fire & forget async void methods

namespace AppDomainAlternative.Ipc
{
    internal interface IConnection : IDisposable, IEnumerable<IInternalChannel>, IResolveProxyIds
    {
        Task<object> CreateInstance(ConstructorInfo ctor, bool hostInstance, params object[] arguments);
        event Action<Domains, IChannel> NewChannel;
        void Terminate(IInternalChannel channel);
        void Write(long channelId, Stream stream);
    }

    internal class Connection : IConnection, IHaveChannels, IObservable<(Domains domain, IChannel channel)>
    {
        IDisposable IObservable<(Domains domain, IChannel channel)>.Subscribe(IObserver<(Domains domain, IChannel channel)> observer) => new ObservableListener(this, observer);
        IEnumerator<IChannel> IEnumerable<IChannel>.GetEnumerator() => channels.Values.Where(channel => channel.Instance != null).GetEnumerator();
        IEnumerator<IInternalChannel> IEnumerable<IInternalChannel>.GetEnumerator() => channels.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => channels.Values.GetEnumerator();
        IObservable<(Domains domain, IChannel channel)> IHaveChannels.AsObservable => this;
        bool IResolveProxyIds.TryToGetInstance(long id, out object instance)
        {
            if (!channels.TryGetValue(id, out var channel) ||
                channel.Instance == null ||
                channel.IsDisposed)
            {
                instance = null;
                return false;
            }

            instance = channel.Instance;

            return true;
        }
        bool IResolveProxyIds.TryToGetInstanceId(object instance, out long id)
        {
            if (instance == null)
            {
                id = 0;
                return false;
            }

            try
            {
                var match = channels.Values.SingleOrDefault(channel => ReferenceEquals(channel.Instance, instance));

                if (match == null || match.IsDisposed)
                {
                    id = 0;
                    return false;
                }

                id = match.Id;
                return true;
            }
            catch
            {
                id = 0;
                return false;
            }
        }

        internal Connection(
            Domains domain,
            IAmASerializer serializer,
            IGenerateProxies proxyGenerator,
            Stream reader,
            Stream writer)
            : this (domain is CurrentDomain, serializer, proxyGenerator, reader, writer)
        {
            this.domain = domain;
        }

        internal class ObservableListener : IDisposable
        {
            private int disposed;
            private readonly Connection connection;
            private readonly Guid id = Guid.NewGuid();
            private readonly IObserver<(Domains domain, IChannel channel)> observer;
            private readonly List<IChannel> observedChannels;

            public ObservableListener(Connection connection, IObserver<(Domains domain, IChannel channel)> observer)
            {
                this.connection = connection;
                this.observer = observer ?? throw new ArgumentNullException(nameof(observer));

                connection.listeners[id] = this;

                observedChannels = new List<IChannel>(connection);
                connection.NewChannel += OnNewChannel;

                foreach (var channel in observedChannels)
                {
                    try
                    {
                        observer.OnNext((connection.domain, channel));
                    }
                    catch
                    {
                        // ignored
                    }
                }

                observedChannels = null;
            }
            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref disposed, 1, 0) == 1)
                {
                    return;
                }

                connection.listeners.TryRemove(id, out _);

                connection.NewChannel -= OnNewChannel;

                try
                {
                    observer.OnCompleted();
                }
                catch
                {
                    // ignored
                }
            }
            public void OnNewChannel(Domains domain, IChannel channel)
            {
                if (observedChannels?.Contains(channel) ?? false)
                {
                    return;
                }

                try
                {
                    observer.OnNext((domain, channel));
                }
                catch
                {
                    // ignored
                }
            }
        }

        protected Connection(
            bool isParent,
            IAmASerializer serializer,
            IGenerateProxies proxyGenerator,
            Stream reader,
            Stream writer)
        {
            this.isParent = isParent;
            this.proxyGenerator = proxyGenerator;
            this.reader = reader;
            this.writer = new BinaryWriter(writer);
            Serializer = serializer;

            startWriter();
            startReader();
        }
        protected virtual IInternalChannel ChannelFactory(long id) => new Channel(id, this, Serializer);

        private IInternalChannel createChannel()
        {
            throwIfDisposed();

            while (channels.Count < int.MaxValue)
            {
                var wasAdded = false;

                //The index is partitioned so the first 32 bits are for remote host generated ids
                //and the last 32 bits are for client generated ids.
                //Partitioning the id bits means no race condition can happen when ids are generated.

                var id = Interlocked.Increment(ref channelCounter) & 0x00000000FFFFFFFFL;

                if (!isParent)
                {
                    id = id << 32;
                }

                var channel = channels.GetOrAdd(id, _ =>
                {
                    wasAdded = true;
                    return ChannelFactory(id);
                });

                if (wasAdded)
                {
                    return channel;
                }
            }

            throw new InvalidOperationException("There are too many streams open.");
        }
        private async void startReader()
        {
            await Task.Yield();

            try
            {
                var index = 0;
                var readBuffer = new byte[8 + 4];

                while (disposed == 0)
                {
                    while (disposed == 0 && index < readBuffer.Length)
                    {
                        index += await reader.ReadAsync(readBuffer, index, readBuffer.Length - index, disposeToken.Token).ConfigureAwait(false);
                    }

                    if (disposed > 0)
                    {
                        break;
                    }

                    index = 0;

                    var id = BitConverter.ToInt64(readBuffer, 0);
                    var length = BitConverter.ToInt32(readBuffer, 8);

                    if (length == 0)
                    {
                        if (channels.TryGetValue(id, out var closedStream))
                        {
                            //disposing the stream will remove the instance from the "streams" collection
                            closedStream.Dispose();
                        }
                        continue;
                    }

                    var isNew = false;
                    var channel = channels.GetOrAdd(id, newId =>
                    {
                        isNew = true;
                        return ChannelFactory(newId);
                    });

                    if (!channel.IsDisposed)
                    {
                        await channel.Buffer.Fill(reader, length, disposeToken.Token).ConfigureAwait(false);
                    }

                    if (!isNew)
                    {
                        continue;
                    }

                    await channel.RemoteStart(proxyGenerator).ConfigureAwait(false);

                    try
                    {
                        NewChannel?.Invoke(domain, channel);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            catch
            {
                Dispose();
            }
        }
        private async void startWriter()
        {
            await Task.Yield();

            try
            {
                using (newRequests)
                {
                    while (disposed == 0)
                    {
                        newRequests.Wait(disposeToken.Token);

                        while (disposed == 0 && pendingWrites.TryDequeue(out var request))
                        {
                            newRequests.Reset();
                            using (request.data)
                            {
                                try
                                {
                                    if (request.data == null || request.data.Length == 0)
                                    {
                                        if (channels.TryRemove(request.id, out var channelToDispose) && !channelToDispose.IsDisposed)
                                        {
                                            try
                                            {
                                                channelToDispose.Dispose();
                                            }
                                            catch
                                            {
                                                // ignored
                                            }
                                        }
                                        writer.Write(request.id);
                                        writer.Write(0);
                                        (writer.BaseStream as PipeStream)?.WaitForPipeDrain();
                                    }
                                    else if (channels.TryGetValue(request.id, out var channel) && !channel.IsDisposed)
                                    {
                                        writer.Write(request.id);
                                        writer.Write((int)request.data.Length);
                                        await request.data.CopyToAsync(writer.BaseStream).ConfigureAwait(false);
                                        (writer.BaseStream as PipeStream)?.WaitForPipeDrain();
                                    }
                                }
                                catch
                                {
                                    disposed = 1;
                                }
                            }
                        }
                    }

                    foreach (var stream in channels.Values)
                    {
                        using (stream)
                        {
                        }
                    }

                    while (pendingWrites.TryDequeue(out var request))
                    {
                        using (request.data)
                        {
                        }
                    }
                }
            }
            catch
            {
                Dispose();
            }
        }
        private int disposed, channelCounter;
        private readonly BinaryWriter writer;
        private readonly CancellationTokenSource disposeToken = new CancellationTokenSource();
        private readonly ConcurrentDictionary<Guid, ObservableListener> listeners = new ConcurrentDictionary<Guid, ObservableListener>();
        private readonly ConcurrentDictionary<long, IInternalChannel> channels = new ConcurrentDictionary<long, IInternalChannel>();
        private readonly ConcurrentQueue<(long id, Stream data)> pendingWrites = new ConcurrentQueue<(long id, Stream data)>();
        private readonly Domains domain;
        private readonly IGenerateProxies proxyGenerator;
        private readonly ManualResetEventSlim newRequests = new ManualResetEventSlim(false);
        private readonly Stream reader;
        private readonly bool isParent;
        private void throwIfDisposed()
        {
            if (disposed > 0)
            {
                throw new ObjectDisposedException(nameof(Connection));
            }
        }

        public IAmASerializer Serializer { get; }
        public async Task<(bool IsHost, T Instance)> GetInstanceOf<T>(FetchInstanceBy fetch = FetchInstanceBy.Any, Func<bool, T, bool> filter = null,
            CancellationToken cancel = default(CancellationToken))
            where T : class
        {
            var existingInstance = (fetch & FetchInstanceBy.Existing) == FetchInstanceBy.Existing ? channels.Values
                .Where(channel => channel.Instance != null)
                .Select(channel => new
                {
                    channel.IsHost,
                    Instance = channel.Instance as T
                })
                .FirstOrDefault(channel =>
                    channel.Instance != null &&
                    (filter?.Invoke(channel.IsHost, channel.Instance) ?? true)) : null;

            if (existingInstance != null)
            {
                return (existingInstance.IsHost, existingInstance.Instance);
            }

            if ((fetch & FetchInstanceBy.Next) != FetchInstanceBy.Next)
            {
                return (false, null);
            }

            var task = new TaskCompletionSource<(bool IsHost, T Instance)>();

            cancel.Register(() => task.TrySetCanceled());

            var listener = new Action<Domains, IChannel>((_, channel) =>
            {
                if (channel.Instance is T newInstance &&
                    (filter?.Invoke(channel.IsHost, newInstance) ?? true))
                {
                    task.TrySetResult((channel.IsHost, newInstance));
                }
            });

            NewChannel += listener;

            try
            {
                await task.Task.ConfigureAwait(false);

                return task.Task.Result;
            }
            finally
            {
                NewChannel -= listener;
            }
        }
        public async Task<object> CreateInstance(ConstructorInfo ctor, bool hostInstance, params object[] arguments)
        {
            var channel = createChannel();

            try
            {
                await channel.LocalStart(proxyGenerator, ctor, hostInstance, arguments).ConfigureAwait(false);

                try
                {
                    NewChannel?.Invoke(domain, channel);
                }
                catch
                {
                    // ignored
                }

                return channel.Instance;
            }
            catch
            {
                channel.Dispose();
                throw;
            }
        }
        public event Action<Domains, IChannel> NewChannel;
        public override string ToString() => isParent ? "Parent Connection" : "Child Connection";
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 1)
            {
                return;
            }

            disposeToken.Cancel();
            disposeToken.Dispose();

            reader?.Dispose();
            writer?.Dispose();

            (domain as IDisposable)?.Dispose();

            foreach (var listener in listeners.Values)
            {
                listener.Dispose();
            }
        }
        public void Terminate(IInternalChannel channel)
        {
            if (disposed > 0)
            {
                return;
            }
            channels.TryRemove(channel.Id, out _);
            pendingWrites.Enqueue((channel.Id, null));
            newRequests.Set();
        }
        public void Write(long channelId, Stream stream)
        {
            throwIfDisposed();
            pendingWrites.Enqueue((channelId, stream));
            newRequests.Set();
        }
    }

    /// <summary>
    /// A collection of <inheritdoc cref="IChannel"/>s use for remoting between <see cref="Domains"/>.
    /// </summary>
    public interface IHaveChannels : IEnumerable<IChannel>, IResolveProxyIds
    {
        /// <summary>
        /// An <see cref="IObservable{T}"/> stream of existing and new <see cref="IChannel"/>.
        /// </summary>
        IObservable<(Domains domain, IChannel channel)> AsObservable { get; }

        /// <summary>
        /// Gets the next instance of a shared instance.
        /// </summary>
        /// <param name="fetch">How the instance should be fetched.</param>
        /// <param name="filter">A filter for fetching the instance.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the await.</param>
        Task<(bool IsHost, T Instance)> GetInstanceOf<T>(
            FetchInstanceBy fetch = FetchInstanceBy.Any,
            Func<bool, T, bool> filter = null,
            CancellationToken cancel = default(CancellationToken))
            where T : class;

        /// <summary>
        /// Creates a proxy instance from a ctor for a class.
        /// </summary>
        /// <param name="baseCtor">The constructor for the class to generate a proxy for.</param>
        /// <param name="hostInstance">If true, the object instance should exist within this process and all method calls from the child/parent process are proxied to this process.</param>
        /// <param name="arguments">The constructor arguments.</param>
        Task<object> CreateInstance(ConstructorInfo baseCtor, bool hostInstance, params object[] arguments);

        /// <summary>
        /// Is invoked when a new <see cref="IChannel"/> is opened.
        /// </summary>
        event Action<Domains, IChannel> NewChannel;
    }

    /// <summary>
    /// When getting a shared instance between <see cref="Domains"/> how the get should fetch the instance.
    /// </summary>
    [Flags]
    public enum FetchInstanceBy
    {
        /// <summary>
        /// <see cref="Existing"/> or <see cref="Next"/>
        /// </summary>
        Any = Existing | Next,

        /// <summary>
        /// Only gets an instance if it was already created.
        /// </summary>
        Existing = 1,

        /// <summary>
        /// Only gets the next created instance.
        /// </summary>
        Next = 2
    }
}
