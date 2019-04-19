using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AppDomainAlternative.Serializer
{
    /// <summary>
    /// An interface for a serializer that will serialize/deserialize data for IPC.
    /// </summary>
    public interface IAmASerializer
    {
        /// <summary>
        /// Serializes an object.
        /// </summary>
        Task Serialize(BinaryWriter writer, Type valueType, object value, IResolveProxyIds resolver);

        /// <summary>
        /// Deserializes an object.
        /// </summary>
        Task<object> Deserialize(BinaryReader reader, Type valueType, IResolveProxyIds resolver, CancellationToken token);

        /// <summary>
        /// Test if a type can be serialized.
        /// </summary>
        bool CanSerialize(Type type);

        /// <summary>
        /// A name identifier for the serializer.
        /// </summary>
        string Name { get; }
    }
}
