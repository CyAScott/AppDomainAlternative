using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Proxy
{
    [TestFixture]
    public class VoidMethodsTests
    {
        public class SampleClass
        {
            public virtual Task AsyncMethod() => throw new NotSupportedException();
            public virtual void SyncMethod() => throw new NotSupportedException();
        }

        [Test]
        public async Task Test()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            var proxyInstance = (SampleClass)factory.GenerateProxy(interceptor, typeof(SampleClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            //SyncMethod
            proxyInstance.SyncMethod();

            Assert.AreEqual(interceptor.Logs.Count, 1);
            var remoteInvoke = interceptor.Logs.Dequeue();
            Assert.AreEqual(false, remoteInvoke.FireAndForget);
            Assert.AreEqual(nameof(proxyInstance.SyncMethod), remoteInvoke.MethodName);
            Assert.AreEqual(typeof(object), remoteInvoke.ReturnType);
            Assert.AreEqual(0, remoteInvoke.Args.Length);

            //AsyncMethod
            await proxyInstance.AsyncMethod().ConfigureAwait(false);

            Assert.AreEqual(interceptor.Logs.Count, 1);
            remoteInvoke = interceptor.Logs.Dequeue();
            Assert.AreEqual(false, remoteInvoke.FireAndForget);
            Assert.AreEqual(nameof(proxyInstance.AsyncMethod), remoteInvoke.MethodName);
            Assert.AreEqual(typeof(object), remoteInvoke.ReturnType);
            Assert.AreEqual(0, remoteInvoke.Args.Length);
        }
    }
}
