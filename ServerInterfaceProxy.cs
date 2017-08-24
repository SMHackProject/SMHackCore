namespace SMHackCore {
    using System;
    using System.Linq;

    public class ServerInterfaceProxy {
        protected readonly string Plugin;

        public ServerInterfaceProxy(string plugin) { Plugin = plugin; }

        public void DoInject(int pid) { InjectionEntryPoint.Server.DoInject(pid); }

        protected object ConvertMessage(object message) {
            var type = message.GetType();
            if (message is string || type.IsPrimitive)
                return message;
            return message.AsDictionary();
        }

        public virtual void DoLog(params object[] messages) {
            foreach (var packet in from message in messages
                                   select new ServerInterface.PluginLogPacket(ConvertMessage(message), Plugin))
                InjectionEntryPoint.PacketsCache.Add(packet);
        }

        public virtual void ReportException(Exception ex) {
            InjectionEntryPoint.Server.DoLog(new ServerInterface.PluginLogPacket(ex, Plugin));
        }
    }
}