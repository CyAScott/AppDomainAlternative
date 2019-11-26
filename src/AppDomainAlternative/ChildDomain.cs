using System;
using System.Diagnostics;
using System.Threading;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative
{
    /// <inheritdoc cref="Domains"/>
    public sealed class ChildDomain : Domains, IDisposable
    {
        private readonly bool debuggerEnabled;
        private readonly int pid;
        private int disposed;

        internal ChildDomain(Process childProcess, bool debuggerEnabled, Connection connection)
        {
            Channels = connection;
            Process = childProcess;
            this.debuggerEnabled = debuggerEnabled;

            try
            {
                pid = childProcess.Id;
            }
            catch
            {
                pid = -1;
            }

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

            if (debuggerEnabled && pid != -1)
            {
                DetachDebugger(pid);
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
