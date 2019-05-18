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
    public class ArrayTests
    {
        private async Task test(DefaultSerializer serializer, MockResolveProxyIds resolver, Array value)
        {
            var arrayType = value.GetType();

            Assert.IsTrue(serializer.CanSerialize(arrayType));

            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                await serializer.Serialize(writer, arrayType, value, resolver).ConfigureAwait(false);
            }

            Console.WriteLine($"Size of {value}: {stream.Length}");

            stream.Position = 0;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var deserializedValue = await serializer.Deserialize(reader, arrayType, resolver, CancellationToken.None).ConfigureAwait(false) as Array;

                Assert.IsNotNull(deserializedValue);
                Assert.AreEqual(value.Rank, deserializedValue.Rank, "Ranks don't match.");
                for (var dimension = 0; dimension < value.Rank; dimension++)
                {
                    Assert.AreEqual(value.GetLength(dimension), deserializedValue.GetLength(dimension), $"Lengths ({dimension}) don't match.");
                    Assert.AreEqual(value.GetLowerBound(dimension), deserializedValue.GetLowerBound(dimension), $"Lower bounds ({dimension}) don't match.");
                    Assert.AreEqual(value.GetUpperBound(dimension), deserializedValue.GetUpperBound(dimension), $"Upper bounds ({dimension}) don't match.");
                }

                var enumerator = value.GetEnumerator();
                var deserializedEnumerator = deserializedValue.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    Assert.IsTrue(deserializedEnumerator.MoveNext());
                    Assert.AreEqual(enumerator.Current, deserializedEnumerator.Current);
                }
                Assert.IsFalse(deserializedEnumerator.MoveNext());
            }
        }

        [Test]
        public async Task Test()
        {
            var resolver = new MockResolveProxyIds();
            var serializer = new DefaultSerializer();

            //single dimension arrays (primitive, nullable, structs, and objects)
            await test(serializer, resolver, new[] { int.MinValue, -1, 0, 1, int.MaxValue }).ConfigureAwait(false);
            await test(serializer, resolver, new int?[] { int.MinValue, -1, 0, null, 1, int.MaxValue }).ConfigureAwait(false);
            await test(serializer, resolver, new[] { DateTime.MinValue, DateTime.Today, DateTime.Now, DateTime.UtcNow, DateTime.MaxValue }).ConfigureAwait(false);
            await test(serializer, resolver, new object[] { int.MinValue, DateTime.UtcNow, Guid.NewGuid(), TimeSpan.MinValue }).ConfigureAwait(false);

            //jagged array
            await test(serializer, resolver, new[]
            {
                new[] { int.MinValue, int.MaxValue },
                new[] { -1, 1 },
                new[] { 0 }
            }).ConfigureAwait(false);

            //multi dimensional arrays
            await test(serializer, resolver, new[,] { { 1, 2 }, { 3, 4 }, { 5, 6 }, { 7, 8 } }).ConfigureAwait(false);

            //array with lower bounds
            var array = Array.CreateInstance(typeof(Guid),
                new [] { 1, 2 },//1 columns, 2 rows
                new [] { 10, 11});//10 lower bound column, 11 lower bound row
            array.SetValue(Guid.NewGuid(), 10, 11);
            array.SetValue(Guid.NewGuid(), 10, 12);
            await test(serializer, resolver, array).ConfigureAwait(false);
        }
    }
}
