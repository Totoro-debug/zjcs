using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Offline.SkillAssets;

namespace Zjcs.Editor.Skills
{
    public static class OfficialSkillUiPackBuild
    {
        private const string ReceiptPath = "assets/reference/skill-ui-native-data-source-manifest.json";
        private const string UnityVersion = "2022.3.57f1c2";
        private const string DeliveryPath = "Zjcs_Data/StreamingAssets/Skills/skill-ui-win64";
        [Serializable] private sealed class Input { public string path, sha256, meta_sha256; public long bytes; }
        [Serializable] private sealed class Output { public string path, sha256; public long bytes; }
        [Serializable] private sealed class Receipt
        {
            public int schema = 1;
            public string unity_version = UnityVersion, target = "StandaloneWindows64";
            public string conversion = "Unity native TextAssets; deterministic bundle with chunk-based LZ4; JSON and original art references are unchanged.";
            public Input[] inputs;
            public Output output;
        }

        public static void RecordValidationPack()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Prepare(workspace, true);
        }

        public static string Prepare(string workspace) => Prepare(workspace, false);

        public static byte[] PrepareForDelivery(string workspace)
        {
            byte[] bytes = File.ReadAllBytes(Prepare(workspace));
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var expected = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(repository, ReceiptPath)));
            if (expected?.output == null || bytes.LongLength != expected.output.bytes || Digest(bytes) != expected.output.sha256)
                throw new BuildFailedException("The captured native UI pack differs from its recorded hash.");
            return bytes;
        }

        public static void ValidateDeliveryLifetime()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(workspace) || !Path.IsPathRooted(workspace))
                throw new BuildFailedException("Use an absolute validation workspace.");
            string test = Path.Combine(workspace, "skill-ui-delivery-" + Guid.NewGuid().ToString("N"));
            byte[] bytes = PrepareForDelivery(test);
            string original = Path.Combine(test, "SkillUiBundle", OfficialSkillUiData.BundleFileName);
            File.Delete(original);
            string executable = Path.Combine(test, "Player", "Zjcs.exe");
            Deliver(bytes, executable);
            string target = DeliveryTarget(executable);
            var bundle = AssetBundle.LoadFromFile(target);
            if (bundle == null) throw new BuildFailedException("The captured UI delivery is not a loadable native bundle.");
            try
            {
                if (bundle.GetAllAssetNames().Length != 2 || File.Exists(original))
                    throw new BuildFailedException("The native UI delivery lifetime scenario did not exercise source removal.");
            }
            finally { bundle.Unload(true); }
            Debug.Log("[Skills] Captured native UI delivery passed after removing its temporary source bundle.");
        }

        private static string Prepare(string workspace, bool record)
        {
            if (Application.unityVersion != UnityVersion || string.IsNullOrEmpty(workspace) || !Path.IsPathRooted(workspace))
                throw new BuildFailedException("Use the pinned Unity version and an absolute validation workspace.");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrWhiteSpace(repository) || !Path.IsPathRooted(repository))
                throw new BuildFailedException("The native skill UI build requires the source repository root.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var inputs = new[] { OfficialSkillUiData.LayoutAssetPath, OfficialSkillUiData.ManifestAssetPath }.Select(path => {
                byte[] bytes = File.ReadAllBytes(Path.Combine(project, path));
                string hash = Digest(bytes), metaHash = Digest(File.ReadAllBytes(Path.Combine(project, path + ".meta")));
                if (hash != Digest(File.ReadAllBytes(Path.Combine(repository, "unity", path)))
                    || metaHash != Digest(File.ReadAllBytes(Path.Combine(repository, "unity", path + ".meta"))))
                    throw new BuildFailedException("The isolated native UI input differs from the recorded repository input: " + path);
                return new Input { path = path, bytes = bytes.LongLength, sha256 = hash, meta_sha256 = metaHash };
            }).ToArray();
            if (inputs[0].sha256 != OfficialSkillUiData.LayoutSha256)
                throw new BuildFailedException("Rebuild the generated skill UI content identity before packaging.");
            string receiptPath = Path.Combine(repository, ReceiptPath);
            Receipt expected = File.Exists(receiptPath) ? JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath)) : null;
            var actual = new Receipt { inputs = inputs };
            if (!record && (expected == null || expected.schema != 1 || expected.unity_version != UnityVersion
                || expected.target != actual.target || JsonUtility.ToJson(new Receipt { inputs = expected.inputs }) != JsonUtility.ToJson(actual)))
                throw new BuildFailedException("Review and record the changed native skill UI input before building.");
            string folder = Path.Combine(workspace, "SkillUiBundle");
            Directory.CreateDirectory(folder);
            string bundlePath = Path.Combine(folder, OfficialSkillUiData.BundleFileName);
            if (record || !File.Exists(bundlePath) || expected.output == null
                || new FileInfo(bundlePath).Length != expected.output.bytes || Digest(File.ReadAllBytes(bundlePath)) != expected.output.sha256)
            {
                var build = new AssetBundleBuild { assetBundleName = OfficialSkillUiData.BundleFileName,
                    assetNames = inputs.Select(value => value.path).ToArray() };
                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(folder, new[] { build },
                    BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression,
                    BuildTarget.StandaloneWindows64);
                if (manifest == null || !manifest.GetAllAssetBundles().SequenceEqual(new[] { OfficialSkillUiData.BundleFileName })
                    || manifest.GetAllDependencies(OfficialSkillUiData.BundleFileName).Length != 0)
                    throw new BuildFailedException("The native skill UI pack must contain only its two independent TextAssets.");
            }
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null) throw new BuildFailedException("The native Windows skill UI pack cannot be loaded.");
            try
            {
                if (bundle.GetAllAssetNames().Length != inputs.Length) throw new BuildFailedException("Unexpected native UI pack content.");
                foreach (Input input in inputs)
                {
                    TextAsset text = bundle.LoadAsset<TextAsset>(input.path.ToLowerInvariant());
                    if (text == null || Digest(text.bytes) != input.sha256 || text.bytes.LongLength != input.bytes)
                        throw new BuildFailedException("The native UI TextAsset bytes differ from the official converted input: " + input.path);
                }
            }
            finally { bundle.Unload(true); }
            actual.output = new Output { path = DeliveryPath, bytes = new FileInfo(bundlePath).Length,
                sha256 = Digest(File.ReadAllBytes(bundlePath)) };
            if (record) File.WriteAllText(receiptPath, JsonUtility.ToJson(actual, true) + "\n", new UTF8Encoding(false));
            else if (JsonUtility.ToJson(actual) != JsonUtility.ToJson(expected))
                throw new BuildFailedException("The native UI pack output differs from its recorded hash.");
            Debug.Log("[Skills] Verified native UI pack: " + actual.output.bytes + " bytes sha256=" + actual.output.sha256);
            return bundlePath;
        }

        private static string DeliveryTarget(string executable)
            => Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable)
                + "_Data", "StreamingAssets", "Skills", OfficialSkillUiData.BundleFileName);

        public static void Deliver(string bundlePath, string executable) => Deliver(File.ReadAllBytes(bundlePath), executable);

        public static void Deliver(byte[] bytes, string executable)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("A captured native UI pack is required.", nameof(bytes));
            string target = DeliveryTarget(executable);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, bytes);
            if (bytes.LongLength != new FileInfo(target).Length || Digest(bytes) != Digest(File.ReadAllBytes(target)))
                throw new BuildFailedException("The delivered native skill UI pack is truncated or changed.");
            Debug.Log("[Skills] Delivered matching native UI pack: " + target);
        }

        private static string Digest(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
