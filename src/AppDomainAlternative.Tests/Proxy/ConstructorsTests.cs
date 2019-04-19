using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AppDomainAlternative.Proxy
{
    [TestFixture]
    public class ConstructorsTests
    {
        public class SampleClass
        {
            public SampleClass(int a, DateTime b, string c)
            {
                CtorArgs = 3;

                A = a;
                B = b;
                C = c;
            }
            public SampleClass(int a, DateTime b)
            {
                CtorArgs = 2;

                A = a;
                B = b;
                C = null;
            }
            public SampleClass(int a)
            {
                CtorArgs = 1;

                A = a;
                B = default(DateTime);
                C = null;
            }

            public readonly int CtorArgs;

            public readonly int A;
            public readonly DateTime B;
            public readonly string C;

            public virtual void SyncMethod(int a, DateTime b, string c) => throw new NotSupportedException();
        }

        private void test(DefaultProxyFactory factory, MockInterceptor interceptor, ConstructorInfo ctor, params object[] arguments)
        {
            var proxyInstance = (SampleClass)factory.GenerateProxy(interceptor, ctor, arguments);

            Assert.AreEqual(interceptor.Logs.Count, 0);

            Assert.AreEqual(arguments.Length, proxyInstance.CtorArgs);

            Assert.AreEqual(arguments[0], proxyInstance.A);
            Assert.AreEqual(arguments.Length > 1 ? arguments[1] : default(DateTime), proxyInstance.B);
            Assert.AreEqual(arguments.Length > 2 ? arguments[2] : default(string), proxyInstance.C);
        }

        [Test]
        public void Test()
        {
            var factory = new DefaultProxyFactory();
            var interceptor = new MockInterceptor();

            var constructors = typeof(SampleClass).GetConstructors();

            test(factory, interceptor, constructors.First(ctor => ctor.GetParameters().Length == 3),
                int.MinValue, DateTime.UtcNow, "Hello World");

            test(factory, interceptor, constructors.First(ctor => ctor.GetParameters().Length == 2),
                int.MinValue, DateTime.UtcNow);

            test(factory, interceptor, constructors.First(ctor => ctor.GetParameters().Length == 1),
                int.MinValue);
        }
    }
}
