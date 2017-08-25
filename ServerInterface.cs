namespace SMHackCore {
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Remoting;
    using System.Runtime.Remoting.Channels.Ipc;
    using System.Text.RegularExpressions;
    using System.Threading;
    using EasyHook;

    public abstract class ServerInterface : MarshalByRefObject {
        protected static readonly Regex CommandLineParser = new Regex(@"(?:[^\""\s]|\""(?:[^\\\""]|\\.)*\"")+");

        protected static readonly Regex CommandLineRegex =
            new Regex(@"^(.+?\.exe)(?:\s+([^\s].+))?$|^(?:""(.+?\.exe)"")(?:\s+([^\s].+))?$");

        public readonly string ChannelName;
        public readonly string InjectionLibrary;
        protected readonly IpcServerChannel ServerChannel;

        protected ServerInterface() {
            InjectionLibrary = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new NullReferenceException(),
                "SMHackCore.dll");
            ServerChannel = RemoteHooking.IpcCreateServer(
                ref ChannelName,
                WellKnownObjectMode.Singleton,
                this);
        }

        public abstract string PluginConfigPath { get; }

        protected static string GetRest(string s, int index) {
            var start = 0;
            for (var i = 0; i < index; i++) {
                var match = CommandLineParser.Match(s, start);
                start = match.Index + match.Length;
            }
            return s.Substring(start).TrimStart();
        }

        protected bool GetCommandLine(string src, out string name, out string args) {
            var match = CommandLineRegex.Match(src);
            name = "";
            args = "";
            if (!match.Success)
                return false;
            name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            args = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
            return true;
        }

        public virtual void DoInject(int id) {
            RemoteHooking.Inject(
                id,
                InjectionOptions.NoService | InjectionOptions.DoNotRequireStrongName,
                InjectionLibrary,
                InjectionLibrary,
                ChannelName);
        }

        public virtual int DoCreateAndInject(string program, string args) {
            var si = new ProcessExtension.StartupInfo();
            var ret = ProcessExtension.CreateProcess(
                program,
                args,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                0x00000016,
                IntPtr.Zero,
                null,
                ref si,
                out var pi);
            if (!ret)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var pid = pi.DwProcessId;
            Thread.Sleep(100);
            DebugActiveProcessStop(pid);
            DoInject(pid);
            return pid;
        }

        public virtual void Ping() { }

        public abstract void Connect(int id, string image);

        public abstract void DoLog(params ClientLogPacket[] packet);

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool DebugActiveProcessStop(int dwProcessId);

        [AttributeUsage(AttributeTargets.Field)]
        public class ClientIgnored : Attribute {
        }

        [Serializable]
        public class ClientLogPacket {
            public object Message;
            [ClientIgnored] public int Pid;
            [ClientIgnored] public DateTime Time;

            public ClientLogPacket(object message) {
                Time = DateTime.Now;
                Pid = InjectionEntryPoint.Process.Id;
                Message = message;
            }
        }

        [Serializable]
        public class PluginLogPacket : ClientLogPacket {
            public string Plugin;

            public PluginLogPacket(object message, string plugin) : base(message) { Plugin = plugin; }
        }

        [Serializable]
        public class HookLogPacket : PluginLogPacket {
            public string Module;
            public string Symbol;

            public HookLogPacket(object message, string plugin, string module, string symbol) : base(message, plugin) {
                Module = module;
                Symbol = symbol;
            }
        }
    }
}