using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Extensions;
using AppDomainAlternative.Proxy;
using NUnit.Framework;

// ReSharper disable AccessToDisposedClosure

namespace AppDomainAlternative.Ipc.Channels
{
    [TestFixture]
    public class ChannelListenerTest
    {
        public const string ArgumentNullMsg = "The GUID value was null.";

        public class DummyClass
        {
            public ConcurrentBag<Guid> Values { get; } = new ConcurrentBag<Guid>();
            public virtual Task<string> SetValue(Guid? value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(ArgumentNullMsg);
                }

                Values.Add(value.Value);

                return Task.FromResult(value.ToString());
            }
        }

        internal class MockConnectionLayer : MockConnection
        {
            private ManualResetEventSlim signal;
            private readonly ConcurrentQueue<(long id, Stream data)> queue = new ConcurrentQueue<(long id, Stream data)>();
            private int disposed;

            public async void Start(MockChannel channel, CancellationTokenSource cancel)
            {
                await Task.Yield();

                signal = new ManualResetEventSlim(false);

                while (!cancel.IsCancellationRequested && disposed == 0)
                {
                    signal.Wait(cancel.Token);

                    while (queue.TryDequeue(out var request) && !cancel.IsCancellationRequested && disposed == 0)
                    {
                        await channel.Buffer.Fill(request.data, (int)request.data.Length, cancel.Token).ConfigureAwait(false);
                    }
                }

                signal.Dispose();
            }
            public override void Terminate(IInternalChannel channel)
            {
                Dispose();
                base.Terminate(channel);
            }
            public override void Write(long channelId, Stream stream)
            {
                queue.Enqueue((channelId, stream));
                signal.Set();
                base.Write(channelId, stream);
            }
            public override void Dispose()
            {
                Interlocked.CompareExchange(ref disposed, 1, 0);
                signal.Set();
            }
        }

        #region Stress Test

        public async void ThreadTest(
            CancellationToken cancel,
            DummyClass remoteInstance,
            ConcurrentBag<Guid> values,
            TaskCompletionSource<bool> onFinish)
        {
            try
            {
                await Task.Yield();

                var cancelTask = new TaskCompletionSource<bool>();
                var ids = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToArray();
                var rnd = new Random();

                cancel.Register(() => cancelTask.TrySetResult(true));

                foreach (var value in ids.TakeWhile(_ => !cancel.IsCancellationRequested))
                {
                    await Task.Yield();

                    Thread.SpinWait(rnd.Next(1, 1000));

                    if (cancel.IsCancellationRequested)
                    {
                        break;
                    }

                    values.Add(value);

                    var task = remoteInstance.SetValue(value);

                    await Task.WhenAny(cancelTask.Task, task).ConfigureAwait(false);
                }

                onFinish.TrySetResult(true);
            }
            catch (Exception error)
            {
                onFinish.TrySetException(error);
            }
        }

        [Test]
        public async Task StressTest()
        {
            var proxyGenerator = new DefaultProxyFactory();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(5)))
            using (var localConnectionLayer = new MockConnectionLayer())
            using (var remoteConnectionLayer = new MockConnectionLayer())
            using (var localChannel = new MockChannel(localConnectionLayer))
            using (var remoteChannel = new MockChannel(remoteConnectionLayer))
            {
                localConnectionLayer.Start(remoteChannel, cancel);
                remoteConnectionLayer.Start(localChannel, cancel);

                var localStart = localChannel.LocalStart(cancel, proxyGenerator,
                    typeof(DummyClass).GetConstructors().First(),
                    true);
                var remoteStart = remoteChannel.RemoteStart(cancel, proxyGenerator);

                await Task.WhenAll(localStart, remoteStart).ConfigureAwait(false);

                var localInstance = (DummyClass)localStart.Result;
                var remoteInstance = (DummyClass)remoteStart.Result.instance;

                localChannel.Instance = localInstance;
                localChannel.IsHost = true;
                localChannel.StartListening(cancel, localChannel.RemoteRequests);
                remoteChannel.Instance = remoteInstance;
                remoteChannel.IsHost = false;
                remoteChannel.StartListening(cancel, remoteChannel.RemoteRequests);

                //stress test
                const int threadCount = 10;
                var tasks = new TaskCompletionSource<bool>[threadCount];
                var values = new ConcurrentBag<Guid>();
                for (var index = 0; index < threadCount; index++)
                {
                    ThreadTest(cancel.Token, remoteInstance, values, tasks[index] = new TaskCompletionSource<bool>());
                }
                await Task.WhenAll(tasks.Select(item => item.Task)).ConfigureAwait(false);

                //validate state
                Assert.AreEqual(values.Count, localInstance.Values.Count);
                Assert.AreEqual(values.Count, values.Count(id => localInstance.Values.Contains(id)));
            }
        }

        #endregion

        [Test]
        public async Task IntegrationTestExceptionCall()
        {
            var proxyGenerator = new DefaultProxyFactory();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (var localConnectionLayer = new MockConnectionLayer())
            using (var remoteConnectionLayer = new MockConnectionLayer())
            using (var localChannel = new MockChannel(localConnectionLayer))
            using (var remoteChannel = new MockChannel(remoteConnectionLayer))
            {
                localConnectionLayer.Start(remoteChannel, cancel);
                remoteConnectionLayer.Start(localChannel, cancel);

                var localStart = localChannel.LocalStart(cancel, proxyGenerator,
                    typeof(DummyClass).GetConstructors().First(),
                    true);
                var remoteStart = remoteChannel.RemoteStart(cancel, proxyGenerator);

                await Task.WhenAll(localStart, remoteStart).ConfigureAwait(false);

                var localInstance = (DummyClass)localStart.Result;
                var remoteInstance = (DummyClass)remoteStart.Result.instance;

                localChannel.Instance = localInstance;
                localChannel.IsHost = true;
                localChannel.StartListening(cancel, localChannel.RemoteRequests);
                remoteChannel.Instance = remoteInstance;
                remoteChannel.IsHost = false;
                remoteChannel.StartListening(cancel, remoteChannel.RemoteRequests);

                //test exception case
                Assert.ThrowsAsync<ArgumentNullException>(() => remoteInstance.SetValue(null), ArgumentNullMsg);
                Assert.AreEqual(0, localInstance.Values.Count);
            }
        }

        [Test]
        public async Task IntegrationTestExceptionFreeCall()
        {
            var proxyGenerator = new DefaultProxyFactory();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (var localConnectionLayer = new MockConnectionLayer())
            using (var remoteConnectionLayer = new MockConnectionLayer())
            using (var localChannel = new MockChannel(localConnectionLayer))
            using (var remoteChannel = new MockChannel(remoteConnectionLayer))
            {
                localConnectionLayer.Start(remoteChannel, cancel);
                remoteConnectionLayer.Start(localChannel, cancel);

                var localStart = localChannel.LocalStart(cancel, proxyGenerator,
                    typeof(DummyClass).GetConstructors().First(),
                    true);
                var remoteStart = remoteChannel.RemoteStart(cancel, proxyGenerator);

                await Task.WhenAll(localStart, remoteStart).ConfigureAwait(false);

                var localInstance = (DummyClass)localStart.Result;
                var remoteInstance = (DummyClass)remoteStart.Result.instance;

                localChannel.Instance = localInstance;
                localChannel.IsHost = true;
                localChannel.StartListening(cancel, localChannel.RemoteRequests);
                remoteChannel.Instance = remoteInstance;
                remoteChannel.IsHost = false;
                remoteChannel.StartListening(cancel, remoteChannel.RemoteRequests);

                //test exception free case
                var value = Guid.NewGuid();
                await remoteInstance.SetValue(value).ConfigureAwait(false);
                Assert.AreEqual(1, localInstance.Values.Count);
                Assert.AreEqual(value, localInstance.Values.First());

                localInstance.Values.Clear();
            }
        }
    }
}
