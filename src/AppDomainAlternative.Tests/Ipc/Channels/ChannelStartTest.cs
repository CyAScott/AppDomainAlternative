using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Extensions;
using AppDomainAlternative.Proxy;
using NUnit.Framework;

// ReSharper disable AccessToModifiedClosure

namespace AppDomainAlternative.Ipc.Channels
{
    [TestFixture]
    public class ChannelStartTest
    {
        private static async Task test(bool hostInstance, params object[] arguments)
        {
            var proxyGenerator = new DefaultProxyFactory();

            using (var cancel = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes(1)))
            using (var localChannel = new MockChannel())
            using (var remoteChannel = new MockChannel())
            {
                var writeTask = new TaskCompletionSource<(long id, Stream data)>();
                cancel.Token.Register(() => writeTask.TrySetCanceled());
                localChannel.MockConnection.Writes += (id, data) => writeTask.TrySetResult((id, data));
                remoteChannel.MockConnection.Writes += (id, data) => writeTask.TrySetResult((id, data));

                //make a local start on another thread
                var localStart = localChannel.LocalStart(cancel, proxyGenerator,
                    typeof(DummyClass).GetConstructors()
                        .FirstOrDefault(ctor => ctor.GetParameters().Length == arguments.Length) ??
                    typeof(DummyClass).GetConstructors()
                        .First(ctor => ctor.GetParameters().Length != arguments.Length),
                    hostInstance,
                    arguments);
                if (localStart.IsCompleted)
                {
                    //this usually means an error happened so lets throw it now
                    await localStart.ConfigureAwait(false);
                }

                //wait for local start to start the waiting step for a response
                await writeTask.Task.ConfigureAwait(false);

                //read the write request from the local channel
                var writeRequest = writeTask.Task.Result;
                Assert.AreEqual(localChannel.Id, writeRequest.id);
                Assert.IsNotNull(writeRequest.data);
                writeTask = new TaskCompletionSource<(long id, Stream data)>();

                //send the request to the remote channel
                await remoteChannel.Buffer.Fill(writeRequest.data, (int)writeRequest.data.Length, cancel.Token).ConfigureAwait(false);

                var remoteStart = await remoteChannel.RemoteStart(cancel, proxyGenerator).ConfigureAwait(false);
                await writeTask.Task.ConfigureAwait(false);

                //read the write request from the remote channel
                writeRequest = writeTask.Task.Result;
                Assert.AreEqual(remoteChannel.Id, writeRequest.id);
                Assert.IsNotNull(writeRequest.data);

                //send the request to the local channel
                await localChannel.Buffer.Fill(writeRequest.data, (int)writeRequest.data.Length, cancel.Token).ConfigureAwait(false);

                //wait for all start tasks to finish
                await localStart.ConfigureAwait(false);

                //validate local instance
                var localInstance = localStart.Result as DummyClass;
                Assert.IsNotNull(localInstance);

                Assert.AreEqual(localInstance.Arg1, arguments.Skip(0).FirstOrDefault());
                Assert.AreEqual(localInstance.Arg2, arguments.Skip(1).FirstOrDefault());
                Assert.AreEqual(localInstance.Arg3, arguments.Skip(2).FirstOrDefault());

                //validate remote instance
                var remoteInstance = remoteStart.instance as DummyClass;
                Assert.IsNotNull(remoteInstance);

                Assert.AreEqual(remoteInstance.Arg1, arguments.Skip(0).FirstOrDefault());
                Assert.AreEqual(remoteInstance.Arg2, arguments.Skip(1).FirstOrDefault());
                Assert.AreEqual(remoteInstance.Arg3, arguments.Skip(2).FirstOrDefault());

                //validate proxy was generated correctly
                if (hostInstance)
                {
                    Assert.IsFalse(remoteStart.isHost);

                    Assert.AreEqual(typeof(DummyClass), localInstance.GetType());
                    Assert.AreNotEqual(typeof(DummyClass), remoteInstance.GetType());
                }
                else
                {
                    Assert.IsTrue(remoteStart.isHost);

                    Assert.AreNotEqual(typeof(DummyClass), localInstance.GetType());
                    Assert.AreEqual(typeof(DummyClass), remoteInstance.GetType());
                }
            }
        }

        public class DummyClass
        {
            public DummyClass()
            {
            }
            public DummyClass(Guid arg1) => Arg1 = arg1;
            public DummyClass(Guid arg1, DateTime arg2)
            {
                Arg1 = arg1;
                Arg2 = arg2;
            }
            public DummyClass(Guid arg1, DateTime arg2, int arg3)
            {
                Arg1 = arg1;
                Arg2 = arg2;
                Arg3 = arg3;
            }

            public readonly Guid? Arg1;
            public readonly DateTime? Arg2;
            public readonly int? Arg3;
            public virtual void EnabledMethod() => throw new NotSupportedException();
        }

        [Test]
        public async Task IntegrationTest()
        {
            await test(true,
                Guid.NewGuid()).ConfigureAwait(false);

            await test(true,
                Guid.NewGuid(),
                DateTime.UtcNow).ConfigureAwait(false);

            await test(true,
                Guid.NewGuid(),
                DateTime.UtcNow,
                13).ConfigureAwait(false);

            await test(false,
                Guid.NewGuid()).ConfigureAwait(false);

            await test(false,
                Guid.NewGuid(),
                DateTime.UtcNow).ConfigureAwait(false);

            await test(false,
                Guid.NewGuid(),
                DateTime.UtcNow,
                17).ConfigureAwait(false);
        }
    }
}
