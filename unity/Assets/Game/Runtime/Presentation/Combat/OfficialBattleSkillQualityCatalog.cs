using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleSkillQualityBinding
    {
        public int quality;
        public Sprite sprite;
        public Vector2 sourceSize, trimOffset, trimSize, uvMin, uvMax;
    }

    /// <summary>GameSkillIcon.Init's original item Quality -> LanguageUtils.GetItemQualityImage binding.</summary>
    public sealed class OfficialBattleSkillQualityCatalog : ScriptableObject
    {
        [Serializable] private sealed class Item { public int itemId, quality; }
        [Serializable] private sealed class Payload { public string manifest; public Item[] items; }
        [Serializable] private sealed class Receipt { public string manifest, output_sha256; }
        [SerializeField] private TextAsset itemQualities;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private BattleSkillQualityBinding[] qualities;
        private Dictionary<int, int> itemMap;

        public static OfficialBattleSkillQualityCatalog Load()
        {
            var result = Resources.Load<OfficialBattleSkillQualityCatalog>("Combat/OfficialBattleSkillQualityCatalog");
            if (result == null) throw new InvalidOperationException("Official skill quality catalog is missing.");
            result.EnsureLoaded();
            return result;
        }

        public Sprite Find(int itemId)
        {
            EnsureLoaded();
            if (!itemMap.TryGetValue(itemId, out int quality))
                throw new InvalidOperationException("Official skill item quality is missing: " + itemId);
            return FindQuality(quality);
        }

        public Sprite FindQuality(int quality)
        {
            return Binding(quality).sprite;
        }

        public void Apply(Image image, int itemId)
        {
            EnsureLoaded();
            if (!itemMap.TryGetValue(itemId, out int quality))
                throw new InvalidOperationException("Official skill item quality is missing: " + itemId);
            BattleSkillQualityBinding binding = Binding(quality);
            image.sprite = binding.sprite;
            var geometry = image.GetComponent<OfficialBattleSkillQualityGeometry>();
            if (geometry == null) geometry = image.gameObject.AddComponent<OfficialBattleSkillQualityGeometry>();
            geometry.Bind(binding);
        }

        private BattleSkillQualityBinding Binding(int quality)
        {
            EnsureLoaded();
            foreach (BattleSkillQualityBinding entry in qualities)
                if (entry.quality == quality && entry.sprite != null) return entry;
            throw new InvalidOperationException("Official skill quality sprite is not restored: " + quality);
        }

        private void EnsureLoaded()
        {
            if (itemMap != null) return;
            if (itemQualities == null || sourceManifest == null || qualities == null || qualities.Length != 6)
                throw new InvalidOperationException("Official skill quality bindings are incomplete.");
            var receipt = JsonUtility.FromJson<Receipt>(sourceManifest.text);
            var data = JsonUtility.FromJson<Payload>(itemQualities.text);
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(itemQualities.text)))
                    .Replace("-", "").ToLowerInvariant();
            if (receipt == null || receipt.manifest != "854_156383" || receipt.output_sha256 != hash
                || data == null || data.manifest != receipt.manifest || data.items == null)
                throw new InvalidOperationException("Official skill quality source hashes differ.");
            var loaded = new Dictionary<int, int>();
            foreach (Item item in data.items) loaded.Add(item.itemId, item.quality);
            itemMap = loaded;
        }
    }
}
