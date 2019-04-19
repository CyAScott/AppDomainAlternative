# AppDomainAlternative

AppDomain Alternative (ADA) is a .Net core sandboxing alternative to [AppDomains](https://docs.microsoft.com/en-us/dotnet/api/system.appdomain) (which was [deprecated in .Net Core](https://devblogs.microsoft.com/dotnet/porting-to-net-core/)). Microsoft's recommendation is to use process isolation for sandboxing and to use [inter-process communication (IPC)](https://docs.microsoft.com/en-us/windows/desktop/ipc/interprocess-communications) classes for data communication between those processes.

ADA manages the creation of child processes and establishes an [Anonymous Pipe](https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-use-anonymous-pipes-for-local-interprocess-communication) connection between the parent and child processes for IPC. In addition, it has an abstraction layer for sharing an object similar to [MarshalByRefObject](https://docs.microsoft.com/en-us/dotnet/api/system.marshalbyrefobject).
