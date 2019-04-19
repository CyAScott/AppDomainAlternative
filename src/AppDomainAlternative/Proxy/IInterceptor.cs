using System;
using System.Threading.Tasks;
using AppDomainAlternative.Serializer;

namespace AppDomainAlternative.Proxy
{
    /// <summary>
    /// An interceptor for proxy calls.
    /// </summary>
    public interface IInterceptor
    {
        /// <summary>
        /// The serializer for serializing/deserializing data.
        /// </summary>
        IAmASerializer Serializer { get; }

        /// <summary>
        /// Invokes an instance across the process barrier.
        /// </summary>
        Task<T> RemoteInvoke<T>(bool fireAndForget, string methodName, params Tuple<Type, object>[] args);
    }
}
