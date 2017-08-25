namespace SMHackCore {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using EasyHook;

    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public class InjectionEntryPoint : IEntryPoint {
        public static readonly Process Process = Process.GetCurrentProcess();
        internal static ServerInterface Server;

        internal static BlockingCollection<ServerInterface.ClientLogPacket> PacketsCache =
            new BlockingCollection<ServerInterface.ClientLogPacket>();

        private readonly List<LocalHook> _localHooks = new List<LocalHook>();
        private readonly List<IPlugin> _plugins = new List<IPlugin>();

        internal readonly string[] SearchPaths;

        // ReSharper disable once UnusedParameter.Local
        public InjectionEntryPoint(RemoteHooking.IContext context, string channelName) {
            Server = RemoteHooking.IpcConnectClient<ServerInterface>(channelName);
            SearchPaths = new[] {
                Server.PluginConfigDirectory,
                Path.GetDirectoryName(typeof(InjectionEntryPoint).Assembly.Location)
            };
            LoadPlugins();
            InitAllPlugin();
        }

        public void Run(RemoteHooking.IContext context, string channelName) {
            HookAll();
            StartLoop();
        }

        private static void HandleException(Exception e, bool isCritical = true) {
            try {
                Server.DoLog(new ServerInterface.ClientLogPacket(e));
            } finally {
                if (isCritical)
                    Process.Kill();
            }
        }

        private string FindPlugin(string name) {
            return (
                       from dir in SearchPaths
                       let cmb = Path.Combine(dir, name)
                       where File.Exists(cmb)
                       select cmb
                   ).FirstOrDefault() ??
                   throw new FileNotFoundException();
        }

        private void LoadPlugins() {
            try {
                Server.Connect(Process.Id, Process.MainModule.FileName);
                _plugins.AddRange(
                    from info in Server.PluginConfig.PluginInfos
                    let pluginPath = FindPlugin(info.Path)
                    let pluginAssembly = Assembly.LoadFile(pluginPath)
                    from exportedType in pluginAssembly.GetExportedTypes()
                    where typeof(IPlugin).IsAssignableFrom(exportedType)
                    select Activator.CreateInstance(
                        exportedType,
                        new ServerInterfaceProxy(pluginPath),
                        info.Args) as IPlugin);
            } catch (Exception e) {
                HandleException(e);
            }
        }

        private static string GetPluginName(IPlugin plugin) {
            var type = plugin.GetType();
            return type.Name == "PluginMain" ?
                type.Assembly.GetName().Name :
                $"{type.Assembly.GetName().Name}::{type.Name}";
        }

        private void InitAllPlugin() {
            try {
                foreach (var plugin in _plugins) {
                    PacketsCache.Add(
                        new ServerInterface.PluginLogPacket("load", GetPluginName(plugin)));
                    plugin.Init();
                }
            } catch (Exception e) {
                HandleException(e);
            }
        }

        private void HookAll() {
            try {
                foreach (var bundle in
                    from plugin in _plugins
                    let pluginName = GetPluginName(plugin)
                    from hook in plugin.GetApiHooks()
                    let hookProxy = new ServerInterfaceHookProxy(pluginName, hook.Module, hook.Symbol)
                    let hooked = hook.DelegateFetcher(hookProxy)
                    let localhook = LocalHook.Create(
                        LocalHook.GetProcAddress(hook.Module, hook.Symbol),
                        hooked,
                        hookProxy)
                    select new {
                        localhook,
                        hook,
                        pluginName
                    }) {
                    bundle.localhook.ThreadACL.SetExclusiveACL(new int[1]);
                    _localHooks.Add(bundle.localhook);
                    PacketsCache.Add(
                        new ServerInterface.HookLogPacket(
                            "Hooked",
                            bundle.pluginName,
                            bundle.hook.Module,
                            bundle.hook.Symbol));
                }
            } catch (Exception e) {
                HandleException(e);
            }
        }

        private void StartLoop() {
            Process.Resume();
            AppDomain.CurrentDomain.ProcessExit += delegate {
                lock (this) {
                    Server.DoLog(PacketsCache.ToArray());
                }
            };
            try {
                while (true) {
                    Thread.Sleep(10);
                    lock (this) {
                        var clientLogPackets = PacketsCache.GetConsumingEnumerable().Take(PacketsCache.Count).ToArray();
                        if (clientLogPackets.Length > 0)
                            Server.DoLog(clientLogPackets);
                        else
                            Server.Ping();
                    }
                    Thread.Sleep(90);
                }
            } catch (Exception ex) {
                Console.WriteLine(ex);
            } finally {
                UnHookAll();
            }
        }

        private void UnHookAll() {
            foreach (var localHook in _localHooks)
                localHook.Dispose();
            _localHooks.Clear();
            LocalHook.Release();
        }
    }
}