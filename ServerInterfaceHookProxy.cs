namespace SMHackCore {
    using System;
    using System.Linq;

    public class ServerInterfaceHookProxy : ServerInterfaceProxy {
        protected readonly string Module;
        protected readonly string Symbol;

        public ServerInterfaceHookProxy(string plugin, string module, string symbol) : base(plugin) {
            Module = module;
            Symbol = symbol;
        }

        public override void DoLog(params object[] messages) {
            foreach (var packet in from message in messages
                                   select new ServerInterface.HookLogPacket(
                                       ConvertMessage(message),
                                       Plugin,
                                       Module,
                                       Symbol))
                InjectionEntryPoint.PacketsCache.Add(packet);
        }

        public override void ReportException(Exception ex) {
            InjectionEntryPoint.Server.DoLog(new ServerInterface.HookLogPacket(ex, Plugin, Module, Symbol));
        }
    }
}