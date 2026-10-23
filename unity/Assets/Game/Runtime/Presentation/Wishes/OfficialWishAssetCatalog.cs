using System;
using System.IO;
using TMPro;
using UnityEngine;
using Zjcs.Offline.Commerce;

namespace Zjcs.Presentation.Wishes
{
    [Serializable]
    public sealed class WishUiRuntimeCatalog
    {
        public string manifest;
        public CommercePrefab[] prefabs;
        public WishSpriteAssetBinding[] spriteBindings;
        public string[] spriteFiles;
        public WishUiIcon[] icons;
        public WishResultAnimationSet resultAnimations;
        public WishResultTiming resultTiming;
        public WishAudioAssetBinding[] audio;

        public CommercePrefab Prefab(string key)
        {
            foreach (CommercePrefab value in prefabs ?? Array.Empty<CommercePrefab>())
                if (value != null && value.key == key) return value;
            return null;
        }
    }

    [Serializable]
    public sealed class WishResultAnimationSet
    {
        public string one, onePieces, quality, qualityCut, stars;
        public WishStarAnimationEvent[] starEvents;
    }

    [Serializable]
    public sealed class WishStarAnimationEvent
    {
        public float time;
        public string functionName;
        public int index;
    }

    [Serializable]
    public sealed class WishResultTiming
    {
        public float oneSoundDelayPercent, oneTransDelay, oneFadeInDelay;
        public float tenFadeInDelay, tenTransDelay, tenTransTimeRate;
        public float additionalAwardsDelay, gachaFadeInTime;
        public float hundredShowUpDelay, hundredTransDelay, hundredScrollDelayThread;
        public string hundredScrollCurve;
    }

    [Serializable]
    public sealed class WishSpriteAssetBinding
    {
        public string file;
        public float[] border;
        public float pixelsPerUnit;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class WishUiIcon
    {
        public int id;
        public string file;
    }

    [Serializable]
    public sealed class WishAudioAssetBinding
    {
        public string eventName, file;
        public AudioClip clip;
    }

    [CreateAssetMenu(menuName = "Zjcs/Wishes/Official Wish Asset Catalog")]
    public sealed class OfficialWishAssetCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Wishes/OfficialWishAssetCatalog";

        public TextAsset runtimeCatalog;
        public WishSpriteAssetBinding[] sprites = Array.Empty<WishSpriteAssetBinding>();
        public WishAudioAssetBinding[] audio = Array.Empty<WishAudioAssetBinding>();
        public Font chineseFont;
        public TMP_FontAsset chineseTmpFont;
        private WishUiRuntimeCatalog data;

        public static OfficialWishAssetCatalog LoadDefault()
        {
            OfficialWishAssetCatalog result = Resources.Load<OfficialWishAssetCatalog>(DefaultResourcePath);
            if (result == null) throw new InvalidOperationException("Official wish assets are not installed.");
            result.ValidateBindings();
            return result;
        }

        public WishUiRuntimeCatalog Load()
        {
            ValidateBindings();
            if (data == null)
            {
                data = JsonUtility.FromJson<WishUiRuntimeCatalog>(runtimeCatalog.text);
                if (data == null || data.manifest != "854_156383" || data.prefabs == null
                    || data.prefabs.Length != 19 || data.spriteBindings == null || data.spriteFiles == null
                    || data.spriteBindings.Length != data.spriteFiles.Length
                    || data.Prefab("gacha_shell") == null || data.Prefab("goddess") == null
                    || data.Prefab("gacha_senior") == null || data.Prefab("gacha_result") == null
                    || data.Prefab("gacha_friend") == null || data.Prefab("gacha_ten_reward") == null
                    || data.Prefab("gacha_hundred_row") == null || data.Prefab("gacha_award_icon") == null
                    || data.Prefab("shop_goods") == null || data.resultAnimations == null
                    || !HasWishShellVisualComponents(data.Prefab("gacha_shell"))
                    || data.resultTiming == null || data.resultAnimations.starEvents == null
                    || data.resultAnimations.starEvents.Length != 20
                    || string.IsNullOrEmpty(data.resultAnimations.stars)
                    || string.IsNullOrEmpty(data.resultAnimations.one)
                    || string.IsNullOrEmpty(data.resultAnimations.onePieces)
                    || string.IsNullOrEmpty(data.resultAnimations.quality)
                    || string.IsNullOrEmpty(data.resultAnimations.qualityCut))
                    throw new FormatException("The pinned wish UI catalog is incomplete.");
                foreach (CommercePrefab prefab in data.prefabs)
                {
                    prefab.root = OfficialCommerceCatalogView.BuildTree(prefab);
                    if (prefab.root == null)
                        throw new FormatException("A pinned wish UI prefab has no root: " + prefab.key);
                }
            }
            return data;
        }

        private static bool HasWishShellVisualComponents(CommercePrefab prefab)
        {
            bool hasImage = false;
            bool hasText = false;
            foreach (CommerceUiNode node in prefab?.nodes ?? Array.Empty<CommerceUiNode>())
            {
                hasImage |= node != null && node.has_image && node.image != null && node.image.sprite != null;
                hasText |= node != null && node.has_text && node.text != null;
            }
            return hasImage && hasText;
        }

        public Sprite FindSprite(string sourcePath)
        {
            string file = Path.GetFileName(sourcePath ?? string.Empty);
            foreach (WishSpriteAssetBinding binding in sprites ?? Array.Empty<WishSpriteAssetBinding>())
                if (binding != null && binding.file == file) return binding.sprite;
            return null;
        }

        public Sprite FindItemSprite(int itemId)
        {
            foreach (WishUiIcon icon in Load().icons ?? Array.Empty<WishUiIcon>())
                if (icon != null && icon.id == itemId) return FindSprite(icon.file);
            return null;
        }

        public AudioClip FindAudio(string eventName)
        {
            foreach (WishAudioAssetBinding binding in audio ?? Array.Empty<WishAudioAssetBinding>())
                if (binding != null && binding.eventName == eventName) return binding.clip;
            return null;
        }

        public Font ChineseFont => chineseFont;
        public TMP_FontAsset ChineseTmpFont => chineseTmpFont;

        private void ValidateBindings()
        {
            WishUiRuntimeCatalog source = runtimeCatalog == null ? null
                : JsonUtility.FromJson<WishUiRuntimeCatalog>(runtimeCatalog.text);
            if (source == null || source.spriteFiles == null || sprites == null
                || sprites.Length != source.spriteFiles.Length || chineseFont == null || chineseTmpFont == null)
                throw new InvalidOperationException("Official wish catalog, sprite, or font bindings are incomplete.");
            if (source.prefabs == null || source.prefabs.Length != 19
                || source.Prefab("gacha_friend") == null || source.Prefab("gacha_ten_reward") == null
                || source.Prefab("gacha_hundred_row") == null || source.Prefab("gacha_award_icon") == null
                || source.resultAnimations == null || source.audio == null || source.audio.Length == 0
                || source.resultTiming == null || source.resultAnimations.starEvents == null
                || source.resultAnimations.starEvents.Length != 20
                || string.IsNullOrEmpty(source.resultAnimations.stars)
                || audio == null || audio.Length != source.audio.Length)
                throw new InvalidOperationException("Official Friendship Wish UI is missing.");
            foreach (WishSpriteAssetBinding binding in sprites)
                if (binding == null || string.IsNullOrEmpty(binding.file) || binding.sprite == null)
                    throw new InvalidOperationException("An official wish sprite binding is missing.");
            foreach (WishAudioAssetBinding binding in audio)
                if (binding == null || string.IsNullOrEmpty(binding.eventName)
                    || string.IsNullOrEmpty(binding.file) || binding.clip == null)
                    throw new InvalidOperationException("An official wish audio binding is missing.");
        }
    }
}
