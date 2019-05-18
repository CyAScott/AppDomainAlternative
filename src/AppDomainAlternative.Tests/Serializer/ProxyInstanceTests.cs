using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Serializer.Default;
using NUnit.Framework;

namespace AppDomainAlternative.Serializer
{
    [TestFixture]
    public class ProxyInstanceTests
    {
        private async Task test(DefaultSerializer serializer, MockResolveProxyIds resolver, object value)
        {
            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                await serializer.Serialize(writer, typeof(object), value, resolver).ConfigureAwait(false);
            }

            Console.WriteLine($"Size of {value}: {stream.Length}");

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, typeof(object), resolver, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(deserializedValue, value);
            }
        }

        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            var min = new object();
            resolver.Instances[long.MinValue] = min;

            var zero = new object();
            resolver.Instances[0] = zero;

            var max = new object();
            resolver.Instances[long.MaxValue] = max;

            await test(serializer, resolver, min).ConfigureAwait(false);
            await test(serializer, resolver, zero).ConfigureAwait(false);
            await test(serializer, resolver, max).ConfigureAwait(false);
        }
    }
}
