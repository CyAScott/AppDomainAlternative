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
    public class GuidTests
    {
        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            Assert.IsTrue(serializer.CanSerialize(typeof(Guid)));

            var values = new[]
            {
                Guid.Empty, Guid.NewGuid(), Guid.NewGuid()
            };

            foreach (var value in values)
            {
                var stream = new MemoryStream();

                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    await serializer.Serialize(writer, typeof(Guid), value, resolver).ConfigureAwait(false);
                }

                Console.WriteLine($"Size of {value}: {stream.Length}");

                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var deserializedValue = await serializer.Deserialize(reader, typeof(Guid), resolver, CancellationToken.None).ConfigureAwait(false);
                    Assert.AreEqual(value, deserializedValue);
                }
            }
        }
    }
}
