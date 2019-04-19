using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppDomainAlternative.Serializer;

namespace AppDomainAlternative.Proxy
{
    public class MockInterceptor : IInterceptor
    {
        public IAmASerializer Serializer => MockSerializer;
        public MockSerializer MockSerializer { get; } = new MockSerializer();
        public async Task<T> RemoteInvoke<T>(bool fireAndForget, string methodName, params Tuple<Type, object>[] args)
        {
            await Task.Yield();

            Logs.Enqueue(new InvokeArgs
            {
                Args = args,
                FireAndForget = fireAndForget,
                MethodName = methodName,
                ReturnType = typeof(T)
            });

            //wait to simulate the time it takes to remote invoke the call across the process barrier
            await Task.Delay(1).ConfigureAwait(false);

            return (T)RemoteInvokeReturnValue;
        }
        public object RemoteInvokeReturnValue { get; set; }
        public readonly Queue<InvokeArgs> Logs = new Queue<InvokeArgs>();
    }

    public class InvokeArgs
    {
        public Tuple<Type, object>[] Args { get; set; }
        public Type ReturnType { get; set; }
        public bool FireAndForget { get; set; }
        public string MethodName { get; set; }
    }
}
