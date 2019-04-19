using System;

namespace AppDomainAlternative.Proxy
{
    /// <summary>
    /// Settings for proxying a class and its members
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class ProxyAttribute : Attribute
    {
        /// <summary>
        /// What to do if the return value or member arguments cannot be passed across the process barrier.
        /// The default value is <see cref="Proxy.InvalidTypeHandling.ThrowErrorOnCreate"/>.
        /// </summary>
        public InvalidTypeHandling InvalidTypeHandling { get; set; } = InvalidTypeHandling.ThrowErrorOnCreate;

        /// <summary>
        /// Enables proxying.
        /// The default value is true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// If true and the member has no return type (Task or void) then the invoke command is sent to the host asynchronously and the response will be ignored.
        /// The default value is false.
        /// </summary>
        public bool FireAndForget { get; set; }
    }

    /// <inheritdoc cref="ProxyAttribute.InvalidTypeHandling"/>
    public enum InvalidTypeHandling
    {
        /// <summary>
        /// Don't proxy members where a return type or argument type cannot be passed across the process barrier.
        /// </summary>
        Ignore,

        /// <summary>
        /// Throw an error when generating a proxy class that has members where a return type or argument type cannot be passed across the process barrier.
        /// </summary>
        ThrowErrorOnCreate,

        /// <summary>
        /// Throw an error when invoking members where a return type or argument type cannot be passed across the process barrier.
        /// </summary>
        ThrowErrorOnInvoke
    }
}
