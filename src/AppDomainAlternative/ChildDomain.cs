using System;
using System.Diagnostics;
using System.Threading;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative
{
    /// <inheritdoc cref="Domains"/>
    public sealed class ChildDomain : Domains, IDisposable
    {
        private int disposed;

        internal ChildDomain(Process childProcess, Connection connection)
        {
            Channels = connection;
            Process = childProcess;

            childProcess.Exited += (sender, eventArgs) => Dispose();
        }

        /// <inheritdoc />
        public override IHaveChannels Channels { get; }

        /// <summary>
        /// The process for the child.
        /// </summary>
        public override Process Process { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) != 0)
            {
                return;
            }

            using ((IDisposable)Channels)
            using (Process)
            {
                try
                {
                    Process.Kill();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
