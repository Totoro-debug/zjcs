using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Verified native graphs, with scoped migrations following the original catalog.</summary>
    internal static class OfficialBattleNativeGraphSources
    {
        [Serializable] private sealed class Index { public string[] resources = null; }
        [Serializable] private sealed class Source { public string manifest = null; public string output_sha256 = null; }
        private static IReadOnlyList<TextAsset> current;

        internal static TextAsset LoadVerified(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            var metadata = Resources.Load<TextAsset>(resource + "-source-manifest");
            if (asset == null || metadata == null)
                throw new InvalidOperationException("Original native graph source is missing: " + resource);
            var source = JsonUtility.FromJson<Source>(metadata.text);
            var data = JsonUtility.FromJson<Source>(asset.text);
            if (source == null || data == null || string.IsNullOrEmpty(source.manifest)
                || source.manifest != data.manifest || source.output_sha256 == null
                || source.output_sha256.Length != 64)
                throw new FormatException("Original native graph source is inconsistent: " + resource);
            string digest;
            using (var sha = SHA256.Create())
                digest = BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(digest, source.output_sha256, StringComparison.Ordinal))
                throw new FormatException("Original native graph integrity check failed: " + resource);
            return asset;
        }

        internal static IReadOnlyList<TextAsset> LoadAll()
        {
            if (current != null) return current;
            var asset = Resources.Load<TextAsset>("Combat/skill-native-graph-sources");
            var index = asset == null ? null : JsonUtility.FromJson<Index>(asset.text);
            if (index?.resources == null || index.resources.Length == 0
                || index.resources[0] != "Combat/skill-native-graphs")
                throw new FormatException("Original native graph source index is invalid.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<TextAsset>();
            foreach (string resource in index.resources)
            {
                if (string.IsNullOrEmpty(resource) || !seen.Add(resource))
                    throw new FormatException("Original native graph source index contains a duplicate.");
                result.Add(LoadVerified(resource));
            }
            return current = result.AsReadOnly();
        }
    }
}
