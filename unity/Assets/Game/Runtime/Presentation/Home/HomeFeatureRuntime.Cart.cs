using System;
using System.Collections.Generic;
using Zjcs.Offline.Home;
using System.Linq;
using Zjcs.Official.HomeCartWindowsUI;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private const int CartMaxCalcLevelUp = 10;

        internal LanguageGroupConfig NativeLanguageGroupConfig => storage?.NativeLanguageGroupConfig;
        internal ILanguageTypeService NativeLanguageTypeService => storage?.NativeLanguageTypeService;

        internal static bool TryFindLevelTarget(OfficialHomeRules rules, HomeConditionNode node, out int level)
        {
            level = 0;
            if (node == null) return false;
            if (node._type == "LevelCondition" && node.Level > 0)
            {
                level = node.Level;
                return true;
            }
            if ((node._type == "CompositeOrCondition" || node._type == "CompositeAndCondition") && node.Ids != null)
            {
                Dictionary<int, HomeConditionNode> lookup = rules?.Data?.conditions?
                    .Where(row => row != null && row.node != null)
                    .ToDictionary(row => row.classId, row => row.node);
                if (lookup != null)
                {
                    foreach (int id in node.Ids)
                        if (lookup.TryGetValue(id, out HomeConditionNode child) && TryFindLevelTarget(rules, child, out level))
                            return true;
                }
            }
            return false;
        }

        private OfficialHomeCartWindowAssetCatalog cartAssets;

        internal OfficialHomeCartWindowAssetCatalog CartAssets
            => cartAssets ?? (cartAssets = OfficialHomeCartWindowAssetCatalog.LoadDefault());

        private void RegisterCartExtensions()
        {
            OfficialHomeCartWindowAssetCatalog assets = CartAssets;
            OfficialHomeCartWindowAssetCatalog.Skin4266Info info = assets.GetSkin4266Info();
            if (info == null || info.skinItemId <= 0) return;

            rules.RegisterRuntimeFurnitureSkin(new HomeFurnitureSkinRecord
            {
                skinItemId = info.skinItemId,
                furnitureType = info.furnitureType,
                prefabName = info.prefabName,
                isDefault = false,
                order = info.order
            });

            rules.RegisterRuntimeItem(new HomeItemRecord
            {
                id = info.skinItemId,
                name = assets.Text(info.itemNameKey),
                quality = 4,
                type = 428,
                icon = string.Empty,
                rank = -1,
                overlap = false
            });
        }

        internal IReadOnlyList<HomeCartEquipPreviewItem> CalcCartEquipPreview(int hangUpId)
        {
            HomeHangUpRecord hangUp = rules?.HangUp(hangUpId);
            HomeHangUpRewardRecord reward = hangUp == null ? null : rules.HangUpReward(hangUp.rewardId);
            HomeRewardRuleRecord rule = reward == null ? null : rules.SelectRewardRule(reward.rewardRuleId, context);
            if (rule == null || rule.ids == null || rule.ids.Length == 0 || rule.percent <= 0.0)
                return Array.Empty<HomeCartEquipPreviewItem>();

            int[] weights = rule.weights ?? Array.Empty<int>();
            long totalWeight = 0;
            for (int index = 0; index < rule.ids.Length; index++)
            {
                int weight = index < weights.Length ? Math.Max(0, weights[index]) : 1000;
                totalWeight += weight;
            }
            if (totalWeight <= 0) return Array.Empty<HomeCartEquipPreviewItem>();

            var items = new List<HomeCartEquipPreviewItem>(rule.ids.Length);
            double[] counts = rule.counts ?? Array.Empty<double>();
            for (int index = 0; index < rule.ids.Length; index++)
            {
                int itemId = rule.ids[index];
                int weight = index < weights.Length ? Math.Max(0, weights[index]) : 1000;
                if (itemId <= 0 || weight <= 0) continue;
                double probability = rule.percent * weight / totalWeight;
                int amount = index < counts.Length ? Math.Max(1, (int)Math.Round(counts[index])) : 1;
                items.Add(new HomeCartEquipPreviewItem(itemId, probability, amount));
            }
            return items;
        }
    }
}
