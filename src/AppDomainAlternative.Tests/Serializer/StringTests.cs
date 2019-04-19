using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AppDomainAlternative.Serializer
{
    [TestFixture]
    public class StringTests
    {
        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            var stream = new MemoryStream();

            var str = new string(new[]
            {
                char.MinValue,

                //standard ASCII
                'A',
                'Z',
                'a',
                'z',

                //extended ASCII
                'À',
                'Ï',

                //unicode
                'Ā',
                'ď',

                char.MaxValue
            });

            using (var writer = new BinaryWriter(stream, Encoding.Unicode, true))
            {
                await serializer.Serialize(writer, typeof(string), str, resolver).ConfigureAwait(false);
            }

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.Unicode, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, typeof(string), resolver, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(deserializedValue, str);
            }
        }
    }
}
