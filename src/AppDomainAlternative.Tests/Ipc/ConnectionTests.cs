using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Proxy;
using AppDomainAlternative.Serializer;
using AppDomainAlternative.Serializer.Default;
using NUnit.Framework;

// ReSharper disable AccessToDisposedClosure

namespace AppDomainAlternative.Ipc
{
    [TestFixture]
    public class ConnectionTests
    {
        internal class MockChannel : IInternalChannel
        {
            public MockChannel(long id, IConnection connection, IAmASerializer serializer)
            {
                Buffer = new ReadWriteBuffer(id);

                Connection = connection;
                Reader = new BinaryReader(Buffer, Encoding.UTF8);
                Id = id;
                Serializer = serializer;
            }
            public BinaryReader Reader { get; }
            public IAmASerializer Serializer { get; }
            public IConnection Connection { get; }
            public ReadWriteBuffer Buffer { get; }
            public Task<T> RemoteInvoke<T>(bool fireAndForget, string methodName, params Tuple<Type, object>[] args) => throw new NotSupportedException();
            public Task LocalStart(IGenerateProxies proxyGenerator, ConstructorInfo ctor, bool hostInstance, params object[] arguments)
            {
                IsHost = hostInstance;
                Connection.Write(Id, new MemoryStream(InitBytes.ToByteArray(), false));
                return Task.CompletedTask;
            }
            public Task LocalStart<T>(T instance)
                where T : class, new()
            {
                IsHost = true;
                Connection.Write(Id, new MemoryStream(InitBytes.ToByteArray(), false));
                return Task.CompletedTask;
            }
            public Task RemoteStart(IGenerateProxies proxyGenerator)
            {
                IsHost = false;
                return Task.CompletedTask;
            }
            public bool IsDisposed { get; private set; }
            public bool IsHost { get; private set; }
            public long Id { get; }
            public object Instance { get; } = new object();
            public override string ToString() => IsHost ? "Host Side Channel" : "Client Side Channel";
            public void Dispose()
            {
                IsDisposed = true;
                Buffer.Dispose();
            }

            /// <summary>
            /// Used init the channel.
            /// </summary>
            public Guid InitBytes { get; } = Guid.NewGuid();
        }

        internal class TestConnection : Connection
        {
            internal TestConnection(bool isParent, IAmASerializer serializer, IGenerateProxies proxyGenerator, Stream reader, Stream writer)
                : base(isParent, serializer, proxyGenerator, reader, writer)
            {
            }

            protected override IInternalChannel ChannelFactory(long id) => new MockChannel(id, this, Serializer);
        }

        internal static async Task VerifyConnectionIsDuplexed(
            IConnection child, IConnection parent,
            CancellationTokenSource cancel,
            MockChannel childSideChannel, MockChannel parentSideChannel)
        {
            //send additional data from the parent across the channel
            var data = Guid.NewGuid();
            var dataCopy = new byte[16];
            parent.Write(parentSideChannel.Id, new MemoryStream(data.ToByteArray()));
            Assert.AreEqual(dataCopy.Length, await childSideChannel.Buffer.ReadAsync(dataCopy, 0, 16, cancel.Token).ConfigureAwait(false));
            Assert.AreEqual(data, new Guid(dataCopy.ToArray()));

            //send additional data from the child across the channel
            data = Guid.NewGuid();
            child.Write(childSideChannel.Id, new MemoryStream(data.ToByteArray()));
            Assert.AreEqual(dataCopy.Length, await parentSideChannel.Buffer.ReadAsync(dataCopy, 0, 16, cancel.Token).ConfigureAwait(false));
            Assert.AreEqual(data, new Guid(dataCopy.ToArray()));
        }

        internal static async void VerifyConnectionIsDuplexed(
            IConnection child, IConnection parent,
            CancellationTokenSource cancel,
            MockChannel childSideChannel, MockChannel parentSideChannel,
            TaskCompletionSource<bool> task)
        {
            await Task.Yield();

            try
            {
                var rnd = new Random();

                for (var index = 0; index < 1000 && !cancel.IsCancellationRequested; index++)
                {
                    await Task.Yield();

                    Thread.SpinWait(rnd.Next(1, 1000));

                    if (cancel.IsCancellationRequested)
                    {
                        break;
                    }

                    await VerifyConnectionIsDuplexed(child, parent, cancel, childSideChannel, parentSideChannel).ConfigureAwait(false);
                }

                task.TrySetResult(true);
            }
            catch (Exception error)
            {
                task.TrySetException(error);
            }
        }

        internal (IConnection parent, IConnection child) MakeConnections()
        {
            var parentReader = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
            var parentWriter = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);

            var parent = new TestConnection(true, new DefaultSerializer(), new DefaultProxyFactory(), parentWriter, parentReader);

            var child = new TestConnection(false, new DefaultSerializer(), new DefaultProxyFactory(),
                new AnonymousPipeClientStream(PipeDirection.In, parentReader.GetClientHandleAsString()),
                new AnonymousPipeClientStream(PipeDirection.Out, parentWriter.GetClientHandleAsString()));

            return (parent, child);
        }

        public class DummyClass
        {
            public virtual void EnableMethod()
            {
            }
        }

        [Test]
        public async Task CreateChannelFromClientTest()
        {
            var (parent, child) = MakeConnections();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (child)
            using (parent)
            {
                var newChannel = new TaskCompletionSource<IChannel>();

                cancel.Token.Register(() => newChannel.TrySetCanceled(cancel.Token));
                parent.NewChannel += (domains, channel) => newChannel.TrySetResult(channel);

                //create a new channel
                await child.CreateInstance(typeof(DummyClass).GetConstructors().First(), true).ConfigureAwait(false);


                //wait for the channel to init on the server
                var childSideChannel = (MockChannel)child.First();
                var parentSideChannel = await newChannel.Task.ConfigureAwait(false) as MockChannel;
                Assert.IsNotNull(parentSideChannel);

                //validate init data was exchanged
                var dataCopy = new byte[16];
                Assert.AreEqual(dataCopy.Length, await parentSideChannel.Buffer.ReadAsync(dataCopy, 0, dataCopy.Length, cancel.Token).ConfigureAwait(false));
                Assert.AreEqual(childSideChannel.InitBytes, new Guid(dataCopy.ToArray()));

                await VerifyConnectionIsDuplexed(child, parent, cancel, childSideChannel, parentSideChannel).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task CreateChannelFromServerTest()
        {
            var (parent, child) = MakeConnections();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (child)
            using (parent)
            {
                var newChannel = new TaskCompletionSource<IChannel>();

                cancel.Token.Register(() => newChannel.TrySetCanceled(cancel.Token));
                child.NewChannel += (domains, channel) => newChannel.TrySetResult(channel);

                //create a new channel
                await parent.CreateInstance(typeof(DummyClass).GetConstructors().First(), true).ConfigureAwait(false);

                //wait for the channel to init on the client
                var childSideChannel = await newChannel.Task.ConfigureAwait(false) as MockChannel;
                var parentSideChannel = (MockChannel)parent.First();
                Assert.IsNotNull(childSideChannel);

                //validate init data was exchanged
                var dataCopy = new byte[16];
                Assert.AreEqual(dataCopy.Length, await childSideChannel.Buffer.ReadAsync(dataCopy, 0, 16, cancel.Token).ConfigureAwait(false));
                Assert.AreEqual(parentSideChannel.InitBytes, new Guid(dataCopy.ToArray()));

                await VerifyConnectionIsDuplexed(child, parent, cancel, childSideChannel, parentSideChannel).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task MultipleChannelsTest()
        {
            var (parent, child) = MakeConnections();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (child)
            using (parent)
            {
                const int numberOfChannels = 10;

                var newChannels = new ConcurrentDictionary<long, MockChannel>();
                var newChannel = new TaskCompletionSource<bool>();

                cancel.Token.Register(() => newChannel.TrySetCanceled(cancel.Token));
                child.NewChannel += (domains, channel) =>
                {
                    newChannels[((IInternalChannel)channel).Id] = (MockChannel)channel;
                    if (newChannels.Count >= numberOfChannels)
                    {
                        newChannel.TrySetResult(true);
                    }
                };

                //create new channels
                await Task.WhenAll(Enumerable.Range(0, numberOfChannels).Select(_ => parent.CreateInstance(typeof(DummyClass).GetConstructors().First(), true))).ConfigureAwait(false);

                //wait for the channels to init on the client
                await newChannel.Task.ConfigureAwait(false);
                var channelPairs = parent.Cast<MockChannel>()
                    .Select(parentSideChannel => new
                    {
                        childSideChannel = newChannels[parentSideChannel.Id],
                        parentSideChannel
                    })
                    .ToArray();

                await Task.WhenAll(channelPairs.Select(async pair =>
                {
                    //validate init data was exchanged
                    var dataCopy = new byte[16];
                    Assert.AreEqual(dataCopy.Length, await pair.childSideChannel.Buffer.ReadAsync(dataCopy, 0, 16, cancel.Token).ConfigureAwait(false));
                    Assert.AreEqual(pair.parentSideChannel.InitBytes, new Guid(dataCopy.ToArray()));

                    await VerifyConnectionIsDuplexed(child, parent, cancel, pair.childSideChannel, pair.parentSideChannel).ConfigureAwait(false);
                })).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task StressTest()
        {
            var (parent, child) = MakeConnections();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (child)
            using (parent)
            {
                const int numberOfChannels = 10;

                var newChannels = new ConcurrentDictionary<long, MockChannel>();
                var newChannel = new TaskCompletionSource<bool>();

                cancel.Token.Register(() => newChannel.TrySetCanceled(cancel.Token));
                child.NewChannel += (domains, channel) =>
                {
                    newChannels[((IInternalChannel)channel).Id] = (MockChannel)channel;
                    if (newChannels.Count >= numberOfChannels)
                    {
                        newChannel.TrySetResult(true);
                    }
                };

                //create new channels
                await Task.WhenAll(Enumerable.Range(0, numberOfChannels).Select(_ => parent.CreateInstance(typeof(DummyClass).GetConstructors().First(), true))).ConfigureAwait(false);

                //wait for the channels to init on the client
                await newChannel.Task.ConfigureAwait(false);
                var channelPairs = parent.Cast<MockChannel>()
                    .Select(parentSideChannel => new
                    {
                        childSideChannel = newChannels[parentSideChannel.Id],
                        parentSideChannel,
                        task = new TaskCompletionSource<bool>()
                    })
                    .ToArray();

                await Task.WhenAll(channelPairs.Select(async pair =>
                {
                    //validate init data was exchanged
                    var dataCopy = new byte[16];
                    Assert.AreEqual(dataCopy.Length, await pair.childSideChannel.Buffer.ReadAsync(dataCopy, 0, 16, cancel.Token).ConfigureAwait(false));
                    Assert.AreEqual(pair.parentSideChannel.InitBytes, new Guid(dataCopy.ToArray()));
                })).ConfigureAwait(false);

                foreach (var pair in channelPairs)
                {
                    VerifyConnectionIsDuplexed(child, parent, cancel, pair.childSideChannel, pair.parentSideChannel, pair.task);
                }

                await Task.WhenAll(channelPairs.Select(pair => pair.task.Task)).ConfigureAwait(false);
            }
        }
    }
}
