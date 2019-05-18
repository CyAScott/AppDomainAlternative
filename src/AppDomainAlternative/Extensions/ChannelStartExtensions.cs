using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDomainAlternative.Ipc;
using AppDomainAlternative.Proxy;

namespace AppDomainAlternative.Extensions
{
    internal static class ChannelStartExtensions
    {
        public static async Task<(bool isHost, object instance)> RemoteStart(this IInternalChannel channel, CancellationTokenSource cancel, IGenerateProxies proxyGenerator)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (cancel == null)
            {
                throw new ArgumentNullException(nameof(cancel));
            }

            if (proxyGenerator == null)
            {
                throw new ArgumentNullException(nameof(proxyGenerator));
            }

            bool isHost;
            object instance;
            var responseStream = new MemoryStream();

            using (var writer = new BinaryWriter(responseStream, Encoding.UTF8, true))
            {
                try
                {
                    //write success (we may rewrite this byte if there is an error later)
                    writer.Write(true);

                    //read hostInstance
                    var makeProxy = channel.Reader.ReadBoolean();
                    isHost = !makeProxy;

                    //read type to proxy
                    var type = (Type)await channel.Serializer.Deserialize(channel.Reader, typeof(Type), channel.Connection, cancel.Token).ConfigureAwait(false);

                    //read ctor param count
                    var paramCount = channel.Reader.ReadByte();

                    //read each ctor param in pairs of type and value
                    var arguments = new object[paramCount];
                    var types = new Type[paramCount];
                    for (var index = 0; index < paramCount; index++)
                    {
                        var paramType = types[index] = (Type)await channel.Serializer.Deserialize(channel.Reader, typeof(Type), channel.Connection, cancel.Token).ConfigureAwait(false);

                        arguments[index] = await channel.Serializer.Deserialize(channel.Reader, paramType, channel.Connection, cancel.Token).ConfigureAwait(false);
                    }

                    //find the ctor
                    var ctor = type.GetConstructors()
                        .Select(item => new
                        {
                            ctor = item,
                            @params = item.GetParameters()
                        })
                        .FirstOrDefault(ctorInfo =>
                            ctorInfo.@params.Length == paramCount &&
                            ctorInfo.@params
                                .Select(param => param.ParameterType)
                                .Zip(types, (a, b) => a.IsAssignableFrom(b))
                                .All(result => result))?.ctor ??
                        throw new ArgumentException("Unable fond the constructor.");

                    //create the instance
                    instance = makeProxy ? proxyGenerator.GenerateProxy(channel, ctor, arguments) : ctor.Invoke(arguments);
                }
                catch (Exception error)
                {
                    instance = null;
                    isHost = false;
                    responseStream.Position = 0;
                    responseStream.SetLength(0);

                    var errorType = error.GetType();

                    //write unsuccessful
                    writer.Write(false);

                    //write the error
                    await channel.Serializer.Serialize(writer, typeof(Type), errorType, channel.Connection).ConfigureAwait(false);
                    await channel.Serializer.Serialize(writer, errorType, error, channel.Connection).ConfigureAwait(false);
                }
            }

            //send the response
            try
            {
                responseStream.Position = 0;
                channel.Connection.Write(channel.Id, responseStream);
            }
            catch
            {
                // ignored
            }

            return (isHost, instance);
        }
        public static async Task<object> LocalStart(this IInternalChannel channel, CancellationTokenSource cancel, IGenerateProxies proxyGenerator, ConstructorInfo ctor, bool hostInstance, params object[] arguments)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (cancel == null)
            {
                throw new ArgumentNullException(nameof(cancel));
            }

            if (ctor == null)
            {
                throw new ArgumentNullException(nameof(ctor));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (proxyGenerator == null)
            {
                throw new ArgumentNullException(nameof(proxyGenerator));
            }

            var response = new byte[] { 1 };
            var responseTask = channel.Buffer.ReadAsync(response, 0, 1);

            var initRequest = new MemoryStream();
            using (var writer = new BinaryWriter(initRequest, Encoding.UTF8, true))
            {
                //write hostInstance
                writer.Write(hostInstance);

                //write type to proxy
                await channel.Serializer.Serialize(writer, typeof(Type), ctor.DeclaringType, channel.Connection).ConfigureAwait(false);

                //write ctor param count
                var @params = ctor.GetParameters();
                writer.Write((byte)@params.Length);

                if (arguments.Length != @params.Length)
                {
                    throw new ArgumentException("Invalid constructor.");
                }

                //write each ctor param in pairs of type and value
                foreach (var param in @params.Zip(arguments, (param, arg) => new { type = arg?.GetType() ?? param.ParameterType, arg }))
                {
                    await channel.Serializer.Serialize(writer, typeof(Type), param.type, channel.Connection).ConfigureAwait(false);
                    await channel.Serializer.Serialize(writer, param.type, param.arg, channel.Connection).ConfigureAwait(false);
                }
            }

            initRequest.Position = 0;
            channel.Connection.Write(channel.Id, initRequest);

            await responseTask.ConfigureAwait(false);

            if (response[0] != 0)
            {
                return hostInstance ? ctor.Invoke(arguments) : proxyGenerator.GenerateProxy(channel, ctor, arguments);
            }

            var exceptionType = (Type)await channel.Serializer.Deserialize(channel.Reader, typeof(Type), channel.Connection, cancel.Token).ConfigureAwait(false);
            var exception = (Exception)await channel.Serializer.Deserialize(channel.Reader, exceptionType, channel.Connection, cancel.Token).ConfigureAwait(false);

            throw exception;
        }
    }
}
