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
    public class PrimitiveTypesTests
    {
        private async Task test<T>(DefaultSerializer serializer, MockResolveProxyIds resolver, params T[] values)
        {
            Assert.IsTrue(serializer.CanSerialize(typeof(T)));

            foreach (var value in values)
            {
                var stream = new MemoryStream();

                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    await serializer.Serialize(writer, typeof(T), value, resolver).ConfigureAwait(false);
                }

                Console.WriteLine($"Size of {value}: {stream.Length}");

                stream.Position = 0;

                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var deserializedValue = await serializer.Deserialize(reader, typeof(T), resolver, CancellationToken.None).ConfigureAwait(false);
                    Assert.AreEqual(value, deserializedValue);
                }
            }
        }

        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            //boolean
            await test(serializer, resolver, true, false).ConfigureAwait(false);

            //char
            await test(serializer, resolver, char.MinValue, char.MaxValue).ConfigureAwait(false);

            //signed integer types
            await test<sbyte>(serializer, resolver, sbyte.MinValue, -1, 0, 1, sbyte.MaxValue).ConfigureAwait(false);
            await test(serializer, resolver, int.MinValue, -1, 0, 1, int.MaxValue).ConfigureAwait(false);
            await test(serializer, resolver, long.MinValue, -1, 0, 1, long.MaxValue).ConfigureAwait(false);
            await test<short>(serializer, resolver, short.MinValue, -1, 0, 1, short.MaxValue).ConfigureAwait(false);

            //unsigned integer types
            await test<byte>(serializer, resolver, byte.MinValue, 1, byte.MaxValue).ConfigureAwait(false);
            await test(serializer, resolver, uint.MinValue, (uint)1, uint.MaxValue).ConfigureAwait(false);
            await test(serializer, resolver, ulong.MinValue, (ulong)1, ulong.MaxValue).ConfigureAwait(false);
            await test<ushort>(serializer, resolver, ushort.MinValue, 1, ushort.MaxValue).ConfigureAwait(false);

            //float point types
            await test(serializer, resolver, decimal.MinValue, -1, 0, 1, decimal.MaxValue).ConfigureAwait(false);
            await test(serializer, resolver, double.MinValue, -1, 0, 1, double.MaxValue).ConfigureAwait(false);
            await test(serializer, resolver, float.MinValue, -1, 0, 1, float.MaxValue).ConfigureAwait(false);
        }
    }
}
