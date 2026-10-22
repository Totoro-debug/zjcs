using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;

namespace Zjcs.Presentation.Commerce
{
    /// <summary>
    /// Recreates the official `ItemIcon` composition (quality frame, icon, count and
    /// optional bottom name) from the pinned sprite payload. Slots that the original
    /// prefab leaves for `IItemIconManager` are filled with this composition.
    /// </summary>
    public sealed partial class CommerceScreenController
    {
        internal GameObject CreateItemIcon(Transform parent, int itemId, long count, bool showName, float size = 96f)
        {
            var root = new GameObject("ItemIcon_" + itemId, typeof(RectTransform));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;

            CommerceItem item = runtime.Catalog.Data.items.FirstOrDefault(value => value.id == itemId);
            string quality = QualitySprite(item == null ? 1 : item.quality);
            Sprite qualitySprite = runtime.FindSprite(quality) ?? runtime.FindSprite("ImgQuality_Blue");
            AddImage(root.transform, "Quality", qualitySprite, new Vector2(size, size), false);
            AddImage(root.transform, "Icon", runtime.FindItemSprite(itemId), new Vector2(size * 0.86f, size * 0.86f), true);

            Transform countRoot = AddImage(root.transform, "CountBg", runtime.FindSprite("ImgItemIcon_NumBg"),
                new Vector2(size * 0.62f, size * 0.3f), false).transform;
            RectTransform countRect = countRoot.GetComponent<RectTransform>();
            countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-2f, 2f);
            AddCountText(countRoot, count, size * 0.2f);

            if (showName)
            {
                var nameObject = new GameObject("BottomName", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform nameRect = nameObject.GetComponent<RectTransform>();
                nameRect.SetParent(root.transform, false);
                nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0f);
                nameRect.pivot = new Vector2(0.5f, 1f);
                nameRect.anchoredPosition = new Vector2(0f, 2f);
                nameRect.sizeDelta = new Vector2(size + 32f, size * 0.28f);
                TextMeshProUGUI text = nameObject.GetComponent<TextMeshProUGUI>();
                text.font = runtime.ChineseTmpFont;
                text.fontSize = size * 0.18f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.94f, 0.94f, 0.94f, 1f);
                text.raycastTarget = false;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.text = runtime.Catalog.ItemName(itemId);
            }
            return root;
        }

        private void AddCountText(Transform parent, long count, float fontSize)
        {
            var go = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2, 0);
            rect.offsetMax = new Vector2(-2, 0);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = runtime.ChineseTmpFont;
            text.fontSize = Mathf.Max(12f, fontSize);
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = FormatCount(count);
        }

        internal void AddRewardIcons(Transform parent, CommerceRewardMap rewards)
        {
            if (parent == null) return;
            ClearChildren(parent);
            foreach (KeyValuePair<int, long> reward in rewards.Values)
                CreateItemIcon(parent, reward.Key, reward.Value, false);
            OfficialCommercePrefabRenderer.RebuildLayout(parent.gameObject);
        }

        internal void AddRewardIcons(Transform parent, IReadOnlyDictionary<int, long> rewards)
        {
            AddRewardIcons(parent, new CommerceRewardMap(rewards));
        }

        internal string FormatCount(long count)
        {
            if (count >= 100000000L) return (count / 100000000d).ToString("0.#", CultureInfo.InvariantCulture) + "亿";
            if (count >= 10000L) return (count / 10000d).ToString("0.#", CultureInfo.InvariantCulture) + "万";
            return count.ToString("N0", CultureInfo.InvariantCulture);
        }

        private string QualitySprite(int quality)
        {
            switch (quality)
            {
                case 3: return "ImgQuality_Blue";
                case 4: return "ImgQuality_Purple";
                case 5: return "ImgQuality_Orange";
                case 6: return "ImgQuality_Red";
                default: return "ImgQuality_White";
            }
        }

        internal Transform AddImage(Transform parent, string name, Sprite sprite, Vector2 size, bool preserveAspect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return go.transform;
        }
    }
}

