using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Proxy
{
    [TestFixture]
    public class PropertiesTests
    {
        public class SampleClass
        {
            public virtual Task<int> AsyncGetProperty { get; } = null;
            //NOTE: Task<> for set property methods is not supported since Task<> cannot be serialized

            public virtual int SyncGetProperty { get; } = 0;
            public virtual int SyncGetSetProperty { get; set; }
            public virtual int SyncSetProperty { set => throw new NotSupportedException(); }
        }

        [Test]
        public async Task Test()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            var proxyInstance = (SampleClass)factory.GenerateProxy(interceptor, typeof(SampleClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            foreach (var value in new [] { int.MinValue, 1, int.MaxValue })
            {
                //SyncGetProperty
                interceptor.RemoteInvokeReturnValue = value;
                var returnValue = proxyInstance.SyncGetProperty;
                Assert.AreEqual(value, returnValue);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                var remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual($"get_{nameof(proxyInstance.SyncGetProperty)}", remoteInvoke.MethodName);
                Assert.AreEqual(typeof(int), remoteInvoke.ReturnType);
                Assert.AreEqual(0, remoteInvoke.Args.Length);

                //AsyncGetProperty
                interceptor.RemoteInvokeReturnValue = value;
                returnValue = await proxyInstance.AsyncGetProperty.ConfigureAwait(false);
                Assert.AreEqual(value, returnValue);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual($"get_{nameof(proxyInstance.AsyncGetProperty)}", remoteInvoke.MethodName);
                Assert.AreEqual(typeof(int), remoteInvoke.ReturnType);
                Assert.AreEqual(0, remoteInvoke.Args.Length);

                //SyncSetProperty
                proxyInstance.SyncSetProperty = value;

                Assert.AreEqual(interceptor.Logs.Count, 1);
                remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual($"set_{nameof(proxyInstance.SyncSetProperty)}", remoteInvoke.MethodName);
                Assert.AreEqual(typeof(object), remoteInvoke.ReturnType);
                Assert.AreEqual(1, remoteInvoke.Args.Length);
                Assert.AreEqual(typeof(int), remoteInvoke.Args[0].Item1);
                Assert.AreEqual(value, remoteInvoke.Args[0].Item2);

                //SyncGetSetProperty - get
                interceptor.RemoteInvokeReturnValue = value;
                returnValue = proxyInstance.SyncGetSetProperty;
                Assert.AreEqual(value, returnValue);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual($"get_{nameof(proxyInstance.SyncGetSetProperty)}", remoteInvoke.MethodName);
                Assert.AreEqual(typeof(int), remoteInvoke.ReturnType);
                Assert.AreEqual(0, remoteInvoke.Args.Length);

                //SyncGetSetProperty - set
                proxyInstance.SyncGetSetProperty = value;

                Assert.AreEqual(interceptor.Logs.Count, 1);
                remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual($"set_{nameof(proxyInstance.SyncGetSetProperty)}", remoteInvoke.MethodName);
                Assert.AreEqual(typeof(object), remoteInvoke.ReturnType);
                Assert.AreEqual(1, remoteInvoke.Args.Length);
                Assert.AreEqual(typeof(int), remoteInvoke.Args[0].Item1);
                Assert.AreEqual(value, remoteInvoke.Args[0].Item2);
            }
        }
    }
}
