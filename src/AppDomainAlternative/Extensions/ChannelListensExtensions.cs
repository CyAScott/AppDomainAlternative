using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative.Extensions
{
    internal static class ChannelListensExtensions
    {
        private static Dictionary<string, (MethodInfo method, Type[] argumentTypes)[]> getInstanceShape(this object instance) => instance
            .GetType()
            .GetMethods()
            .Where(method => method.IsVirtual && method.DeclaringType != typeof(object))
            .GroupBy(method => method.Name)
            .ToDictionary(group => group.Key, group => group
                .Select(method => (method, method.GetParameters().Select(param => param.ParameterType).ToArray()))
                .ToArray());
        private static async Task<(bool success, Type type)> readType(this IInternalChannel channel, CancellationTokenSource disposeToken)
        {
            try
            {
                var type = (Type)await channel.Serializer.Deserialize(channel.Reader, typeof(Type), channel.Connection, disposeToken.Token).ConfigureAwait(false);

                return channel.IsDisposed ? (false, null) : (true, type);
            }
            catch
            {
                return (false, null);
            }
        }
        private static async Task<(bool success, object value)> readValue(this IInternalChannel channel, CancellationTokenSource disposeToken, Type type)
        {
            try
            {
                var responseValue = await channel.Serializer.Deserialize(channel.Reader, type, channel.Connection, disposeToken.Token).ConfigureAwait(false);

                return channel.IsDisposed ? (false, null) : (true, responseValue);
            }
            catch
            {
                return (false, null);
            }
        }
        private static async Task<bool> fillBuffer(this IInternalChannel channel, CancellationTokenSource disposeToken, byte[] buffer, int length)
        {
            try
            {
                var index = 0;
                while (!channel.IsDisposed && index < length)
                {
                    index += await channel.Reader.BaseStream.ReadAsync(buffer, index, length - index, disposeToken.Token).ConfigureAwait(false);
                }

                return !channel.IsDisposed;
            }
            catch
            {
                return false;
            }
        }
        private static async void invoke(this IInternalChannel channel, int requestId, MethodInfo method, object[] arguments)
        {
            await Task.Yield();

            Type responseType;
            object response;
            var success = false;
            try
            {
                response = method.Invoke(channel.Instance, arguments);

                if (response is Task task)
                {
                    await task.ConfigureAwait(false);

                    response = null;
                    responseType = typeof(void);

                    if (method.ReturnType.IsGenericType &&
                        method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        response = method.ReturnType.GetProperty("Result").GetValue(task);
                        responseType = response?.GetType() ?? method.ReturnType.GetGenericArguments().Single();
                    }
                }
                else
                {
                    responseType = response?.GetType() ?? method.ReturnType;
                }

                success = true;
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                response = error.InnerException;
                responseType = response.GetType();
            }
            catch (Exception error)
            {
                response = error;
                responseType = response.GetType();
            }

            var responseStream = new MemoryStream();

            using (var writer = new BinaryWriter(responseStream, Encoding.UTF8, true))
            {
                try
                {
                    writer.Write(requestId);
                    writer.Write(success);

                    await channel.Serializer.Serialize(writer, typeof(Type), responseType, channel.Connection).ConfigureAwait(false);
                    await channel.Serializer.Serialize(writer, responseType, response, channel.Connection).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    try
                    {
                        responseStream.Position = 0;
                        responseStream.SetLength(0);

                        writer.Write(requestId);
                        writer.Write(false);

                        await channel.Serializer.Serialize(writer, typeof(Type), typeof(InvalidOperationException), channel.Connection).ConfigureAwait(false);
                        await channel.Serializer.Serialize(writer, typeof(InvalidOperationException),
                            new InvalidOperationException($"Failure to serialize: {responseType.Name}", error), channel.Connection).ConfigureAwait(false);
                    }
                    catch
                    {
                        try
                        {
                            channel.Dispose();
                            return;
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }

            try
            {
                responseStream.Position = 0;
                channel.Connection.Write(channel.Id, responseStream);
            }
            catch
            {
                try
                {
                    channel.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static async Task<T> RemoteInvoke<T>(this IInternalChannel channel,
            ConcurrentDictionary<int, TaskCompletionSource<object>> remoteRequests,
            Func<int> generateRequestId,
            bool fireAndForget,
            string methodName,
            params Tuple<Type, object>[] args)
        {
            var requestStream = new MemoryStream();

            //requestId
            var requestId = generateRequestId();
            await requestStream.WriteAsync(BitConverter.GetBytes(requestId), 0, 4).ConfigureAwait(false);

            using (var writer = new BinaryWriter(requestStream, Encoding.UTF8, true))
            {
                //methodName
                await channel.Serializer.Serialize(writer, typeof(string), methodName, channel.Connection).ConfigureAwait(false);

                //argLength
                writer.Write((byte)args.Length);

                //args
                foreach (var arg in args)
                {
                    await channel.Serializer.Serialize(writer, typeof(Type), arg.Item2 == null ? arg.Item1 : arg.Item2.GetType(), channel.Connection).ConfigureAwait(false);
                    await channel.Serializer.Serialize(writer, arg.Item1, arg.Item2, channel.Connection).ConfigureAwait(false);
                }
            }

            var task = new TaskCompletionSource<object>();

            //enqueue request
            while (!remoteRequests.TryAdd(requestId, task))
            {
                //we need to generate a new request id
                requestId = generateRequestId();
                requestStream.Position = 0;
                await requestStream.WriteAsync(BitConverter.GetBytes(requestId), 0, 4).ConfigureAwait(false);
            }

            requestStream.Position = 0;
            channel.Connection.Write(channel.Id, requestStream);

            return fireAndForget ? default(T) : (T)await task.Task.ConfigureAwait(false);
        }

        public static async Task ListenForRequests(this IInternalChannel channel,
            CancellationTokenSource cancel,
            object instance)
        {
            var members = instance.getInstanceShape();

            await Task.Yield();

            var buffer = new byte[4];

            try
            {
                while (!channel.IsDisposed)
                {
                    //read the request id
                    if (!await channel.fillBuffer(cancel, buffer, 4).ConfigureAwait(false))
                    {
                        break;
                    }
                    var requestId = BitConverter.ToInt32(buffer, 0);

                    //read the method name for the request
                    var methodName = (string)await channel.Serializer.Deserialize(channel.Reader, typeof(string), channel.Connection, cancel.Token).ConfigureAwait(false);

                    //read the method argument count
                    if (!await channel.fillBuffer(cancel, buffer, 1).ConfigureAwait(false))
                    {
                        break;
                    }
                    var argLength = buffer[0];

                    //read the arguments in pairs of type and value
                    var argTypes = new Type[argLength];
                    var argValues = new object[argLength];
                    var index = 0;
                    for (; index < argLength; index++)
                    {
                        var argTypeInfo = await channel.readType(cancel).ConfigureAwait(false);
                        if (!argTypeInfo.success)
                        {
                            break;
                        }
                        argTypes[index] = argTypeInfo.type;

                        var argValueInfo = await channel.readValue(cancel, argTypeInfo.type).ConfigureAwait(false);
                        if (!argValueInfo.success)
                        {
                            break;
                        }
                        argValues[index] = argValueInfo.value;
                    }
                    if (index != argLength)
                    {
                        break;
                    }

                    //if the method name is not found then respond with an exception
                    if (!members.TryGetValue(methodName, out var signatures))
                    {
                        try
                        {
                            var responseStream = new MemoryStream();

                            using (var writer = new BinaryWriter(responseStream, Encoding.UTF8, true))
                            {
                                writer.Write(requestId);
                                writer.Write(false);
                                await channel.Serializer.Serialize(writer, typeof(ArgumentException), new ArgumentException("Member name not found."), channel.Connection).ConfigureAwait(false);
                            }

                            responseStream.Position = 0;
                            channel.Connection.Write(channel.Id, responseStream);
                        }
                        catch
                        {
                            break;
                        }
                        continue;
                    }

                    //find the overloaded method to use
                    MethodInfo method;
                    try
                    {
                        (method, _) = signatures.Single(signature =>
                            signature.argumentTypes.Length == argTypes.Length &&
                            signature.argumentTypes.Zip(argTypes, (a, b) => a.IsAssignableFrom(b)).All(_ => _));
                    }
                    catch
                    {
                        //if the method signature was not found then respond with an exception
                        try
                        {
                            var responseStream = new MemoryStream();

                            using (var writer = new BinaryWriter(responseStream, Encoding.UTF8, true))
                            {
                                writer.Write(requestId);
                                writer.Write(false);
                                await channel.Serializer.Serialize(writer, typeof(ArgumentException), new ArgumentException("Signature not found."), channel.Connection).ConfigureAwait(false);
                            }

                            responseStream.Position = 0;
                            channel.Connection.Write(channel.Id, responseStream);
                        }
                        catch
                        {
                            break;
                        }
                        continue;
                    }

                    //invoke the method
                    channel.invoke(requestId, method, argValues);
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                channel.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public static async Task ListenForResponses(this IInternalChannel channel,
            CancellationTokenSource cancel,
            ConcurrentDictionary<int, TaskCompletionSource<object>> remoteRequests,
            object instance)
        {
            await Task.Yield();

            var buffer = new byte[4];

            try
            {
                while (!channel.IsDisposed)
                {
                    if (!await channel.fillBuffer(cancel, buffer, 4).ConfigureAwait(false))
                    {
                        break;
                    }
                    var requestId = BitConverter.ToInt32(buffer, 0);

                    if (!await channel.fillBuffer(cancel, buffer, 1).ConfigureAwait(false))
                    {
                        break;
                    }
                    var success = buffer[0] > byte.MinValue;

                    var typeInfo = await channel.readType(cancel).ConfigureAwait(false);
                    if (!typeInfo.success)
                    {
                        break;
                    }

                    var response = await channel.readValue(cancel, typeInfo.type).ConfigureAwait(false);
                    if (!response.success)
                    {
                        break;
                    }

                    if (!remoteRequests.TryRemove(requestId, out var request))
                    {
                        continue;
                    }

                    try
                    {
                        if (success)
                        {
                            request.TrySetResult(response.value);
                        }
                        else
                        {
                            request.TrySetException((Exception)response.value);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                channel.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public static async void StartListening(this IInternalChannel channel,
            CancellationTokenSource cancel,
            ConcurrentDictionary<int, TaskCompletionSource<object>> remoteRequests)
        {
            if (channel.IsHost)
            {
                await channel.ListenForRequests(cancel, channel.Instance).ConfigureAwait(false);
            }
            else
            {
                await channel.ListenForResponses(cancel, remoteRequests, channel.Instance).ConfigureAwait(false);
            }
        }
    }
}
