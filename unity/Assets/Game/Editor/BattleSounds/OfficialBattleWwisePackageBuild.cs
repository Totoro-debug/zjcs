using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;

namespace Zjcs.Editor
{
    // Copy raw packages after BuildPlayer; do not duplicate them in the source
    // StreamingAssets tree or pass native package bytes through Unity importers.
    public sealed class OfficialBattleWwisePackageBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        [Serializable] private sealed class Entry { public string file, bank; }
        [Serializable] private sealed class Catalog { public string manifest, sdk_version; public Entry[] packages; }
        [Serializable] private sealed class Output { public string path, sha256; public long bytes; }
        [Serializable] private sealed class Receipt { public string manifest; public Output[] outputs; }
        private const string Prefix = "unity/" + OfflineBattleWwisePackages.AssetDirectory + "/";
        public int callbackOrder => 10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneWindows64) ValidatePackages();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            string player = Path.GetFullPath(report.summary.outputPath);
            string destination = Path.Combine(Path.GetDirectoryName(player),
                Path.GetFileNameWithoutExtension(player) + "_Data", "StreamingAssets",
                OfflineBattleWwisePackages.StreamingDirectory);
            Directory.CreateDirectory(destination);
            var packages = ValidatePackages();
            foreach (Output output in packages)
            {
                string name = output.path.Substring(Prefix.Length);
                string target = Path.Combine(destination, name);
                string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.Copy(Path.Combine(OfflineBattleWwisePackages.DirectoryPath, name), temporary);
                    Verify(temporary, output);
                    if (File.Exists(target)) File.Replace(temporary, target, null);
                    else File.Move(temporary, target);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            Debug.Log("[Battle] Bound all " + packages.Count + " original Windows Wwise packages to " + destination);
        }

        private static List<Output> ValidatePackages()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/battle-animation/windows-sound-runtime-source-manifest.json")));
            var asset = Resources.Load<TextAsset>("Combat/Wwise/catalog");
            if (asset == null) throw new BuildFailedException("Original sound package catalog is missing.");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (receipt.manifest != "884_156692" || receipt.outputs == null
                || catalog.manifest != receipt.manifest || catalog.sdk_version != "2022.1.18.8567"
                || catalog.packages == null || catalog.packages.Length == 0)
                throw new BuildFailedException("Inspect changed original sound package source/version.");
            var expected = new Dictionary<string, Output>(StringComparer.OrdinalIgnoreCase);
            foreach (Output output in receipt.outputs)
                if (output.path.StartsWith(Prefix, StringComparison.Ordinal) && output.path.EndsWith(".pck", StringComparison.Ordinal))
                    expected.Add(output.path.Substring(Prefix.Length), output);
            if (expected.Count != catalog.packages.Length)
                throw new BuildFailedException("Original sound package catalog and source receipt disagree.");
            var result = new List<Output>();
            bool minimum = false;
            foreach (Entry entry in catalog.packages)
            {
                string name = entry.file;
                if (string.IsNullOrEmpty(name) || name != Path.GetFileName(name)
                    || name != entry.bank + ".pck" || !expected.TryGetValue(name, out Output output))
                    throw new BuildFailedException("Unbound original sound package: " + name);
                expected.Remove(name);
                minimum |= entry.bank == "Minimum";
                Verify(Path.Combine(OfflineBattleWwisePackages.DirectoryPath, name), output);
                result.Add(output);
            }
            if (!minimum || expected.Count != 0)
                throw new BuildFailedException("The original Minimum/Init package chain is incomplete.");
            return result;
        }

        private static void Verify(string file, Output expected)
        {
            if (!File.Exists(file) || new FileInfo(file).Length != expected.bytes)
                throw new BuildFailedException("Missing or truncated original sound package: " + file);
            using (var stream = File.OpenRead(file))
            using (var algorithm = SHA256.Create())
            {
                string hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (hash != expected.sha256)
                    throw new BuildFailedException("Changed original sound package: " + file);
            }
        }
    }
}
