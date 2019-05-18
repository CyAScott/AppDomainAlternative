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
    public class TimeSpanTests
    {
        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            Assert.IsTrue(serializer.CanSerialize(typeof(TimeSpan)));

            var values = new[]
            {
                TimeSpan.MinValue, TimeSpan.Zero, TimeSpan.MaxValue
            };

            foreach (var value in values)
            {
                var stream = new MemoryStream();

                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    await serializer.Serialize(writer, typeof(TimeSpan), value, resolver).ConfigureAwait(false);
                }

                Console.WriteLine($"Size of {value}: {stream.Length}");

                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var deserializedValue = await serializer.Deserialize(reader, typeof(TimeSpan), resolver, CancellationToken.None).ConfigureAwait(false);
                    Assert.AreEqual(value, deserializedValue);
                }
            }
        }
    }
}
