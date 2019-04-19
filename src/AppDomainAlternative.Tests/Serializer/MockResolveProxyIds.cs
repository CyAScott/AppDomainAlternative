using System.Collections.Concurrent;
using System.Linq;

namespace AppDomainAlternative.Serializer
{
    public class MockResolveProxyIds : IResolveProxyIds
    {
        public readonly ConcurrentDictionary<long, object> Instances = new ConcurrentDictionary<long, object>();

        public bool TryToGetInstanceId(object instance, out long id)
        {
            foreach (var pair in Instances.Where(pair => ReferenceEquals(pair.Value, instance)))
            {
                id = pair.Key;
                return true;
            }

            id = 0;
            return false;
        }

        public bool TryToGetInstance(long id, out object instance) => Instances.TryGetValue(id, out instance);
    }
}
