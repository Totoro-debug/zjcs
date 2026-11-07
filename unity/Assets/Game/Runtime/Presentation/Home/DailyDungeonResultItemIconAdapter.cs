using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonResultItemIconAdapter : MonoBehaviour
    {
        private static OfficialCharacterAssetCatalog itemAssets;
        private static DailyDungeonRewardAssetCatalog rewardAssets;

        public void Configure(HomeFeatureRuntime host, int itemId, long count)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (itemId <= 0 || count <= 0) throw new ArgumentOutOfRangeException(nameof(itemId));

            Transform content = FindPath(transform, "Content");
            Image quality = FindPath(content, "QualityImg")?.GetComponent<Image>();
            Image icon = FindPath(content, "mask/icon")?.GetComponent<Image>();
            Transform countRoot = FindPath(content, "BgCountText");
            TMP_Text countText = FindPath(content, "BgCountText/CountText")?.GetComponent<TMP_Text>();
            if (quality == null || icon == null || countRoot == null || countText == null)
                throw new InvalidOperationException("Official daily dungeon item icon bindings are incomplete.");

            rewardAssets ??= DailyDungeonRewardAssetCatalog.LoadDefault();
            DailyDungeonRewardAssetCatalog.Item item = rewardAssets.Find(itemId);
            if (item == null)
                throw new InvalidOperationException("Official daily dungeon reward binding is missing: " + itemId);
            itemAssets ??= OfficialCharacterAssetCatalog.LoadDefault();
            quality.sprite = itemAssets.FindQualityBackgroundSprite(item?.quality ?? 0);
            quality.enabled = quality.sprite != null;
            icon.sprite = item.sprite;
            if (icon.sprite == null)
                throw new InvalidOperationException("Official daily dungeon reward icon is missing: " + itemId);
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;

            bool showCount = count > 1 && item.itemType != 110;
            countRoot.gameObject.SetActive(showCount);
            countText.text = showCount ? count.ToString() : string.Empty;
            Transform bottomText = FindPath(content, "BottomText");
            if (bottomText != null) bottomText.gameObject.SetActive(false);

            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.interactable = false;
        }

        private static Transform FindPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            return root.Find(path);
        }
    }
}
