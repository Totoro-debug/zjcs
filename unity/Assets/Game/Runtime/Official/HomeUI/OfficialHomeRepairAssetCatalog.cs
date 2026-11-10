using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.HomeUI
{
    // Explicit native prefab bindings; all text and classifications are extracted
    // from the selected original language and HomeSettings, never synthesized.
    public sealed class OfficialHomeRepairAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class Icon { public EFurniture furniture; public GameObject prefab; }
        [Serializable] private sealed class TextEntry { public string key, text; }
        [Serializable] private sealed class Classification { public string TypeNameKey; public int[] FurnitureList; }
        [Serializable] private sealed class Furniture { public int type, itemId; }
        [Serializable] private sealed class RecordedHangUp { public int id, furniture; }
        [Serializable] private sealed class Payload
        {
            public string manifest;
            public TextEntry[] texts;
            public Classification[] classifications;
            public Furniture[] furniture;
            public RecordedHangUp[] recordedHangUps;
        }
        public GameObject repairPrefab;
        public GameObject repairedNotifyPrefab;
        public TextAsset texts;
        public Icon[] icons;
        private Payload payload;
        private Dictionary<string, string> localized;

        public static OfficialHomeRepairAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeRepairAssetCatalog>("Home/OfficialHomeRepairAssetCatalog")
                ?? throw new InvalidOperationException("Original home repair assets are unavailable.");

        private void Load()
        {
            if (payload != null) return;
            var parsed = JsonUtility.FromJson<Payload>(texts.text);
            if (parsed == null || parsed.manifest != "884_156692")
                throw new InvalidOperationException("Unexpected original home repair asset version.");
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in parsed.texts) entries.Add(entry.key, entry.text);
            localized = entries;
            payload = parsed;
        }

        public string Text(string key)
        {
            Load();
            return localized.TryGetValue(key, out string value) ? value : key;
        }

        public string FurnitureTypeNameKey(EFurniture furniture)
        {
            Load();
            foreach (var group in payload.classifications)
                foreach (int type in group.FurnitureList)
                    if (type == (int)furniture) return group.TypeNameKey;
            return string.Empty;
        }

        public int FurnitureItemId(EFurniture furniture)
        {
            Load();
            foreach (var row in payload.furniture)
                if (row.type == (int)furniture) return row.itemId;
            throw new KeyNotFoundException("Original furniture item is unavailable: " + furniture);
        }

        public IEnumerable<int> RecordedHangUpIds(EFurniture furniture)
        {
            Load();
            foreach (var row in payload.recordedHangUps)
                if (row.furniture == (int)furniture) yield return row.id;
        }

        public FurnitureIconWidget IconPrefab(EFurniture furniture)
        {
            foreach (var icon in icons)
                if (icon.furniture == furniture && icon.prefab != null)
                    return icon.prefab.GetComponent<FurnitureIconWidget>()
                        ?? throw new InvalidOperationException("Original furniture icon component is unavailable: " + furniture);
            throw new KeyNotFoundException("Original furniture icon is unavailable: " + furniture);
        }
    }
}
