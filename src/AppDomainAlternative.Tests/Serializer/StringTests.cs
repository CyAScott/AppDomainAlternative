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

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                await serializer.Serialize(writer, typeof(string), str, resolver).ConfigureAwait(false);
            }

            Console.WriteLine($"Size of \"{str}\": {stream.Length}");

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, typeof(string), resolver, CancellationToken.None).ConfigureAwait(false);
                Assert.AreEqual(deserializedValue, str);
            }
        }
    }
}
