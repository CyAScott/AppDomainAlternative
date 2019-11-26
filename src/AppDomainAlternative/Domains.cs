using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AppDomainAlternative.Ipc;

namespace AppDomainAlternative
{
    /// <summary>
    /// A wrapper for a <see cref="Process"/>.
    /// </summary>
    public abstract class Domains
    {
        #region Visual Studio Debugger Support

        /// <summary>
        /// The environment variable name to use when finding the correct DTE version to use for attaching the debugger.
        /// </summary>
        public const string VsDteEnvironmentVariableName = "__VsDteEnvironmentVariableName__";

        /// <summary>
        /// Where the VsBugger.exe file is located.
        /// </summary>
        protected static string VsDebuggerLocation { get; }

        /// <summary>
        /// The Visual Studio DTE version to use for debugging.
        /// </summary>
        protected static string DteVersion { get; }

#pragma warning disable 1591
#pragma warning disable IDE1006 // Naming Styles
        // ReSharper disable InconsistentNaming
        // ReSharper disable StringLiteralTypo
        // ReSharper disable UnusedMember.Local
        [DllImport("ole32.dll")]
        internal static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);
#pragma warning restore 1591
#pragma warning restore IDE1006 // Naming Styles
        // ReSharper restore InconsistentNaming
        // ReSharper restore StringLiteralTypo
        // ReSharper restore UnusedMember.Local

        internal static bool TryToAttachDebugger(int pid)
        {
            if (!DebuggingSupported)
            {
                Debug.WriteLine("No Debugger Found");
                return false;
            }

            try
            {
                using (var command = Process.Start(new ProcessStartInfo(VsDebuggerLocation, $"-pid:{pid} -ppid:{Current.Process.Id} -dtev:{DteVersion} -debugger:attach")
                {
                    RedirectStandardOutput = true
                }))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    command.WaitForExit(30000);//wait for 30 seconds to attach the debugger

                    if (!command.HasExited)
                    {
                        command.Kill();
                        throw new TimeoutException("Timed out when trying to attach debugger to process.");
                    }

                    var output = command.StandardOutput.ReadToEnd();

                    if (command.ExitCode != 1)
                    {
                        throw new Exception(output);
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Unable to attach debugger to process ({pid}): {error}");

                return false;
            }
        }

        internal static void DetachDebugger(int pid)
        {
            try
            {
                using (var command = Process.Start(new ProcessStartInfo(VsDebuggerLocation, $"-pid:{pid} -dtev:{DteVersion} -debugger:detach")
                {
                    RedirectStandardOutput = true
                }))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    command.WaitForExit(30000);//wait for 30 seconds to attach the debugger

                    if (!command.HasExited)
                    {
                        command.Kill();
                        throw new TimeoutException("Timed out when trying to attach debugger to process.");
                    }

                    if (command.ExitCode != 1)
                    {
                        throw new Exception(command.StandardOutput.ReadToEnd());
                    }
                }
            }
            catch (Exception error)
            {
                Debug.WriteLine($"Unable to detach debugger to process ({pid}): {error}");
            }
        }

        #endregion

        static Domains()
        {
            Current = new CurrentDomain(Process.GetCurrentProcess());

            try
            {
                VsDebuggerLocation = Path.Combine(Path.GetDirectoryName(typeof(Domains).Assembly.Location) ?? "", "VsDebugger.exe");
                if (!File.Exists(VsDebuggerLocation))
                {
                    VsDebuggerLocation = null;
                    return;
                }

                /* The DTE version for Visual Studio is needed to attach the debugger to a child process.
                 * The best way to find this information is to use the VsWhere utility developed by Microsoft.
                 *
                 * The DTE version can be set manually using an environment variable. This will be useful for
                 * dev machines that have multiple versions of Visual Studio installed.
                 *
                 * This means that attaching the debugger to a child process is only supported on Windows.
                 */
                DteVersion = Environment.GetEnvironmentVariable(VsDteEnvironmentVariableName);

                if (string.IsNullOrEmpty(DteVersion) || CLSIDFromProgID(DteVersion, out var classId) == 0 && classId != Guid.Empty)
                {
                    using (var process = new Process())
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
                                    DteVersion = $"{match.Groups["major"].Value}.0";
                                    break;
                                }
                            }
                        }
                    }
                }

                //confirm the dte version is valid
                if (CLSIDFromProgID($"VisualStudio.DTE.{DteVersion}", out classId) != 0 || classId == Guid.Empty)
                {
                    throw new Exception($"Unable to Find DTE Version \"VisualStudio.DTE.{DteVersion}\". Try setting the environment variable \"{VsDteEnvironmentVariableName}\" to the correct value for this machine.");
                }

                DebuggingSupported = true;
            }
            catch (Exception error)
            {
                DteVersion = null;
                DebuggingSupported = false;

                Debug.WriteLine($"Unable to find debugger: {error}");
            }
        }

        internal Domains()
        {
        }

        /// <summary>
        /// All the open <see cref="IChannel"/>s between the parent and child <see cref="Domains"/>.
        /// </summary>
        public abstract IHaveChannels Channels { get; }

        /// <summary>
        /// The process for the domain.
        /// </summary>
        public abstract Process Process { get; }

        /// <summary>
        /// True if debugging child processes is supported.
        /// </summary>
        public static bool DebuggingSupported { get; }

        /// <summary>
        /// The current domain (aka Process).
        /// </summary>
        public static readonly CurrentDomain Current;
    }
}
