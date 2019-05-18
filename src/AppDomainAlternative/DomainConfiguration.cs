using System;
using AppDomainAlternative.Proxy;
using AppDomainAlternative.Serializer;
using AppDomainAlternative.Serializer.Default;

namespace AppDomainAlternative
{
    /// <summary>
    /// Global configuration settings for Inter Process Communication (IPC).
    /// </summary>
    public static class DomainConfiguration
    {
        /// <summary>
        /// A resolver for <see cref="IAmASerializer"/>s.
        /// The resolver resolves by name/id.
        /// </summary>
        public static Func<string, IAmASerializer> SerializerResolver { get; set; } = DefaultSerializer.Resolve;

        /// <summary>
        /// A resolver for <see cref="IGenerateProxies"/>.
        /// The resolver resolves by name/id.
        /// </summary>
        public static Func<string, IGenerateProxies> Resolver { get; set; } = DefaultProxyFactory.Resolve;
    }
}
