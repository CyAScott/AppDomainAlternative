using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Serializer
{
    [TestFixture]
    public class NullTests
    {
        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.Unicode, true))
            {
                await serializer.Serialize(writer, typeof(object), null, resolver).ConfigureAwait(false);
            }

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.Unicode, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, typeof(object), resolver, CancellationToken.None).ConfigureAwait(false);
                Assert.IsNull(deserializedValue);
            }
        }
    }
}
