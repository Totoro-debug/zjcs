using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeCartWindowsUI
{
    // Explicit bindings to the complete selected original 903_156948 Cart UI windows,
    // skin 4266 item/weather metadata, equip preview probabilities, audio and texts.
    public sealed class OfficialHomeCartWindowAssetCatalog : ScriptableObject, IProfessionRuleTipData
    {
        [Serializable] public sealed class SpriteBinding { public string path; public Sprite sprite; }
        [Serializable] public sealed class SoundBinding { public uint eventId; public AudioClip clip; }
        [Serializable] public sealed class EquipPreviewEntry { public int hangUpId, itemId, amount; public double probability; }
        [Serializable] public sealed class Skin4266Info
        {
            public int skinItemId, furnitureType, weatherSkinId, order;
            public string prefabName, weatherPrefabName, itemNameKey, itemDescKey;
        }
        [Serializable] private sealed class Entry { public string key, text; }
        [Serializable] private sealed class Rule { public string Key, TitleKey; public string[] TipKeys; }
        [Serializable] private sealed class Payload
        {
            public string manifest;
            public Entry[] texts;
            public Rule cartRule;
            public EquipPreviewEntry[] equipPreviews;
            public Skin4266Info skin4266;
        }

        public GameObject cartDialogPrefab;
        public GameObject hangUpItemPrefab;
        public GameObject hangUpOutputItemPrefab;
        public GameObject itemDetailDialogPrefab;
        public GameObject levelUpPanelPrefab;
        public GameObject levelUpSwitchItemPrefab;
        public GameObject levelUpPropItemPrefab;
        public GameObject ruleDialogPrefab;
        public GameObject previewDialogItemPrefab;
        public GameObject awardPanelPrefab;
        public GameObject awardPanelItemPrefab;
        public ScriptableObject hangUpItemGraphAsset;
        public SpriteBinding[] sprites;
        public Sprite cartSkin4266Icon;
        public Sprite cartSkin4266Preview;
        public SoundBinding[] sounds;
        public TextAsset texts;

        private Dictionary<string, string> localized;
        private Dictionary<string, Sprite> spriteMap;
        private Dictionary<uint, AudioClip> soundMap;
        private Rule cartRule;
        private EquipPreviewEntry[] equipPreviews;
        private Skin4266Info skin4266;

        public static OfficialHomeCartWindowAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeCartWindowAssetCatalog>("Home/OfficialHomeCartWindowAssetCatalog")
                ?? throw new InvalidOperationException("Original home cart window assets are unavailable.");

        private void EnsureLoaded()
        {
            if (localized != null) return;
            if (!texts) throw new InvalidOperationException("Original home cart window data asset is missing.");
            var payload = JsonUtility.FromJson<Payload>(texts.text);
            if (payload == null || payload.manifest != "903_156948")
                throw new InvalidOperationException("Unexpected original home cart window asset version.");

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            if (payload.texts != null)
                foreach (var entry in payload.texts)
                    entries[entry.key] = entry.text;

            var sMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (sprites != null)
                foreach (var binding in sprites)
                    if (!string.IsNullOrEmpty(binding?.path) && binding.sprite != null)
                        sMap[binding.path] = binding.sprite;

            var aMap = new Dictionary<uint, AudioClip>();
            if (sounds != null)
                foreach (var binding in sounds)
                    if (binding != null && binding.eventId != 0 && binding.clip != null)
                        aMap[binding.eventId] = binding.clip;

            cartRule = payload.cartRule;
            equipPreviews = payload.equipPreviews ?? Array.Empty<EquipPreviewEntry>();
            skin4266 = payload.skin4266;
            spriteMap = sMap;
            soundMap = aMap;
            localized = entries;
        }

        public string GetTitleKey(string key)
        {
            EnsureLoaded();
            return cartRule != null && cartRule.Key == key
                ? cartRule.TitleKey
                : throw new InvalidOperationException("Selected home cart rule tip is unavailable: " + key);
        }

        public IReadOnlyList<string> GetTipsKey(string key)
        {
            EnsureLoaded();
            return cartRule != null && cartRule.Key == key
                ? cartRule.TipKeys
                : throw new InvalidOperationException("Selected home cart rule tip is unavailable: " + key);
        }

        public bool ContainsText(string key)
        {
            EnsureLoaded();
            return key != null && localized.ContainsKey(key);
        }

        public string Text(string key)
        {
            EnsureLoaded();
            return key != null && localized.TryGetValue(key, out string value) ? value : key;
        }

        public bool TryGetSprite(string path, out Sprite sprite)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(path) && spriteMap.TryGetValue(path, out sprite)) return true;
            sprite = null;
            return false;
        }

        public bool TryGetSound(uint eventId, out AudioClip clip)
        {
            EnsureLoaded();
            return soundMap.TryGetValue(eventId, out clip);
        }

        public IReadOnlyList<HomeCartEquipPreviewItem> GetEquipPreviews(int hangUpId)
        {
            EnsureLoaded();
            var list = new List<HomeCartEquipPreviewItem>();
            foreach (var entry in equipPreviews)
                if (entry.hangUpId == hangUpId)
                    list.Add(new HomeCartEquipPreviewItem(entry.itemId, entry.probability, entry.amount));
            return list;
        }

        public Skin4266Info GetSkin4266Info()
        {
            EnsureLoaded();
            return skin4266;
        }
    }
}
