using System.Reflection;

namespace AppDomainAlternative.Proxy
{
    /// <summary>
    /// An interface for a proxy generator.
    /// </summary>
    public interface IGenerateProxies
    {
        /// <summary>
        /// Generates a proxy.
        /// </summary>
        object GenerateProxy(IInterceptor interceptor, ConstructorInfo ctor, params object[] arguments);

        /// <summary>
        /// A name identifier for the proxy generator.
        /// </summary>
        string Name { get; }
    }
}
