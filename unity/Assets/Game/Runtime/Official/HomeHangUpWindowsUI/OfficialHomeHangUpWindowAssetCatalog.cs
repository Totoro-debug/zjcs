using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.HomeHangUpWindowsUI
{
    // Explicit bindings to the complete selected original window and its texts.
    public sealed class OfficialHomeHangUpWindowAssetCatalog : ScriptableObject
    {
        [Serializable] private sealed class Entry { public string key, text; }
        [Serializable] private sealed class Payload { public string manifest; public Entry[] texts; }
        public GameObject bedPrefab;
        public TextAsset texts;
        public string bedRewardImagePath;
        public Sprite bedRewardImage;
        private Dictionary<string, string> localized;

        public static OfficialHomeHangUpWindowAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeHangUpWindowAssetCatalog>("Home/OfficialHomeHangUpWindowAssetCatalog")
                ?? throw new InvalidOperationException("Original home income window assets are unavailable.");

        public string Text(string key)
        {
            if (localized == null)
            {
                var payload = JsonUtility.FromJson<Payload>(texts.text);
                if (payload == null || payload.manifest != "884_156692")
                    throw new InvalidOperationException("Unexpected original home income window asset version.");
                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in payload.texts) entries.Add(entry.key, entry.text);
                localized = entries;
            }
            return localized.TryGetValue(key, out var value) ? value : key;
        }
    }
}
