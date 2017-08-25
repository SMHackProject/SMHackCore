namespace SMHackCore {
    using System;
    using System.IO;
    using System.Linq;

    [Serializable]
    public class PluginConfig {
        public readonly PluginInfo[] PluginInfos;

        public PluginConfig(string path) {
            PluginInfos = (from line in File.ReadLines(path)
                           where !line.StartsWith("#") && line.Length > 0
                           let splts = line.Split(' ')
                           select new PluginInfo(splts.First(), splts.Skip(1).ToArray())).ToArray();
        }

        [Serializable]
        public class PluginInfo {
            public readonly string[] Args;
            public readonly string Path;

            public PluginInfo(string path, string[] args) {
                Path = path;
                Args = args;
            }
        }
    }
}