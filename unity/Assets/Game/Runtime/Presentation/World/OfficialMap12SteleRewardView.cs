using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    // UILogicUtils.InitRewardContentByItemDropInfos for the selected fixed-drop
    // closure. ItemComparerBase, AwardUtils and CanGetItemWhenExist retain their
    // original order; the catalog rejects types needing additional filters.
    public static class OfficialMap12SteleRewardView
    {
        public static void Populate(Transform slot, OfficialMap12SteleCatalog catalog, OfficialMap12Stele stele,
            OfficialMap11UiAssets assets, string resourceRoot, Func<string, string> localize,
            Action<int> showItemInfo)
        {
            if (showItemInfo == null) throw new ArgumentNullException(nameof(showItemInfo));
            foreach (var old in slot.GetComponentsInChildren<WorldItemIcon>(false))
            {
                old.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(old.gameObject);
            }
            var rewards = catalog.RewardPreview(stele);
            var settings = catalog.Data.RewardPreviewSettings;
            var items = rewards.Keys.Select(id => catalog.Data.Items.Single(v => v.ClassId == id))
                .OrderByDescending(v => v.ClassId == settings.PlayerExpItemId ? 1 : 0)
                .ThenByDescending(v => v.Quality).ThenBy(v => v.ClassId);
            foreach (var item in items)
            {
                if (settings.IgnoreShowAwardId.Contains(item.ClassId)) continue;
                // All 50 selected original items have CanDropWhenExist=true.
                // That source invariant is checked before this view is used.
                var prefab = Resources.Load<GameObject>(resourceRoot + assets.ItemTemplate(item.ClassId));
                if (prefab == null) throw new InvalidDataException("Original stele item template is missing: " + item.ClassId);
                var icon = UnityEngine.Object.Instantiate(prefab, slot, false).GetComponent<WorldItemIcon>();
                var button = icon.GetComponent<Button>();
                if (button == null) throw new InvalidDataException("Original stele item button is unbound: " + item.ClassId);
                icon.Init(assets.Item(item.ClassId), assets.ItemQuality(item.ClassId),
                    string.Format(CultureInfo.CurrentCulture, localize("ui_ItemIcon_count_key"),
                        WorldBattleReportNumberFormat.Compact(rewards[item.ClassId])));
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => showItemInfo(item.ClassId));
                button.enabled = true;
            }
        }
    }
}
