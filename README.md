# AppDomainAlternative

The [AppDomain](https://docs.microsoft.com/en-us/dotnet/api/system.appdomain) SandBox features were intentionally left out of .Net Core for several [technical and security reasons](https://devblogs.microsoft.com/dotnet/porting-to-net-core/). Microsoft's recommendation for .Net Core SandBoxing is to use [process](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process) isolation instead of AppDomain isolation and to use [inter-process communication (IPC)](https://docs.microsoft.com/en-us/windows/desktop/ipc/interprocess-communications) classes (ie pipes, sockets, etc.) instead of remoting classes like the [MarshalByRefObject](https://docs.microsoft.com/en-us/dotnet/api/system.marshalbyrefobject) class for data communication between those processes. AppDomainAlternative is a .Net Core friendly alternative to using AppDomains as SandBoxes and uses the recommended solution of process isolation and supports object remoting over [Anonymous Pipes](https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-use-anonymous-pipes-for-local-interprocess-communication).

### Important Differences between  AppDomains and Processes

1) All AppDomains exist under one process so unhandled exceptions from any AppDomain terminates the entire process and all AppDomains within it. However, processes do not work like that and if one process has an unhandled exception only that process terminates and not the parent or child processes. Although it is possible to replicate this feature between processes, AppDomainAlternative does not replicate this feature because it is not a desired feature for proper SandBoxing.

2) AppDomains do not have a “main” method (like processes) that is executed when the AppDomain is created. A process’s “main” method always executes immediately when the process is created and is intrinsic to how processes work. It is possible to support this feature by creating a generic launcher assembly that executes its “main” method when the process starts and waits for instructions from the parent process to do work like AppDomains do. However, this is a complex feature that will be developed in a later release of AppDomainAlternative.

3) AppDomains can load [Dynamic Assemblies](https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/emitting-dynamic-methods-and-assemblies) generated during run time by another AppDomain. Dynamic assembly run time generation via Emit is still supported in .Net Core but the [assembly serialization features are not](https://github.com/dotnet/corefx/issues/4491#issuecomment-189756092). Since those assemblies cannot be serialized, they cannot be passed from one process to another for execution. [ILPack](https://github.com/Lokad/ILPack) looks like a good choice to fill in the feature gap, but it has not been released yet.

### Remoting Alternative

Remoting was an important feature for AppDomains and [is no longer supported](https://docs.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable#remoting) in .Net Core. Which means classes that inherit from [MarshalByRefObject](https://docs.microsoft.com/en-us/dotnet/api/system.marshalbyrefobject) can no longer be used for remoting from one domain to another. Classes that inherit from MarshalByRefObject work by creating a proxy instance on a remote domain and all calls to that proxy instance are serialized and passed to the original domain for execution. Here is an example class that can be proxied across the domain barrier:

```
public class ChatRoom : MarshalByRefObject
{
	public void SendMessage(string message)
	{
		Console.WriteLine(message);
	}
}
```

AppDomainAlternative takes a similar approach to remoting across the process barrier. However, there is no need to inherit from a special class. Here is an example class that can be proxied across the process barrier:

```
public class ChatRoom
{
	public virtual void SendMessage(string message)
	{
		Console.WriteLine(message);
	}
}
```

The difference is the methods and properties need to be marked as [virtual](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/virtual). This allows AppDomainAlternative to create a proxy instance of the class by overriding those methods and properties. The override of those methods and properties handle the remoting responsibilities, include exceptions thrown by the remote call.

#### AppDomain Alternative

Creating a child AppDomain is similar to starting a child process with AppDomainAlternative. Below is an example on how to create a child AppDomain and using the `ChatRoom` class for remoting:

```
var domain = AppDomain.CreateDomain("Some Name");
var chatRoom = (ChatRoom)domain.CreateInstanceAndUnwrap(typeof(ChatRoom).Assembly.FullName, typeof(ChatRoom).FullName);
chatRoom.SendMessage("Hello World");
```

That example creates an instance on the child domain and returns a proxied instance of that class to the parent domain for remoting. Below is an example of how to do the same thing with AppDomainAlternative:

```
var domain = Domains.Current.AddChildDomain(new ProcessStartInfo("dotnet", "path to .Net Core assembly"));
var chatRoom = (ChatRoom)await childDomain.Channels.CreateInstance(typeof(ChatRoom).GetConstructors().First(), hostInstance: false).ConfigureAwait(false);
chatRoom.SendMessage("Hello World");
```

You may notice that AppDomainAlternative does offer additional features that are not included in remoting for AppDomains. One of those features is it supports constructors when creating remote objects.
