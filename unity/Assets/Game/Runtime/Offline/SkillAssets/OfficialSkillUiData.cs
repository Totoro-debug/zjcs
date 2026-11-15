using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.SkillAssets
{
    // Both catalog getters cross this interface. A valid embedded payload and a
    // separately built native TextAsset pack carry exactly the same official JSON.
    public static partial class OfficialSkillUiData
    {
        public const string BundleFileName = "skill-ui-win64";
        public const string LayoutAssetPath = "Assets/Official/Skills/skill-ui.json";
        public const string ManifestAssetPath = "Assets/Official/Skills/source-manifest.json";
        private static readonly Dictionary<TextAsset, TextAsset> resolved = new Dictionary<TextAsset, TextAsset>();
        private static TextAsset packedLayout, packedManifest;

        [Serializable] private sealed class Receipt { public Entry[] files; }
        [Serializable] private sealed class Entry { public string path, sha256; public long bytes; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            resolved.Clear();
            packedLayout = packedManifest = null;
        }

        public static TextAsset Resolve(TextAsset embedded)
        {
            if (embedded == null) throw new InvalidOperationException("The original skill UI TextAsset is not bound.");
            if (resolved.TryGetValue(embedded, out TextAsset value)) return value;
            if (Digest(embedded.bytes) == LayoutSha256)
            {
                Debug.Log("[Skill UI data] Verified embedded TextAsset: " + LayoutSha256);
                return resolved[embedded] = embedded;
            }
            if (Application.isEditor)
                throw new InvalidOperationException("The imported skill UI and generated content identity differ; reimport the recorded input.");
            LoadNativePack();
            return resolved[embedded] = packedLayout;
        }

        public static TextAsset ResolveManifest(TextAsset embeddedManifest, TextAsset embeddedLayout)
        {
            TextAsset layout = Resolve(embeddedLayout);
            TextAsset manifest = layout == embeddedLayout ? embeddedManifest : packedManifest;
            ValidateManifest(manifest, layout.bytes.LongLength);
            return manifest;
        }

        private static void LoadNativePack()
        {
            if (packedLayout != null) return;
            string path = Path.Combine(Application.streamingAssetsPath, "Skills", BundleFileName);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new InvalidOperationException("The matching local skill UI pack is absent or unreadable: " + path);
            bool retainAssets = false;
            try
            {
                TextAsset layout = bundle.LoadAsset<TextAsset>(LayoutAssetPath.ToLowerInvariant());
                TextAsset manifest = bundle.LoadAsset<TextAsset>(ManifestAssetPath.ToLowerInvariant());
                if (layout == null || Digest(layout.bytes) != LayoutSha256)
                    throw new InvalidOperationException("The native skill UI pack does not match the compiled official content identity.");
                ValidateManifest(manifest, layout.bytes.LongLength);
                packedLayout = layout;
                packedManifest = manifest;
                retainAssets = true;
                Debug.Log("[Skill UI data] Verified native Windows pack: " + LayoutSha256);
            }
            finally { bundle.Unload(!retainAssets); }
        }

        private static void ValidateManifest(TextAsset manifest, long length)
        {
            if (manifest == null) throw new InvalidOperationException("The skill UI source receipt is absent.");
            Receipt receipt = JsonUtility.FromJson<Receipt>(manifest.text);
            Entry[] entries = receipt?.files?.Where(value => value != null && value.path == LayoutAssetPath).ToArray();
            if (entries == null || entries.Length != 1 || entries[0].sha256 != LayoutSha256 || entries[0].bytes != length)
                throw new InvalidOperationException("The native skill UI data and its recorded source receipt disagree.");
        }

        private static string Digest(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
