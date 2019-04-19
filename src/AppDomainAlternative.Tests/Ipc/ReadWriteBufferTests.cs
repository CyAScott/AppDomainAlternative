using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

// ReSharper disable AccessToDisposedClosure

namespace AppDomainAlternative.Ipc
{
    [TestFixture]
    public class ReadWriteBufferTests
    {
        [Test]
        public void MultipleReadAttemptsTest()
        {
            //test that multiple attempts to read when the first read operation has not finished triggers an error

            var buffer = new ReadWriteBuffer(0);

            var writeBuffer = Guid.NewGuid().ToByteArray();
            var readBuffer = new byte[writeBuffer.Length];

            buffer.ReadAsync(readBuffer, 0, readBuffer.Length);

            Assert.Catch<InvalidOperationException>(() => buffer.ReadAsync(readBuffer, 0, readBuffer.Length));
        }

        [Test]
        public async Task ReadThenWriteTest()
        {
            var buffer = new ReadWriteBuffer(0);

            var writeBuffer = Guid.NewGuid().ToByteArray();
            var readBuffer = new byte[writeBuffer.Length];

            var read = buffer.ReadAsync(readBuffer, 0, readBuffer.Length);

            await buffer.Fill(new MemoryStream(writeBuffer), writeBuffer.Length, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(readBuffer.Length, await read.ConfigureAwait(false));
            Assert.AreEqual(new Guid(writeBuffer), new Guid(readBuffer));
        }

        [Test]
        public async Task StressTest()
        {
            var randomData = new byte[1_048_576];//1 MB
            var readData = new byte[randomData.Length];
            var rnd = new Random(846845445);

            rnd.NextBytes(randomData);

            foreach (var __ in Enumerable.Range(0, 100))
            {
                using (var buffer = new ReadWriteBuffer(0))
                {
                    var readTask = new TaskCompletionSource<object>();
                    var timer = new Stopwatch();
                    var writeTask = new TaskCompletionSource<object>();

                    //write thread
                    ThreadPool.QueueUserWorkItem(async _ =>
                    {
                        try
                        {
                            var writeIndex = 0;

                            timer.Start();

                            while (writeIndex < randomData.Length)
                            {
                                var writeLength = Math.Min(rnd.Next(1, 1000), randomData.Length - writeIndex);
                                await buffer.Fill(new MemoryStream(randomData, writeIndex, writeLength, false), writeLength, CancellationToken.None).ConfigureAwait(false);
                                Thread.SpinWait(rnd.Next(1, 1000));
                                writeIndex += writeLength;
                            }

                            writeTask.TrySetResult(null);
                        }
                        catch (Exception error)
                        {
                            writeTask.TrySetException(error);
                        }
                    });

                    //read thread
                    ThreadPool.QueueUserWorkItem(async _ =>
                    {
                        try
                        {
                            var readIndex = 0;
                            while (readIndex < randomData.Length)
                            {
                                var readLength = await buffer.ReadAsync(readData, readIndex, Math.Min(rnd.Next(1, 1000), readData.Length - readIndex)).ConfigureAwait(false);
                                Thread.SpinWait(rnd.Next(1, 1000));
                                readIndex += readLength;
                            }

                            timer.Stop();

                            readTask.TrySetResult(null);
                        }
                        catch (Exception error)
                        {
                            readTask.TrySetException(error);
                        }
                    });

                    await Task.WhenAll(readTask.Task, writeTask.Task).ConfigureAwait(false);

                    Console.WriteLine($"Time: {timer.Elapsed.TotalMilliseconds:0.00} ms");

                    using (var md5 = MD5.Create())
                    {
                        var randomDataHash = new Guid(md5.ComputeHash(randomData));
                        var readDataHash = new Guid(md5.ComputeHash(readData));

                        Console.WriteLine($"random data ({randomDataHash}) == read data ({readDataHash})");

                        Assert.AreEqual(randomDataHash, readDataHash);
                    }
                }
            }
        }

        [Test]
        public async Task WriteThenReadTest()
        {
            var buffer = new ReadWriteBuffer(0);

            var writeBuffer = Guid.NewGuid().ToByteArray();
            var readBuffer = new byte[writeBuffer.Length];

            await buffer.Fill(new MemoryStream(writeBuffer), writeBuffer.Length, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(readBuffer.Length, await buffer.ReadAsync(readBuffer, 0, readBuffer.Length).ConfigureAwait(false));
            Assert.AreEqual(new Guid(writeBuffer), new Guid(readBuffer));
        }
    }
}
