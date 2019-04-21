using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.RegularExpressions;
using System.Web;
using AppDomainAlternative.Ipc;
using AppDomainAlternative.Proxy;
using AppDomainAlternative.Serializer;

namespace AppDomainAlternative
{
    /// <summary>
    /// The current domain (aka Process).
    /// </summary>
    public sealed class CurrentDomain : Domains, IEnumerable<ChildDomain>
    {
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private const string connectionStringVarName = "__ParentProcessConnectionString__";
        private readonly ConcurrentDictionary<int, ChildDomain> children = new ConcurrentDictionary<int, ChildDomain>();

        internal CurrentDomain(Process current)
        {
            Process = current;

            var parentConnectionString = Regex.Match(Environment.GetEnvironmentVariable(connectionStringVarName) ?? "",
                @"^pid=(?<pid>\d+)&write=(?<write>\d+)&read=(?<read>\d+)&debug=(?<debug>[01])&serializer=(?<serializer>[^&]+)&proxyGenerator=(?<proxyGenerator>[^&]+)$", RegexOptions.IgnoreCase);

            if (!parentConnectionString.Success)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(connectionStringVarName)))
                {
                    throw new InvalidOperationException($"Invalid connection string from parent: {Environment.GetEnvironmentVariable(connectionStringVarName)}");
                }
                return;
            }

            var parent = Process.GetProcessById(Convert.ToInt32(parentConnectionString.Groups["pid"].Value));

            var serializerName = HttpUtility.HtmlDecode(parentConnectionString.Groups["serializer"].Value);
            var proxyGeneratorName = HttpUtility.HtmlDecode(parentConnectionString.Groups["proxyGenerator"].Value);

            Channels = new Connection(this,
                DomainConfiguration.SerializerResolver(serializerName) ?? throw new InvalidOperationException($"Invalid serializer from parent: {serializerName}"),
                DomainConfiguration.Resolver(proxyGeneratorName) ?? throw new InvalidOperationException($"Invalid proxy generator from parent: {proxyGeneratorName}"),
                new AnonymousPipeClientStream(PipeDirection.In, parentConnectionString.Groups["read"].Value),
                new AnonymousPipeClientStream(PipeDirection.Out, parentConnectionString.Groups["write"].Value));

            parent.EnableRaisingEvents = true;
            parent.Exited += (sender, eventArgs) => Environment.Exit(0);
        }

        /// <summary>
        /// Gets a child domain by <see cref="System.Diagnostics.Process.Id"/>.
        /// </summary>
        public Domains this[int id] => children[id];

        /// <summary>
        /// Creates a child domain.
        /// </summary>
        public ChildDomain AddChildDomain(ProcessStartInfo startInfo, IAmASerializer serializer = null, IGenerateProxies proxyGenerator = null)
        {
            //if the path is missing then
            if (startInfo == null)
            {
                throw new ArgumentNullException(nameof(startInfo));
            }

            var childProcess = new Process
            {
                StartInfo = startInfo
            };

            proxyGenerator = proxyGenerator ?? DefaultProxyFactory.Instance;
            serializer = serializer ?? DefaultSerializer.Instance;

            var read = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            var write = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            startInfo.Environment[connectionStringVarName] =
                $"pid={Current.Process.Id}&" +
                $"write={write.GetClientHandleAsString()}&" +
                $"read={read.GetClientHandleAsString()}&" +
                $"debug={(Debugger.IsAttached ? 1 : 0)}&" +
                $"serializer={HttpUtility.UrlEncode(serializer.Name)}&" +
                $"proxyGenerator={HttpUtility.UrlEncode(proxyGenerator.Name)}";
            startInfo.UseShellExecute = false;

            childProcess.EnableRaisingEvents = true;

            childProcess.Start();

            read.DisposeLocalCopyOfClientHandle();
            write.DisposeLocalCopyOfClientHandle();

            //NOTE: the read and write streams are switched for the server side
            var child = new ChildDomain(childProcess, new Connection(this, serializer, proxyGenerator, write, read));

            children[childProcess.Id] = child;

            child.Process.Exited += (sender, eventArgs) => children.TryRemove(Process.Id, out _);

            if (child.Process.HasExited)
            {
                children.TryRemove(Process.Id, out _);
            }

            return child;
        }

        /// <inheritdoc />
        public IEnumerator<ChildDomain> GetEnumerator() => children.Values.GetEnumerator();

        /// <summary>
        /// Attempts to get a child domain by <see cref="System.Diagnostics.Process.Id"/>.
        /// </summary>
        public bool TryToGetChild(int id, out ChildDomain child) => children.TryGetValue(id, out child);

        /// <summary>
        /// The number of live child domains created by this domain.
        /// </summary>
        public int Count => children.Count;

        /// <inheritdoc />
        public override IHaveChannels Channels { get; }

        /// <summary>
        /// The current process.
        /// </summary>
        public override Process Process { get; }
    }
}
