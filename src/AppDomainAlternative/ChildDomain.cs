using System;
using System.Diagnostics;
using System.Threading;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative
{
    /// <inheritdoc cref="Domains"/>
    public sealed class ChildDomain : Domains, IDisposable, IDomains
    {
        Connection IDomains.Connection => Connection;
        private Connection Connection { get; }
        private int disposed;

        internal ChildDomain(Process childProcess, Connection connection)
        {
            childProcess.Exited += (sender, eventArgs) => Dispose();
            Process = childProcess;
            Connection = connection;
        }

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

            using (Connection)
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
