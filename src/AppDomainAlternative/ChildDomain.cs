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

        internal ChildDomain(Process childProcess, Connection connection)
        {
            childProcess.Exited += InvokeExited;
            Process = childProcess;
            Connection = connection;
        }
        internal void InvokeExited(object sender, EventArgs args)
        {
            HasExited = true;
            Exited?.Invoke(this, args);
            Dispose();
        }

        private int disposed;

        /// <summary>
        /// Indicates if the domain has ended.
        /// </summary>
        public bool HasExited { get; private set; }

        /// <summary>
        /// A event handler invoked when the child domain terminates.
        /// </summary>
        public event Action<object, EventArgs> Exited;

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

            Connection.Dispose();

            try
            {
                Process.Kill();
            }
            catch
            {
                // ignored
            }

            Process.Dispose();
        }
    }
}
