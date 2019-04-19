using System;

namespace AppDomainAlternative.Serializer
{
    /// <summary>
    /// Passes a class instance, parameter value, or property value by proxy.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter | AttributeTargets.Property)]
    public class PassByProxyAttribute : Attribute
    {
    }
}
