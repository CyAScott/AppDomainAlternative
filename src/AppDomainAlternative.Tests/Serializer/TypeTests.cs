using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Serializer
{
    [TestFixture]
    public class TypeTests
    {
        private async Task test(DefaultSerializer serializer, MockResolveProxyIds resolver, Type value)
        {
            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.Unicode, true))
            {
                await serializer.Serialize(writer, typeof(Type), value, resolver).ConfigureAwait(false);
            }

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.Unicode, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, typeof(Type), resolver, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(deserializedValue, value);
            }
        }

        public class SampleClass
        {
        }

        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            await test(serializer, resolver, typeof(DateTime)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(SampleClass)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(TimeSpan)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(Type)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(bool)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(byte)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(char)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(decimal)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(double)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(float)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(int)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(long)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(sbyte)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(short)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(string)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(uint)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(ulong)).ConfigureAwait(false);
            await test(serializer, resolver, typeof(ushort)).ConfigureAwait(false);
        }
    }
}
