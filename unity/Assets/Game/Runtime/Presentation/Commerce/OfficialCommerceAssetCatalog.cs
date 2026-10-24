using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.Commerce;

namespace Zjcs.Presentation.Commerce
{
    [Serializable]
    public sealed class CommerceSpriteBinding
    {
        public string sourcePath;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CommerceAudioBinding
    {
        public string eventName;
        public AudioClip clip;
    }

    [CreateAssetMenu(menuName = "Zjcs/Commerce/Official Commerce Asset Catalog")]
    public sealed class OfficialCommerceAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Commerce/OfficialCommerceAssetCatalog";

        [SerializeField] private TextAsset runtimeCatalog;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private CommerceSpriteBinding[] sprites = Array.Empty<CommerceSpriteBinding>();
        [SerializeField] private CommerceAudioBinding[] audio = Array.Empty<CommerceAudioBinding>();
        [SerializeField] private Font chineseFont;
        [SerializeField] private TMP_FontAsset chineseTmpFont;
        [SerializeField] private AnimationClip accumulatedPayPanelShow;
        private OfficialCommerceCatalogData data;

        public static OfficialCommerceAssetCatalog LoadDefault()
        {
            OfficialCommerceAssetCatalog catalog = Resources.Load<OfficialCommerceAssetCatalog>(DefaultResourcePath);
            if (catalog == null) throw new InvalidOperationException("Official commerce assets are not installed.");
            catalog.ValidateBindings();
            return catalog;
        }

        public Sprite FindSprite(string sourcePath)
        {
            string name = System.IO.Path.GetFileName(sourcePath ?? string.Empty);
            foreach (CommerceSpriteBinding binding in sprites ?? Array.Empty<CommerceSpriteBinding>())
                if (binding != null && (binding.sourcePath == name
                    || binding.sourcePath.StartsWith(name + "-", StringComparison.Ordinal))) return binding.sprite;
            return null;
        }

        public AnimationClip AccumulatedPayPanelShow => accumulatedPayPanelShow;

        public Sprite FindItemSprite(int itemId)
        {
            CommerceItem item = Load().items.FirstOrDefault(value => value.id == itemId);
            return item == null ? null : FindSprite(item.icon);
        }

        public string LocalizedText(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
            CommerceUiText text = Load().texts.FirstOrDefault(value => value.key == key);
            return text == null ? fallback ?? key : text.value;
        }

        public AudioClip FindAudio(string eventName)
        {
            foreach (CommerceAudioBinding binding in audio ?? Array.Empty<CommerceAudioBinding>())
                if (binding != null && binding.eventName == eventName) return binding.clip;
            return null;
        }

        public Font ChineseFont => chineseFont;
        public TMP_FontAsset ChineseTmpFont => chineseTmpFont;

        public OfficialCommerceCatalogData Load()
        {
            ValidateBindings();
            if (data == null)
            {
                data = JsonUtility.FromJson<OfficialCommerceCatalogData>(runtimeCatalog.text);
                if (data == null || data.manifest != "854_156383" || data.settings == null
                    || data.moneyCosts == null || data.moneyCosts.Length != 43
                    || data.directPay == null || data.directPay.Length != 6
                    || data.dailyGroups == null || data.dailyGroups.Length != 4
                    || data.dailyGifts == null || data.dailyGifts.Length != 12
                    || data.weeklyGroups == null || data.weeklyGroups.Length != 85
                    || data.weeklyGifts == null || data.weeklyGifts.Length != 595
                    || data.vouchers == null || data.vouchers.Length != 6
                    || data.items == null || data.items.Length != 172
                    || data.audio == null || data.audio.Length != 10
                    || data.texts == null || data.texts.Length < 700
                    || data.prefabs == null || data.prefabs.Length < 26
                    || data.prefabs.All(value => value.key != "commerce_functions")
                    || data.prefabs.All(value => value.key != "commerce_shop_entry")
                    || data.prefabs.All(value => value.key != "commerce_activity_entry")
                    || data.prefabs.All(value => value.key != "commerce_first_entry")
                    || data.rules == null || data.rules.Length < 3
                    || string.IsNullOrEmpty(data.sourceHashes?.officialUiJson)
                    || data.firstAwards == null || data.firstAwards.Length != 3
                    || data.accumulatedAwards == null || data.accumulatedAwards.Length != 75)
                    throw new FormatException("The official commerce catalog is incomplete or from an unsupported manifest.");
                foreach (CommercePrefab prefab in data.prefabs)
                    prefab.root = OfficialCommerceCatalogView.BuildTree(prefab);
                ValidateSpriteReferences(data);
            }
            return data;
        }

        private void ValidateSpriteReferences(OfficialCommerceCatalogData catalogData)
        {
            var required = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (CommerceItem item in catalogData.items)
                required.Add(System.IO.Path.GetFileName(item.icon));
            foreach (CommerceDirectPay item in catalogData.directPay ?? Array.Empty<CommerceDirectPay>())
                required.Add(System.IO.Path.GetFileName(item.icon));
            foreach (CommercePrefab prefab in catalogData.prefabs)
                CollectSprites(prefab.root, required);
            foreach (string file in required)
                if (FindSprite(file) == null)
                    throw new InvalidOperationException("An official commerce sprite is not bound: " + file);
        }

        private static void CollectSprites(CommerceUiNode node, System.Collections.Generic.HashSet<string> result)
        {
            if (node == null) return;
            if (node.has_image && node.image != null && node.image.sprite != null && !string.IsNullOrEmpty(node.image.sprite.file))
                result.Add(System.IO.Path.GetFileName(node.image.sprite.file));
            foreach (CommerceUiNode child in node.children ?? Array.Empty<CommerceUiNode>())
                CollectSprites(child, result);
        }

        private void ValidateBindings()
        {
            if (runtimeCatalog == null || sourceManifest == null || sprites == null || sprites.Length == 0
                || audio == null || audio.Length != 10 || chineseFont == null || chineseTmpFont == null)
                throw new InvalidOperationException("Official commerce TextAssets and image bindings are incomplete.");
            foreach (CommerceSpriteBinding binding in sprites)
                if (binding == null || string.IsNullOrEmpty(binding.sourcePath) || binding.sprite == null)
                    throw new InvalidOperationException("An official commerce sprite binding is missing.");
            foreach (CommerceAudioBinding binding in audio)
                if (binding == null || string.IsNullOrEmpty(binding.eventName) || binding.clip == null)
                    throw new InvalidOperationException("An official commerce audio binding is missing.");
        }
    }

}

