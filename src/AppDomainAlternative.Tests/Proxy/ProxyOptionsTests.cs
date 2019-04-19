using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Proxy
{
    [TestFixture]
    public class ProxyOptionsTests
    {
        public class TestEnabledClass
        {
            public virtual void EnabledMethod() => throw new NotSupportedException();

            [Proxy(Enabled = false)]
            public virtual void DisabledMethod() => throw new NotSupportedException();
        }
        [Test]
        public void TestEnabled()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            var proxyInstance = (TestEnabledClass)factory.GenerateProxy(interceptor, typeof(TestEnabledClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            Assert.Catch<NotSupportedException>(proxyInstance.DisabledMethod);

            Assert.AreEqual(interceptor.Logs.Count, 0);
        }

        public class TestFireAndForgetClass
        {
            [Proxy(FireAndForget = true)]
            public virtual Task<int> FireAndForgetReturnTaskValueMethod() => throw new NotSupportedException();

            [Proxy(FireAndForget = true)]
            public virtual Task FireAndForgetTaskMethod() => throw new NotSupportedException();

            [Proxy(FireAndForget = true)]
            public virtual int FireAndForgetReturnValueMethod() => throw new NotSupportedException();

            [Proxy(FireAndForget = true)]
            public virtual void FireAndForgetVoidMethod() => throw new NotSupportedException();
        }
        [Test]
        public async Task TestFireAndForget()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            var proxyInstance = (TestFireAndForgetClass)factory.GenerateProxy(interceptor, typeof(TestFireAndForgetClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            interceptor.RemoteInvokeReturnValue = 0;
            await proxyInstance.FireAndForgetReturnTaskValueMethod().ConfigureAwait(false);
            Assert.AreEqual(interceptor.Logs.Count, 1);
            var remoteInvoke = interceptor.Logs.Dequeue();
            Assert.AreEqual(remoteInvoke.FireAndForget, false);

            interceptor.RemoteInvokeReturnValue = null;
            await proxyInstance.FireAndForgetTaskMethod().ConfigureAwait(false);
            Assert.AreEqual(interceptor.Logs.Count, 1);
            remoteInvoke = interceptor.Logs.Dequeue();
            Assert.AreEqual(remoteInvoke.FireAndForget, true);

            interceptor.RemoteInvokeReturnValue = 1;
            proxyInstance.FireAndForgetReturnValueMethod();
            Assert.AreEqual(interceptor.Logs.Count, 1);
            remoteInvoke = interceptor.Logs.Dequeue();
            Assert.AreEqual(remoteInvoke.FireAndForget, false);

            interceptor.RemoteInvokeReturnValue = null;
            proxyInstance.FireAndForgetVoidMethod();
            Assert.AreEqual(interceptor.Logs.Count, 1);
            remoteInvoke = interceptor.Logs.Dequeue();
            Assert.AreEqual(remoteInvoke.FireAndForget, true);
        }

        public class TestInvalidTypeHandlingIgnoreClass
        {
            public virtual void EnabledMethod() => throw new NotSupportedException();

            [Proxy(InvalidTypeHandling = InvalidTypeHandling.Ignore)]
            public virtual int InvalidTypeHandlingMethod() => throw new NotSupportedException();
        }
        [Test]
        public void TestInvalidTypeHandlingIgnore()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            interceptor.MockSerializer.CanSerializeInterceptor = _ => false;

            var proxyInstance = (TestInvalidTypeHandlingIgnoreClass)factory.GenerateProxy(interceptor, typeof(TestInvalidTypeHandlingIgnoreClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            Assert.Catch<NotSupportedException>(() => proxyInstance.InvalidTypeHandlingMethod());

            Assert.AreEqual(interceptor.Logs.Count, 0);
        }

        public class TestInvalidTypeHandlingThrowErrorOnCreateClass
        {
            public virtual void EnabledMethod() => throw new NotSupportedException();

            [Proxy(InvalidTypeHandling = InvalidTypeHandling.ThrowErrorOnCreate)]
            public virtual int InvalidTypeHandlingMethod() => throw new NotSupportedException();
        }
        [Test]
        public void TestInvalidTypeHandlingThrowErrorOnCreate()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            interceptor.MockSerializer.CanSerializeInterceptor = _ => false;

            Assert.Catch<ArgumentException>(() => factory.GenerateProxy(interceptor, typeof(TestInvalidTypeHandlingThrowErrorOnCreateClass).GetConstructor(Type.EmptyTypes)));
        }

        public class TestInvalidTypeHandlingThrowErrorOnInvokeClass
        {
            [Proxy(InvalidTypeHandling = InvalidTypeHandling.ThrowErrorOnInvoke)]
            public virtual int InvalidTypeHandlingMethod() => throw new NotSupportedException();
        }
        [Test]
        public void TestInvalidTypeHandlingThrowErrorOnInvoke()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            interceptor.MockSerializer.CanSerializeInterceptor = _ => false;

            var proxyInstance = (TestInvalidTypeHandlingThrowErrorOnInvokeClass)factory.GenerateProxy(interceptor, typeof(TestInvalidTypeHandlingThrowErrorOnInvokeClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            Assert.Catch<NotSupportedException>(() => proxyInstance.InvalidTypeHandlingMethod());

            Assert.AreEqual(interceptor.Logs.Count, 0);
        }
    }
}
