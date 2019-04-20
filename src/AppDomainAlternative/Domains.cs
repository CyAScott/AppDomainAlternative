using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative
{
    internal interface IDomains
    {
        Connection Connection { get; }
    }

    /// <summary>
    /// A wrapper for a <see cref="Process"/>.
    /// </summary>
    public abstract class Domains
    {
        private Connection Connection => ((IDomains)this).Connection;

        internal Domains(int id) => Id = id;

        /// <summary>
        /// All the open <see cref="IChannel"/>s.
        /// </summary>
        public IEnumerable<IChannel> Channels => Connection.Where(channel => channel.Instance != null);

        /// <summary>
        /// Creates a proxy instance from a ctor for a class.
        /// </summary>
        /// <param name="baseCtor">The constructor for the class to generate a proxy for.</param>
        /// <param name="hostInstance">If true, the object instance should exist within this process and all method calls from the child/parent process are proxied to this process.</param>
        /// <param name="arguments">The constructor arguments.</param>
        public Task<object> CreateInstance(ConstructorInfo baseCtor, bool hostInstance, params object[] arguments)
        {
            if (Connection == null)
            {
                throw new InvalidOperationException("There is no parent process connection.");
            }
            return Connection.CreateInstance(baseCtor, hostInstance, arguments);
        }

        /// <summary>
        /// The process for the domain.
        /// </summary>
        public abstract Process Process { get; }

        /// <summary>
        /// Gets the next instance of a shared instance.
        /// </summary>
        /// <param name="fetch">How the instance should be fetched.</param>
        /// <param name="filter">A filter for fetching the instance.</param>
        /// <param name="cancel">A <see cref="CancellationToken"/> to cancel the await.</param>
        public async Task<(bool IsHost, T Instance)> GetInstanceOf<T>(
            FetchInstanceBy fetch = FetchInstanceBy.Any,
            Func<bool, T, bool> filter = null,
            CancellationToken cancel = default(CancellationToken))
            where T : class
        {
            var existingInstance = (fetch & FetchInstanceBy.Existing) == FetchInstanceBy.Existing ? Channels
                .Select(channel => new
                {
                    channel.IsHost,
                    Instance = channel.Instance as T
                })
                .FirstOrDefault(channel =>
                    channel.Instance != null &&
                    (filter == null || filter(channel.IsHost, channel.Instance))) : null;

            if (existingInstance != null)
            {
                return (existingInstance.IsHost, existingInstance.Instance);
            }

            if ((fetch & FetchInstanceBy.Next) != FetchInstanceBy.Next)
            {
                return (false, null);
            }

            var task = new TaskCompletionSource<(bool IsHost, T Instance)>();

            cancel.Register(() => task.TrySetCanceled());

            var listener = new Action<Domains, IChannel>((_, channel) =>
            {
                if (channel.Instance is T newInstance &&
                    (filter == null || filter(channel.IsHost, newInstance)))
                {
                    task.TrySetResult((channel.IsHost, newInstance));
                }
            });

            Connection.NewChannel += listener;

            try
            {
                await task.Task.ConfigureAwait(false);

                return task.Task.Result;
            }
            finally
            {
                Connection.NewChannel -= listener;
            }
        }

        /// <summary>
        /// Is invoked when a new <see cref="IChannel"/> is opened.
        /// </summary>
        public event Action<Domains, IChannel> NewChannel
        {
            add => Connection.NewChannel += value;
            remove => Connection.NewChannel -= value;
        }

        /// <summary>
        /// Count of all the open <see cref="IChannel"/>s.
        /// </summary>
        public int ChannelCount => Channels.Count();

        /// <summary>
        /// The domain id (aka process id).
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The current domain (aka Process).
        /// </summary>
        public static readonly CurrentDomain Current = new CurrentDomain(Process.GetCurrentProcess());
    }

    /// <summary>
    /// When getting a shared instance between <see cref="Domains"/> how the get should fetch the instance.
    /// </summary>
    [Flags]
    public enum FetchInstanceBy
    {
        /// <summary>
        /// <see cref="Existing"/> or <see cref="Next"/>
        /// </summary>
        Any = Existing | Next,

        /// <summary>
        /// Only gets an instance if it was already created.
        /// </summary>
        Existing = 1,

        /// <summary>
        /// Only gets the next created instance.
        /// </summary>
        Next = 2
    }
}
