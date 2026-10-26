using System;
using System.Collections.Generic;
using System.IO;

namespace Zjcs.Offline.World.NativeLabelResources
{
    // Local counterpart of only the raw-file handle operations consumed by the
    // original LocalizationBinaryTextService. No network or Android bundle loader.
    public interface ILocalRawFileLease : IDisposable
    {
        string GetRawFilePath();
    }
    public interface ILocalRawFileLoader
    {
        ILocalRawFileLease LoadRawFile(string location);
    }
    public sealed class LocalLanguageFiles : ILocalRawFileLoader
    {
        private readonly IReadOnlyDictionary<string, string> bindings;
        public LocalLanguageFiles(string streamingAssetsPath)
        {
            string root = Path.Combine(streamingAssetsPath, "Map13", "NativeText");
            bindings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Assets/Launch/Config/Binary/Language/zh_CN/text.g.bytes", Path.Combine(root, "launch-zh_CN.bytes") },
                { "Assets/Config/Binary/Language/zh_CN/text.g.bytes", Path.Combine(root, "game-zh_CN.bytes") }
            };
        }
        public ILocalRawFileLease LoadRawFile(string location)
        {
            if (!bindings.TryGetValue(location, out string path)) throw new FileNotFoundException("Unbound local original language file: " + location);
            if (!File.Exists(path)) throw new FileNotFoundException("Packaged original language file is missing.", path);
            return new Lease(path);
        }
        private sealed class Lease : ILocalRawFileLease
        {
            private string path;
            public Lease(string path) { this.path = path; }
            public string GetRawFilePath() => path ?? throw new ObjectDisposedException(nameof(Lease));
            public void Dispose() { path = null; }
        }
    }
}
