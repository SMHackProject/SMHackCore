namespace SMHackCore {
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    public static class ProcessExtension {
        [Flags]
        public enum ThreadAccess {
            Terminate = 0x0001,
            SuspendResume = 0x0002,
            GetContext = 0x0008,
            SetContext = 0x0010,
            SetInformation = 0x0020,
            QueryInformation = 0x0040,
            SetThreadToken = 0x0080,
            Impersonate = 0x0100,
            DirectImpersonation = 0x0200
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        private static extern int ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref StartupInfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        public static void Suspend(this Process process) {
            foreach (ProcessThread thread in process.Threads) {
                var pOpenThread = OpenThread(ThreadAccess.SuspendResume, false, (uint) thread.Id);
                if (pOpenThread == IntPtr.Zero)
                    continue;
                SuspendThread(pOpenThread);
            }
        }

        public static void Resume(this Process process) {
            foreach (ProcessThread thread in process.Threads) {
                var pOpenThread = OpenThread(ThreadAccess.SuspendResume, false, (uint) thread.Id);
                if (pOpenThread == IntPtr.Zero)
                    continue;
                ResumeThread(pOpenThread);
            }
        }

        private static string FindIndexedProcessName(int pid) {
            var processName = Process.GetProcessById(pid).ProcessName;
            var processesByName = Process.GetProcessesByName(processName);
            string processIndexdName = null;

            for (var index = 0; index < processesByName.Length; index++) {
                processIndexdName = index == 0 ? processName : processName + "#" + index;
                var processId = new PerformanceCounter("Process", "ID Process", processIndexdName);
                if ((int) processId.NextValue() == pid)
                    return processIndexdName;
            }

            return processIndexdName;
        }

        private static Process FindPidFromIndexedProcessName(string indexedProcessName) {
            var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName);
            return Process.GetProcessById((int) parentId.NextValue());
        }

        public static Process Parent(this Process process) {
            return FindPidFromIndexedProcessName(FindIndexedProcessName(process.Id));
        }


        public struct ProcessInformation {
            public IntPtr HProcess;
            public IntPtr HThread;
            public int DwProcessId;
            public int DwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct StartupInfo {
            public int Cb;
            public string LpReserved;
            public string LpDesktop;
            public string LpTitle;
            public uint DwX;
            public uint DwY;
            public uint DwXSize;
            public uint DwYSize;
            public uint DwXCountChars;
            public uint DwYCountChars;
            public uint DwFillAttribute;
            public uint DwFlags;
            public short WShowWindow;
            public short CbReserved2;
            public IntPtr LpReserved2;
            public IntPtr HStdInput;
            public IntPtr HStdOutput;
            public IntPtr HStdError;
        }

        public struct SecurityAttributes {
            public int Length;
            public IntPtr LpSecurityDescriptor;
            public bool BInheritHandle;
        }
    }
}