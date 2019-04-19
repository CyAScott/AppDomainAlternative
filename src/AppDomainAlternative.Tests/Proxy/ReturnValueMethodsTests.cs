using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Proxy
{
    [TestFixture]
    public class ReturnValueMethodsTests
    {
        public class SampleClass<T>
        {
            public virtual Task<T> AsyncMethod() => throw new NotSupportedException();
            public virtual T SyncMethod() => throw new NotSupportedException();
        }

        private async Task test<T>(DefaultProxyFactory factory, MockInterceptor interceptor, params T[] values)
        {
            var proxyInstance = (SampleClass<T>)factory.GenerateProxy(interceptor, typeof(SampleClass<T>).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            foreach (var value in values)
            {
                //SyncMethod
                interceptor.RemoteInvokeReturnValue = value;
                var returnValue = proxyInstance.SyncMethod();
                Assert.AreEqual(value, returnValue);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                var remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual(nameof(proxyInstance.SyncMethod), remoteInvoke.MethodName);
                Assert.AreEqual(typeof(T), remoteInvoke.ReturnType);
                Assert.AreEqual(0, remoteInvoke.Args.Length);

                //AsyncMethod
                interceptor.RemoteInvokeReturnValue = value;
                returnValue = await proxyInstance.AsyncMethod().ConfigureAwait(false);
                Assert.AreEqual(value, returnValue);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual(nameof(proxyInstance.AsyncMethod), remoteInvoke.MethodName);
                Assert.AreEqual(typeof(T), remoteInvoke.ReturnType);
                Assert.AreEqual(0, remoteInvoke.Args.Length);
            }
        }

        [Test]
        public async Task Test()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            //test primitive
            await test(factory, interceptor, int.MinValue, 1, int.MaxValue).ConfigureAwait(false);

            //test struct
            await test(factory, interceptor, DateTime.MinValue, DateTime.UtcNow, DateTime.MaxValue).ConfigureAwait(false);
        }
    }
}
