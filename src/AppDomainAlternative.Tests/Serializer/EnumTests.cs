using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Serializer
{
    [TestFixture]
    public class EnumTests
    {
        [Flags]
        public enum SampleEnum
        {
            One = 1,
            Two = 2,
            Three = One | Two,
            Four = 4,
            Five = One | Four,
            Six = Two | Four,
            Seven = One | Two | Four,
            Eight = 8
        }

        private async Task test(DefaultSerializer serializer, MockResolveProxyIds resolver, SampleEnum value)
        {
            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.Unicode, true))
            {
                await serializer.Serialize(writer, typeof(SampleEnum), value, resolver).ConfigureAwait(false);
            }

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.Unicode, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, typeof(SampleEnum), resolver, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(deserializedValue, value);
            }
        }

        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            foreach (var value in Enum.GetValues(typeof(SampleEnum)).Cast<SampleEnum>())
            {
                await test(serializer, resolver, value).ConfigureAwait(false);
            }
        }
    }
}
