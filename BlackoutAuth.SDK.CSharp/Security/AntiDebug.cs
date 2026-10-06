using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlackoutAuth.SDK.CSharp.Security
{
    public static class AntiDebug
    {
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        private static readonly string[] BadProcesses = new[]
        {
            "x64dbg", "x32dbg", "ida", "ida64", "cheatengine", "httpdebugger", "fiddler",
            "wireshark", "dnspy", "de4dot", "processhacker", "scylla", "ilspy"
        };

        public static bool CheckDebugger()
        {
            try
            {
                // Managed debugger check
                if (Debugger.IsAttached) return true;

                // Win32 API checks
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (IsDebuggerPresent()) return true;

                    bool isRemote = false;
                    CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemote);
                    if (isRemote) return true;
                }
            }
            catch { }

            return false;
        }

        public static bool CheckBlacklistedProcesses()
        {
            try
            {
                var running = Process.GetProcesses();
                foreach (var proc in running)
                {
                    string name = proc.ProcessName.ToLowerInvariant();
                    foreach (var bad in BadProcesses)
                    {
                        if (name.Contains(bad))
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
