using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AppDomainAlternative.Proxy
{
    internal sealed class DefaultProxyFactory : IGenerateProxies
    {
        private ConstructorInfo generateType(Guid id, IInterceptor interceptor, ConstructorInfo ctor)
        {
            if (interceptor == null)
            {
                throw new ArgumentNullException(nameof(interceptor));
            }

            var baseType = ctor?.DeclaringType ?? throw new ArgumentNullException(nameof(ctor));
            var name = baseType.Name;
            var genericNameIndex = name.IndexOf('`');
            if (genericNameIndex != -1)
            {
                name = name.Substring(0, genericNameIndex);
            }

            var settings = new ProxyAttribute();

            if (!settings.Enabled)
            {
                throw new NotSupportedException("This class does not support proxying.");
            }

            var typeBuilder = proxyModule.Value.DefineType($"ProxyFor{name}_{id.ToString().Replace("-", "")}",
                TypeAttributes.Class |
                TypeAttributes.Public |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.Sealed |
                TypeAttributes.BeforeFieldInit,
                baseType);

            var interceptorField = typeBuilder.DefineField("interceptor", typeof(IInterceptor),
                FieldAttributes.Private | FieldAttributes.InitOnly);

            generateCtor(typeBuilder, interceptorField, ctor);

            if (baseType.GetMethods()
                .Where(methodInfo => methodInfo.DeclaringType != typeof(object) && methodInfo.IsVirtual && !methodInfo.IsGenericMethod)
                .Count(methodInfo => tryToOverrideMethod(settings, interceptor, typeBuilder, interceptorField, methodInfo)) == 0)
            {
                throw new ArgumentException($"Unable to proxy any member for this type: {baseType.FullName}");
            }

            return typeBuilder.CreateTypeInfo().GetConstructors().Single();
        }
        private Guid getHashId(ConstructorInfo ctor)
        {
            if (ctor?.DeclaringType == null)
            {
                throw new ArgumentNullException(nameof(ctor));
            }
            using (var md5 = MD5.Create())
            {
                // ReSharper disable once PossibleNullReferenceException
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes($"{ctor.DeclaringType.Assembly.FullName};{ctor.DeclaringType.FullName};" +
                                                                       string.Join(";", ctor.GetParameters().Select(param => param.ParameterType.FullName)))));
            }
        }
        private bool handleInvalidType(InvalidTypeHandling invalidTypeHandling, TypeBuilder typeBuilder, MethodInfo methodInfo, string errorMessage)
        {
            switch (invalidTypeHandling)
            {
                default:
                    throw new InvalidCastException();

                case InvalidTypeHandling.Ignore:
                    return false;

                case InvalidTypeHandling.ThrowErrorOnCreate:
                    throw new ArgumentException(errorMessage);

                case InvalidTypeHandling.ThrowErrorOnInvoke:
                    var methodBuilder = typeBuilder.DefineMethod(
                        methodInfo.Name,
                        methodInfo.Attributes,
                        methodInfo.CallingConvention,
                        methodInfo.ReturnType,
                        methodInfo.GetParameters().Select(param => param.ParameterType).ToArray());

                    var il = methodBuilder.GetILGenerator();

                    il.Emit(OpCodes.Newobj, typeof(NotSupportedException).GetConstructors().First());
                    il.Emit(OpCodes.Throw);

                    typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);

                    return true;
            }
        }
        private bool tryToOverrideMethod(ProxyAttribute settings, IInterceptor interceptor, TypeBuilder typeBuilder, FieldBuilder interceptorField, MethodInfo methodInfo)
        {
            settings = methodInfo.GetCustomAttribute<ProxyAttribute>() ?? settings;

            if (!settings.Enabled)
            {
                return false;
            }

            var returnType = methodInfo.ReturnType;

            if (typeof(Task) == returnType)
            {
                returnType = typeof(void);
            }
            else if (typeof(Task).IsAssignableFrom(returnType))
            {
                if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
                {
                    return handleInvalidType(settings.InvalidTypeHandling, typeBuilder, methodInfo,
                        $"Unable to create proxy class. Unknown Task return type for method: {methodInfo.Name}");
                }
                returnType = returnType.GetGenericArguments().Single();
            }

            if (returnType != typeof(void) && !interceptor.Serializer.CanSerialize(returnType))
            {
                return handleInvalidType(settings.InvalidTypeHandling, typeBuilder, methodInfo,
                    $"Unable to create proxy class. Invalid return type for method: {methodInfo.Name}");
            }

            var @params = methodInfo.GetParameters();

            if (@params.Any(param => param.IsOut || !interceptor.Serializer.CanSerialize(param.ParameterType)))
            {
                return handleInvalidType(settings.InvalidTypeHandling, typeBuilder, methodInfo,
                    $"Unable to create proxy class. Invalid argument type for method: {methodInfo.Name}");
            }

            var methodBuilder = typeBuilder.DefineMethod(
                methodInfo.Name,
                methodInfo.Attributes,
                methodInfo.CallingConvention,
                methodInfo.ReturnType,
                @params.Select(param => param.ParameterType).ToArray());

            var il = methodBuilder.GetILGenerator();

            //load interceptor field
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, interceptorField);

            //load fire and forget boolean value
            var fireAndForget = settings.FireAndForget &&
                                (methodInfo.ReturnType == typeof(void) || methodInfo.ReturnType == typeof(Task));
            il.Emit(fireAndForget ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

            //load method name
            il.Emit(OpCodes.Ldstr, methodInfo.Name);

            //load new tuple array
            if (@params.Length == 0)
            {
                // ReSharper disable once PossibleNullReferenceException
                il.Emit(OpCodes.Call, typeof(Array).GetMethod("Empty").MakeGenericMethod(typeof(Tuple<Type, object>)));
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, @params.Length);
                il.Emit(OpCodes.Newarr, typeof(Tuple<Type, object>));

                for (var index = 0; index < @params.Length; index++)
                {
                    il.Emit(OpCodes.Dup);

                    //load param index
                    il.Emit(OpCodes.Ldc_I4, index);

                    var param = @params[index];

                    //load typeof parameter
                    il.Emit(OpCodes.Ldtoken, param.ParameterType);
                    il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", new[] { typeof(RuntimeTypeHandle) }));

                    //load argument
                    il.Emit(OpCodes.Ldarg, index + 1);
                    il.Emit(OpCodes.Box, param.ParameterType);

                    //make tuple of parameter type and argument
                    il.Emit(OpCodes.Newobj, typeof(Tuple<Type, object>).GetConstructors().First());

                    //set tuple value to loaded index in the tuple array
                    il.Emit(OpCodes.Stelem_Ref);
                }
            }

            // ReSharper disable once PossibleNullReferenceException
            il.Emit(OpCodes.Callvirt, typeof(IInterceptor)
                .GetMethod(nameof(IInterceptor.RemoteInvoke))
                .MakeGenericMethod(returnType == typeof(void) ? typeof(object) : returnType));

            if (methodInfo.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Callvirt, typeof(Task).GetMethod(nameof(Task.Wait), Type.EmptyTypes));
            }
            else if (!typeof(Task).IsAssignableFrom(methodInfo.ReturnType))
            {
                // ReSharper disable once PossibleNullReferenceException
                il.Emit(OpCodes.Callvirt, typeof(Task<>).MakeGenericType(returnType).GetProperty("Result").GetMethod);
            }

            il.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder, methodInfo);

            return true;
        }
        private readonly ConcurrentDictionary<Guid, ConstructorInfo> types = new ConcurrentDictionary<Guid, ConstructorInfo>();
        private readonly Lazy<ModuleBuilder> proxyModule = new Lazy<ModuleBuilder>(() =>
        {
            var assemblyName = new AssemblyName("ProxyFactory.Types");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            return assemblyBuilder.DefineDynamicModule("Main");
        });
        private readonly Type[] ctorParams = { typeof(IInterceptor) };
        private void generateCtor(TypeBuilder typeBuilder, FieldBuilder interceptorField, ConstructorInfo ctor)
        {
            var baseTypeParams = ctor.GetParameters();

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                ctorParams.Concat(baseTypeParams.Select(param => param.ParameterType)).ToArray());

            var il = ctorBuilder.GetILGenerator();

            //call base class ctor
            il.Emit(OpCodes.Ldarg_0);
            for (var index = 0; index < baseTypeParams.Length; index++)
            {
                il.Emit(OpCodes.Ldarg, index + 2);
            }
            il.Emit(OpCodes.Call, ctor);

            //this.interceptor = interceptor;
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, interceptorField);

            //end of ctor
            il.Emit(OpCodes.Ret);
        }

        public object GenerateProxy(IInterceptor interceptor, ConstructorInfo ctor, params object[] arguments) =>
            types.GetOrAdd(getHashId(ctor), key => generateType(key, interceptor, ctor)).Invoke(new object[]
            {
                interceptor
            }.Concat(arguments).ToArray());
        public static IGenerateProxies Resolve(string name) => Instance;
        public static readonly IGenerateProxies Instance = new DefaultProxyFactory();
        public string Name { get; } = $"{nameof(DefaultProxyFactory)}@{typeof(DefaultProxyFactory).Assembly.GetName().Version}";
    }
}
