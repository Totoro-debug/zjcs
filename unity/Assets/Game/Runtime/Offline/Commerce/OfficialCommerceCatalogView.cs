using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Commerce
{
    [Serializable] public sealed class OfficialCommerceCatalogData
    {
        public string manifest;
        public CommerceSourceHashes sourceHashes;
        public CommerceSettings settings;
        public CommerceItem[] items;
        public CommerceUiText[] texts;
        public CommercePrefab[] prefabs;
        public CommerceMoneyCost[] moneyCosts;
        public CommerceDirectPay[] directPay;
        public CommercePeriodicCard[] periodicCards;
        public CommerceDailyGroup[] dailyGroups;
        public CommerceGift[] dailyGifts;
        public CommerceWeeklyGroup[] weeklyGroups;
        public CommerceGift[] weeklyGifts;
        public CommerceVoucher[] vouchers;
        public CommerceFirstAward[] firstAwards;
        public CommerceAccumulatedAward[] accumulatedAwards;
        public CommerceQualityFrame[] accumulatedQualityFrames;
        public string accumulatedLockedSprite;
        public CommerceRule[] rules;
        public CommerceAudio[] audio;
    }

    [Serializable] public sealed class CommerceRule
    {
        public string key, title;
        public string[] rows;
    }

    [Serializable] public sealed class CommerceSourceHashes
    {
        public string rulesJson, commerceUiJson, officialUiJson, settingsJson, itemIconsJson;
        public string accumulatedDescriptionsRawfile, accumulatedQualityBundle, itemQualityBundle;
    }
    [Serializable] public sealed class CommerceSettings
    {
        public int voucherItemId, flareStarItemId, diamondItemId, exchangeMin, exchangeMax, exchangeScale;
        public int firstPurchaseMoneyCostId, periodicCardRemindRemainDay;
        public CommerceReward[] dailyFreeRewards;
    }
    [Serializable] public sealed class CommerceItem { public int id; public int quality; public string name, icon; }
    [Serializable] public sealed class CommerceAudio { public string eventName, file, sha256; }
    [Serializable] public sealed class CommerceUiText { public string key, value; }
    [Serializable] public sealed class CommercePrefab
    {
        public string key;
        public CommerceUiNode[] nodes;
        [NonSerialized] public CommerceUiNode root;
    }
    [Serializable] public sealed class CommerceUiNode
    {
        public int parentIndex = -1;
        public string name, path, localization_key;
        public bool active;
        public float[] anchor_min, anchor_max, anchored_position, size_delta, pivot, scale;
        public float rotation, alpha = 1f;
        public string[] scripts;
        public CommerceUiNode[] children;
        public bool has_image, has_text, has_scroll, has_layout, has_grid;
        public bool has_size_fitter, has_layout_element, has_aspect_fitter;
        public CommerceUiImage image;
        public CommerceUiTextStyle text;
        public bool button;
        public CommerceUiScroll scroll;
        public bool mask, rectmask2d;
        public bool mask_component_flags, mask_enabled, mask_show_graphic;
        public bool no_drawing_raycast, raycast_target;
        public float[] raycast_padding;
        public CommerceLayoutGroup layout;
        public CommerceGridLayout grid;
        public int[] size_fitter;
        public CommerceLayoutElement layout_element;
        public CommerceAspectRatioFitter aspect_fitter;
        public CommerceUiAnimation[] animations;
    }
    [Serializable] public sealed class CommerceUiAnimation
    {
        public string name, owner;
        public float duration;
        public bool loop;
        public CommerceUiAnimationTrack[] tracks;
        public CommerceUiAnimationEvent[] events;
    }
    [Serializable] public sealed class CommerceUiAnimationTrack
    {
        public string path, property;
        public CommerceUiAnimationKey[] keys;
    }
    [Serializable] public sealed class CommerceUiAnimationKey
    {
        public float time, value, inTangent, outTangent;
    }
    [Serializable] public sealed class CommerceUiAnimationEvent
    {
        public float time;
        public string functionName;
    }
    [Serializable] public sealed class CommerceUiImage
    {
        public CommerceUiSprite sprite;
        public float[] color;
        public int type;
        public bool preserve_aspect;
        public bool graphic_flags, raycast_target, maskable = true;
    }
    [Serializable] public sealed class CommerceUiSprite
    {
        public string file, name;
        public int width, height;
        public float[] border, rect_size, trim_offset, trim_size, pivot;
        public float ppu;
    }
    [Serializable] public sealed class CommerceUiTextStyle
    {
        public string value;
        public float[] color;
        public float[] margin;
        public float[] auto_size;
        public float font_size;
        public int horizontal, vertical, overflow;
        public bool word_wrap;
    }
    [Serializable] public sealed class CommerceUiScroll
    {
        public bool horizontal, vertical, inertia;
        public int movement;
        public float sensitivity, elasticity, deceleration;
    }
    [Serializable] public sealed class CommerceLayoutGroup
    {
        public int axis, alignment;
        public int[] padding;
        public float spacing;
        public bool[] expand, control, child_scale;
        public bool reverse;
    }
    [Serializable] public sealed class CommerceLayoutElement
    {
        public bool ignore;
        public float[] min, preferred, flexible;
    }
    [Serializable] public sealed class CommerceAspectRatioFitter
    {
        public int mode;
        public float ratio;
    }
    [Serializable] public sealed class CommerceGridLayout
    {
        public int[] padding;
        public int alignment, corner, axis, constraint, count;
        public float[] cell, spacing;
    }
    [Serializable] public sealed class CommerceReward { public int itemId; public long count; }
    [Serializable] public sealed class CommerceMoneyCost
    {
        public int id, voucherCost;
        public float currencyCost, startValue;
        public bool isShow;
        public string productId;
    }
    [Serializable] public sealed class CommerceDirectPay
    {
        public int id, moneyCostId, repeatExtraPercent;
        public string icon, titleKey;
        public CommerceReward[] fixedAward, firstExtraAward, repeatExtraAward;
    }
    [Serializable] public sealed class CommercePeriodicCard
    {
        public int id, moneyCostId, durationDays, awardMultiple, periodicDuration, discount;
        public string titleKey;
        public CommerceReward[] fixedAward, periodicAward;
        public CommerceExtraProp[] extraProps;
    }
    [Serializable] public sealed class CommerceExtraProp { public int propId; public string name; public long value; }
    [Serializable] public sealed class CommerceDailyGroup { public int groupId, bundleMoneyCostId, greatValue; }
    [Serializable] public sealed class CommerceWeeklyGroup { public int groupId, seasonId, seasonStartDay; }
    [Serializable] public sealed class CommerceGift
    {
        public int groupId, id, moneyCostId, limit, greatValue;
        public string titleKey;
        public CommerceReward[] rewards;
    }
    [Serializable] public sealed class CommerceVoucher { public int id, moneyCostId; public string icon, titleKey; }
    [Serializable] public sealed class CommerceFirstAward
    {
        public int day;
        public CommerceReward[] rewards;
        public CommerceProfessionReward[] professionItems;
    }
    [Serializable] public sealed class CommerceProfessionReward
    {
        public int itemId;
        public long count;
        public int[] professions;
    }
    [Serializable] public sealed class CommerceAccumulatedAward
    {
        public int level, group;
        public float threshold;
        public bool isBig;
        public string description;
        public string progressActiveFormat, progressFormat, progressLockedFormat;
        public string requireFormat, requireLocked;
        public CommerceReward[] rewards;
        public CommerceReward[] coreRewards, subRewards;
    }
    [Serializable] public sealed class CommerceQualityFrame
    {
        public int quality;
        public string big, small;
    }

    public sealed class OfficialCommerceCatalogView
    {
        public OfficialCommerceCatalogData Data { get; }

        public static OfficialCommerceCatalogView LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>("Commerce/commerce-runtime");
            if (asset == null) throw new InvalidOperationException("Official commerce runtime data is missing.");
            OfficialCommerceCatalogData data = JsonUtility.FromJson<OfficialCommerceCatalogData>(asset.text);
            if (data == null || data.manifest != "854_156383" || data.periodicCards == null)
                throw new FormatException("Official commerce runtime data is invalid.");
            return new OfficialCommerceCatalogView(data);
        }

        public OfficialCommerceCatalogView(OfficialCommerceCatalogData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Rebuilds the nested UI tree from the flat transport representation. Unity's
        /// JsonUtility has a limited nesting depth; the catalog is therefore serialized as
        /// one flat array per prefab, like the Map10 UI payload.
        /// </summary>
        public static CommerceUiNode BuildTree(CommercePrefab prefab)
        {
            if (prefab?.nodes == null || prefab.nodes.Length == 0) return null;
            var children = new List<CommerceUiNode>[prefab.nodes.Length];
            int root = -1;
            for (int i = 0; i < prefab.nodes.Length; i++)
            {
                CommerceUiNode node = prefab.nodes[i];
                if (node == null) continue;
                if (node.parentIndex < 0)
                {
                    if (root >= 0) throw new FormatException("A commerce prefab has multiple roots: " + prefab.key);
                    root = i;
                }
                else
                {
                    if (node.parentIndex >= i) throw new FormatException("A commerce prefab parent index is invalid: " + prefab.key);
                    if (children[node.parentIndex] == null) children[node.parentIndex] = new List<CommerceUiNode>();
                    children[node.parentIndex].Add(node);
                }
            }
            if (root != 0) throw new FormatException("A commerce prefab root index is invalid: " + prefab.key);
            for (int i = 0; i < prefab.nodes.Length; i++)
                if (children[i] != null) prefab.nodes[i].children = children[i].ToArray();
            return prefab.nodes[root];
        }

        public CommerceMoneyCost MoneyCost(int id) => Data.moneyCosts.FirstOrDefault(value => value.id == id);

        public string ItemName(int id)
        {
            CommerceItem item = Data.items.FirstOrDefault(value => value.id == id);
            return item == null ? "道具" + id : item.name;
        }

        public CommercePrefab Prefab(string key) => Data.prefabs.FirstOrDefault(value => value.key == key);

        public string Text(string key)
        {
            CommerceUiText value = Data.texts.FirstOrDefault(row => row.key == key);
            return value == null ? key : value.value;
        }
    }
}
