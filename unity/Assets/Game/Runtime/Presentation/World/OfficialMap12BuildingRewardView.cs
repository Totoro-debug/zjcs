using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    // UILogicUtils.InitRewardContentByItemDropInfos for the two fixed, available
    // stage-102 entries, with the original ItemIcon template and comparer order.
    public static class OfficialMap12BuildingRewardView
    {
        public static void Populate(Transform slot, OfficialMap12BuildingData data, int level, int subRank,
            OfficialMap11UiAssets assets, string resourceRoot, Func<string, string> localize)
        {
            foreach (var icon in slot.GetComponentsInChildren<WorldItemIcon>(false)) UnityEngine.Object.Destroy(icon.gameObject);
            var rewards = data.MountLiftRewardPreview(level, subRank);
            foreach (var item in data.RewardItems.OrderByDescending(v => v.ItemType == 1 ? 1 : 0)
                .ThenByDescending(v => v.Quality).ThenBy(v => v.ClassId))
            {
                var icon = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(resourceRoot + assets.ItemTemplate(item.ClassId)), slot, false)
                    .GetComponent<WorldItemIcon>();
                icon.Init(assets.Item(item.ClassId), assets.ItemQuality(item.ClassId), string.Format(CultureInfo.CurrentCulture,
                    localize("ui_ItemIcon_count_key"), OfficialPropFormat.Compact(rewards[item.ClassId])));
            }
        }
    }
}
