using System.Diagnostics;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative
{
    /// <summary>
    /// A wrapper for a <see cref="Process"/>.
    /// </summary>
    public abstract class Domains
    {
        internal Domains()
        {
        }

        /// <summary>
        /// All the open <see cref="IChannel"/>s between the parent and child <see cref="Domains"/>.
        /// </summary>
        public abstract IHaveChannels Channels { get; }

        /// <summary>
        /// The process for the domain.
        /// </summary>
        public abstract Process Process { get; }

        /// <summary>
        /// The current domain (aka Process).
        /// </summary>
        public static readonly CurrentDomain Current = new CurrentDomain(Process.GetCurrentProcess());
    }
}
