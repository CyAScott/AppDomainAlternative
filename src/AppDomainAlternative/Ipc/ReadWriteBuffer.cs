using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AppDomainAlternative.Ipc
{
    /// <summary>
    /// A FILO byte buffer.
    /// </summary>
    internal class ReadWriteBuffer : Stream
    {
        internal class ReadRequest : TaskCompletionSource<int>, IAsyncResult
        {
            WaitHandle IAsyncResult.AsyncWaitHandle => ((IAsyncResult)Task).AsyncWaitHandle;
            bool IAsyncResult.CompletedSynchronously => ((IAsyncResult)Task).CompletedSynchronously;
            bool IAsyncResult.IsCompleted => Task.IsCompleted;
            object IAsyncResult.AsyncState => Task.AsyncState;

            public ReadRequest(long id, ArraySegment<byte> buffer, AsyncCallback callback, object state)
                : base(state)
            {
                if (callback != null)
                {
                    Task.ContinueWith(_ => callback(this));
                }
                Buffer = buffer;
                Id = id;
            }
            public ArraySegment<byte> Buffer { get; }
            public long Id { get; }
            public int ReadBytes { get; set; }
        }

        private ReadRequest readRequest;
        private byte[] buffer = new byte[pageSize];
        private const int pageSize = 4096;
        private int
            disposed,
            readIndex, //the reader's index position in the local buffer. readIndex is always <= writeIndex
            writeIndex;//the writer's index position in the local buffer
        private readonly SemaphoreSlim readWriteLock = new SemaphoreSlim(1, 1);//a read/write lock for the local buffer
        private void completeReadRequest()
        {
            var request = readRequest;
            readRequest = null;
            request.TrySetResult(request.ReadBytes);
        }
        private void copyFromLocalBufferTo(bool completeIfNotFinished)
        {
            if (readRequest == null)
            {
                return;
            }

            //if the destination buffer is full then
            if (readRequest.ReadBytes >= readRequest.Buffer.Count)
            {
                //set the request as completed
                completeReadRequest();

                return;
            }

            //skip if there is nothing in the local buffer
            if (readIndex == writeIndex)
            {
                return;
            }

            //copy unread bytes in the local buffer to the request's buffer

            var copyLength = Math.Min(writeIndex - readIndex, readRequest.Buffer.Count);

            Buffer.BlockCopy(
                buffer, readIndex,
                // ReSharper disable once AssignNullToNotNullAttribute
                readRequest.Buffer.Array, readRequest.Buffer.Offset, copyLength);

            readRequest.ReadBytes += copyLength;
            readIndex += copyLength;

            if (completeIfNotFinished || readRequest.ReadBytes >= readRequest.Buffer.Count)
            {
                //set the request as completed
                completeReadRequest();
            }

            flushLocalBuffer();
        }
        private void flushLocalBuffer()
        {
            if (readIndex == 0)
            {
                return;
            }

            //if there is no unread data in the local buffer then
            if (readIndex == writeIndex)
            {
                //reset all indices to 0
                readIndex = writeIndex = 0;
            }
            else
            {
                //else move the unread bytes to the beginning of the buffer

                Buffer.BlockCopy(buffer, readIndex, buffer, 0, writeIndex -= readIndex);

                readIndex = 0;
            }
        }
        private void throwIfDisposed()
        {
            if (disposed > 0)
            {
                throw new ObjectDisposedException(nameof(ReadWriteBuffer));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
            {
                return;
            }

            readRequest?.TrySetCanceled();
            readWriteLock?.Dispose();
        }

        public ReadWriteBuffer(long id) => Id = id;
        public async Task Fill(Stream reader, int bytesToRead, CancellationToken token)
        {
            throwIfDisposed();

            while (bytesToRead > 0)
            {
                await readWriteLock.WaitAsync(token).ConfigureAwait(false);

                copyFromLocalBufferTo(false);

                try
                {
                    //if there is no request waiting for data then
                    if (readRequest == null)
                    {
                        //copy the data into the local buffer to be read later

                        var count = await reader.ReadAsync(buffer, writeIndex, Math.Min(buffer.Length - writeIndex, bytesToRead), token).ConfigureAwait(false);

                        bytesToRead -= count;
                        writeIndex += count;

                        //if the local buffer is full then
                        if (writeIndex == buffer.Length)
                        {
                            //increase the local buffer size by one page
                            Array.Resize(ref buffer, buffer.Length + pageSize);
                        }
                    }
                    else
                    {
                        //copy the data directly into the request's buffer

                        var count = await reader.ReadAsync(
                            // ReSharper disable once AssignNullToNotNullAttribute
                            readRequest.Buffer.Array,
                            readRequest.Buffer.Offset + readRequest.ReadBytes,
                            Math.Min(bytesToRead, readRequest.Buffer.Count - readRequest.ReadBytes), token).ConfigureAwait(false);

                        readRequest.ReadBytes += count;
                        bytesToRead -= count;

                        //set the request as completed
                        completeReadRequest();
                    }
                }
                finally
                {
                    readWriteLock.Release();
                }
            }
        }
        public long Id { get; }
        public override IAsyncResult BeginRead(byte[] data, int offset, int count, AsyncCallback callback, object state)
        {
            throwIfDisposed();

            var request = new ReadRequest(Id, new ArraySegment<byte>(data, offset, count), callback, state);

            if (count == 0)
            {
                request.TrySetResult(0);
                return request;
            }

            readWriteLock.Wait();

            try
            {
                if (readRequest != null)
                {
                    throw new InvalidOperationException("A read is already in progress.");
                }
                readRequest = request;
                copyFromLocalBufferTo(true);
            }
            finally
            {
                readWriteLock.Release();
            }

            return request;
        }
        public override IAsyncResult BeginWrite(byte[] data, int offset, int count, AsyncCallback callback, object state) => throw new NotSupportedException();
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override bool CanRead => disposed == 0;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite { get; } = false;
        public override int EndRead(IAsyncResult asyncResult)
        {
            throwIfDisposed();

            if (!(asyncResult is ReadRequest result) || result.Id != Id)
            {
                throw new InvalidCastException();
            }

            return result.Task.Result;
        }
        public override int Read(byte[] data, int offset, int count)
        {
            throwIfDisposed();
            return ((ReadRequest)BeginRead(data, offset, count, null, null)).Task.Result;
        }
        public override long Length => writeIndex - readIndex;
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void EndWrite(IAsyncResult asyncResult) => throw new NotSupportedException();
        public override void Flush()
        {
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] data, int offset, int count) => throw new NotSupportedException();
    }
}
