namespace AppDomainAlternative.Serializer
{
    /// <summary>
    /// A resolver for resolving instance to id and vice versa.
    /// </summary>
    public interface IResolveProxyIds
    {
        /// <summary>
        /// Attempts to get the id for an instance.
        /// </summary>
        bool TryToGetInstanceId(object instance, out long id);

        /// <summary>
        /// Attempts to get the instance for an id.
        /// </summary>
        bool TryToGetInstance(long id, out object instance);
    }
}
