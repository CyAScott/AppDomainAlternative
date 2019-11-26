using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EnvDTE;
using OsProcess = System.Diagnostics.Process;

namespace VsDebugger
{
    public static class Program
    {
        public static int Main(string[] commandArgs)
        {
            var args = commandArgs
                .Where(arg => arg.Length > 0 && arg[0] == '-')
                .Select(arg => arg.Split(new [] { ':' }, 2))
                .ToDictionary(arg => arg[0].Substring(1).Trim(), arg => arg.Length == 1 ? "" : arg[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (!args.TryGetValue("pid", out var pidStr) || !int.TryParse(pidStr, out var pid))
            {
                Console.WriteLine("Missing command line argument \"-pid:...\" with the target process id.");
                return 2;
            }

            DTE dte;
            string dteVersion = null;
            try
            {
                if (!args.TryGetValue("dtev", out dteVersion))
                {
                    using (var process = new OsProcess())
                    {
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.FileName = "vsWhere";
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();

                        var output = process.StandardOutput.ReadToEnd();

                        process.WaitForExit();

                        using (var reader = new StringReader(output))
                        {
                            var regex = new Regex(@"^\s*installationVersion\s*:\s*(?<major>\d+)(\.\d+)*$", RegexOptions.IgnoreCase);
                            while (reader.Peek() > -1)
                            {
                                var match = regex.Match(reader.ReadLine() ?? "");
                                if (match.Success)
                                {
                                    dteVersion = $"{match.Groups["major"].Value}.0";
                                    break;
                                }
                            }
                        }
                    }

                    return 3;
                }
                dte = (DTE)Marshal.GetActiveObject($"VisualStudio.DTE.{dteVersion}");
            }
            catch
            {
                Console.WriteLine($"Unable to find the DTE version \"VisualStudio.DTE.{dteVersion}\". Try providing a different name with the argument \"-dtev:\\d.\\d\".");
                return 3;
            }

            AttachMode attachMode;
            try
            {
                if (!args.TryGetValue("debugger", out var mode) || string.Equals(mode, "attach", StringComparison.OrdinalIgnoreCase))
                {
                    attachMode = AttachMode.Attach;
                }
                else if (string.Equals(mode, "detach", StringComparison.OrdinalIgnoreCase))
                {
                    attachMode = AttachMode.Detach;
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                Console.WriteLine($"Unable to attach debugger to process ID: {pid}");
                return 4;
            }

            int parentProcessId;
            try
            {
                if (!args.TryGetValue("ppid", out var parentProcessIdStr) || !int.TryParse(parentProcessIdStr, out parentProcessId))
                {
                    parentProcessId = -1;
                }
            }
            catch
            {
                parentProcessId = -1;
            }

            MessageFilter.Register();

            Process target;
            try
            {
                if (attachMode == AttachMode.Detach)
                {
                    target = dte.Debugger.DebuggedProcesses.OfType<Process>().FirstOrDefault(process => process.ProcessID == pid);
                }
                else
                {
                    Console.WriteLine($"Ppid: {parentProcessId}");
                    var parentProcess = parentProcessId == -1 ? null :
                        dte.Debugger.DebuggedProcesses.OfType<Process>().FirstOrDefault(process => process.ProcessID == parentProcessId) ??
                        dte.Debugger.LocalProcesses.OfType<Process>().FirstOrDefault(process => process.ProcessID == parentProcessId);

                    target = (parentProcess?.Parent?.LocalProcesses ?? dte.Debugger.LocalProcesses).OfType<Process>().FirstOrDefault(process => process.ProcessID == pid);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"Unable to find processes to debug: {error}");
                return 5;
            }

            if (target == null)
            {
                Console.WriteLine($"Unable to find process ID: {pid}");
                return 6;
            }

            try
            {
                if (attachMode == AttachMode.Attach)
                {
                    target.Attach();
                }
                else
                {
                    target.Detach(false);
                }
            }
            catch
            {
                Console.WriteLine($"Unable to attach/detach the debugger to/from process ID: {pid}");
                return 7;
            }

            MessageFilter.Revoke();

            return 1;
        }
    }

    public enum AttachMode
    {
        Attach,
        Detach
    }

    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    public class MessageFilter : IOleMessageFilter
    {
        private const int handled = 0, retryAllowed = 2, retry = 99, cancel = -1, waitAndDispatch = 2;

        int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) => handled;

        int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) => dwRejectType == retryAllowed ? retry : cancel;

        int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) => waitAndDispatch;

        public static void Register() => CoRegisterMessageFilter(new MessageFilter());

        public static void Revoke() => CoRegisterMessageFilter(null);

        public static void CoRegisterMessageFilter(IOleMessageFilter newFilter) => CoRegisterMessageFilter(newFilter, out _);

        [DllImport("Ole32.dll")]
        public static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
    }

}
