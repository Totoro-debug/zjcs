using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHangUpWindowsUI
{
    // Explicit bindings to the complete selected original window and its texts.
    public sealed class OfficialHomeHangUpWindowAssetCatalog : ScriptableObject, IProfessionRuleTipData
    {
        [Serializable] private sealed class Entry { public string key, text; }
        [Serializable] private sealed class Rule { public string Key, TitleKey; public string[] TipKeys; }
        [Serializable] private sealed class Payload { public string manifest; public Entry[] texts; public Rule blessingRule; }
        public GameObject bedPrefab, blessingRulePrefab;
        public TextAsset texts;
        public string bedRewardImagePath;
        public Sprite bedRewardImage;
        private Dictionary<string, string> localized;
        private Rule blessingRule;
        public string GetTitleKey(string key) { Text(key); return GetRule(key).TitleKey; }
        public IReadOnlyList<string> GetTipsKey(string key) { Text(key); return GetRule(key).TipKeys; }
        Rule GetRule(string key) => blessingRule != null && blessingRule.Key == key ? blessingRule
            : throw new InvalidOperationException("Selected home rule tip is unavailable: " + key);

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
                blessingRule = payload.blessingRule;
                localized = entries;
            }
            return localized.TryGetValue(key, out var value) ? value : key;
        }
    }
}
