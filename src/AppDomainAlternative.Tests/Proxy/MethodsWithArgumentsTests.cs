using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Proxy
{
    [TestFixture]
    public class MethodsWithArgumentsTests
    {
        public class SampleClass
        {
            public virtual Task AsyncMethod(int a, DateTime b, string c) => throw new NotSupportedException();
            public virtual void SyncMethod(int a, DateTime b, string c) => throw new NotSupportedException();
        }

        [Test]
        public async Task Test()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            var proxyInstance = (SampleClass)factory.GenerateProxy(interceptor, typeof(SampleClass).GetConstructor(Type.EmptyTypes));

            Assert.AreEqual(interceptor.Logs.Count, 0);

            foreach (var values in new[]
            {
                (a: int.MinValue, b: DateTime.MinValue, c:string.Empty),
                (a: 0, b: DateTime.UtcNow, c:(string)null),
                (a: int.MaxValue, b: DateTime.MaxValue, c:"Hello World")
            })
            {
                //SyncMethod
                proxyInstance.SyncMethod(values.a, values.b, values.c);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                var remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual(nameof(proxyInstance.SyncMethod), remoteInvoke.MethodName);
                Assert.AreEqual(typeof(object), remoteInvoke.ReturnType);
                Assert.AreEqual(3, remoteInvoke.Args.Length);
                Assert.AreEqual(typeof(int), remoteInvoke.Args[0].Item1);
                Assert.AreEqual(values.a, remoteInvoke.Args[0].Item2);
                Assert.AreEqual(typeof(DateTime), remoteInvoke.Args[1].Item1);
                Assert.AreEqual(values.b, remoteInvoke.Args[1].Item2);
                Assert.AreEqual(typeof(string), remoteInvoke.Args[2].Item1);
                Assert.AreEqual(values.c, remoteInvoke.Args[2].Item2);

                //AsyncMethod
                await proxyInstance.AsyncMethod(values.a, values.b, values.c).ConfigureAwait(false);

                Assert.AreEqual(interceptor.Logs.Count, 1);
                remoteInvoke = interceptor.Logs.Dequeue();
                Assert.AreEqual(false, remoteInvoke.FireAndForget);
                Assert.AreEqual(nameof(proxyInstance.AsyncMethod), remoteInvoke.MethodName);
                Assert.AreEqual(typeof(object), remoteInvoke.ReturnType);
                Assert.AreEqual(3, remoteInvoke.Args.Length);
                Assert.AreEqual(typeof(int), remoteInvoke.Args[0].Item1);
                Assert.AreEqual(values.a, remoteInvoke.Args[0].Item2);
                Assert.AreEqual(typeof(DateTime), remoteInvoke.Args[1].Item1);
                Assert.AreEqual(values.b, remoteInvoke.Args[1].Item2);
                Assert.AreEqual(typeof(string), remoteInvoke.Args[2].Item1);
                Assert.AreEqual(values.c, remoteInvoke.Args[2].Item2);
            }
        }
    }
}
