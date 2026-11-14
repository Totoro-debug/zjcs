using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.HomeOutputAddition;

namespace Zjcs.Offline.HomeUI
{
    // The local request boundary. The original reward handlers own numeric rules;
    // the home journal and character receipt own recovery across the two files.
    public sealed class OfflineHomeFastForwardRequests
    {
        readonly OfficialHomeRules rules;
        readonly OfflineHomeIncomeSession income;
        readonly OfflineMainlineProgressSession profile;
        readonly Func<HomeConditionContext> context;
        readonly OfflineHomeOutputAddition additions;
        readonly HashSet<int> autoIncreaseItems;
        readonly Dictionary<int, OutputAdditionPropType> speedAdditionTypes;

        public OfflineHomeFastForwardRequests(OfficialHomeRules rules, OfflineHomeIncomeSession income,
            OfflineMainlineProgressSession profile, Func<HomeConditionContext> context,
            OfflineHomeOutputAddition additions)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.income = income ?? throw new ArgumentNullException(nameof(income));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.additions = additions ?? throw new ArgumentNullException(nameof(additions));
            var data = JObject.Parse(OfficialHomeFastHangUpAssetCatalog.LoadDefault().rules.text);
            if ((string)data["manifest"] != "884_156692")
                throw new InvalidOperationException("Selected acceleration item definitions are unavailable.");
            autoIncreaseItems = new HashSet<int>(((JArray)data["tables"]["item"])
                .Where(item => (item["Effects"] as JArray)?.Any(effect =>
                    (string)effect["_type"] == "ItemEffectAutoIncrease") == true)
                .Select(item => (int)item["ClassId"]));
            var output = Resources.Load<TextAsset>("Home/output-addition-rules");
            var outputData = output ? JObject.Parse(output.text) : null;
            if ((string)outputData?["manifest"] != "884_156692")
                throw new InvalidOperationException("Selected item output additions are unavailable.");
            speedAdditionTypes = outputData["settings"]["ItemIncreaseSpeedAddPercentTypeDict"]
                .ToObject<Dictionary<int, OutputAdditionPropType>>();
        }

        public OfflineHomeFastForwardOperation PrepareFree(int fastId, string receipt)
        {
            var fast = rules.FastHangUp(fastId);
            if (fast == null || !income.CanFreeFastForward(fastId)) return null;
            return Prepare(fast, fast.freeHours, true, Array.Empty<HomeCostEntry>(), receipt);
        }

        public OfflineHomeFastForwardOperation PrepareItems(int fastId,
            IReadOnlyDictionary<int, long> counts, string receipt)
        {
            var fast = rules.FastHangUp(fastId);
            if (fast == null || counts == null || counts.Count == 0) return null;
            profile.Reload();
            double hours = 0;
            var costs = new List<HomeCostEntry>();
            foreach (var pair in counts)
            {
                var item = fast.items?.FirstOrDefault(value => value.itemId == pair.Key);
                if (item == null || pair.Value <= 0 || pair.Value > Math.Min(100, fast.maxUseItemCount)
                    || profile.InventoryCount(pair.Key) < pair.Value) return null;
                hours += item.hours * pair.Value;
                costs.Add(new HomeCostEntry { itemId = pair.Key, count = pair.Value });
            }
            return Prepare(fast, hours, false, costs, receipt);
        }

        OfflineHomeFastForwardOperation Prepare(HomeFastHangUpRecord fast, double hours,
            bool free, IReadOnlyList<HomeCostEntry> costs, string receipt)
        {
            var extras = new List<OfflineHomeFastForwardAutoIncrease>();
            bool heal = false;
            foreach (int id in fast.extraHangUpRewardList ?? Array.Empty<int>())
            {
                var hangUp = rules.HangUp(id)
                    ?? throw new InvalidOperationException("Selected extra hang-up is unavailable: " + id);
                if (hangUp.rewardType == 3) { heal = true; continue; }
                if (hangUp.rewardType != 2)
                    throw new NotSupportedException("Bind the original extra reward handler: " + hangUp.rewardType);
                var reward = rules.HangUpReward(hangUp.rewardId)
                    ?? throw new InvalidOperationException("Selected extra reward is unavailable: " + hangUp.rewardId);
                var row = rules.SelectRewardRule(reward.rewardRuleId, context());
                if (row?.ids == null || row.ids.Length == 0)
                    throw new InvalidOperationException("Selected extra reward item is unavailable.");
                int itemId = row.ids[0]; // Original AutoIncreaseItemHangUpRewardHandler.GetRewardItem.
                if (!autoIncreaseItems.Contains(itemId)) continue;
                speedAdditionTypes.TryGetValue(itemId, out var type);
                extras.Add(new OfflineHomeFastForwardAutoIncrease { HangUpId = id, ItemId = itemId,
                    OutputAdditionPropValue = additions.PropertyValue(type), HasOutputAddition = free });
            }
            return income.PrepareFastForwardOperation(fast.fastHangUpId,
                fast.hangUpIdList ?? Array.Empty<int>(), hours, free, costs, extras, heal, receipt,
                fast.bindFurniture == 0);
        }

        public ProgressionResult CommitPendingProfile(DateTime now)
        {
            var operation = income.PendingFastForwardOperation;
            if (operation == null) return null;
            profile.Reload();
            long unix = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds();
            bool Applied(string receipt) => profile.Save.Player.AppliedInventoryEventIds.Contains(receipt);
            // An uncharged request can be abandoned if its items are no longer
            // available. A durable character receipt always takes precedence.
            if (!Applied(operation.ProfileReceipt) && operation.Costs.Any(cost =>
                profile.InventoryCount(cost.itemId) < cost.count))
            {
                income.CancelUnpaidFastForwardOperation(operation.Receipt, Applied);
                return null;
            }
            var result = profile.ApplyInventoryTransaction(operation.ProfileReceipt, save => {
                foreach (var cost in operation.Costs)
                    if (!OfflineInventoryOperations.Consume(save.Player, cost.itemId, cost.count, unix).IsApplied)
                        throw new InvalidOperationException("Acceleration items could not be consumed.");
                foreach (var extra in operation.AutoIncrease)
                    OfflineItemAutoIncrease.SettleFastForward(save.Player.Inventory.FirstOrDefault(
                        value => value.ItemId == extra.ItemId), operation.Hours,
                        extra.HasOutputAddition, extra.OutputAdditionPropValue, now.ToUniversalTime());
                if (operation.HealToFull)
                {
                    // TreatmentHangUpRewardHandler -> Common.Player.HpPercent setter.
                    save.Player.Health.HpUpdateTimeTicks = now.ToUniversalTime().Ticks;
                    save.Player.Health.HpPercentVal = 1f;
                }
            });
            return result;
        }

        public OfflineHomeFastForwardResult CompletePending(DateTime now)
        {
            var operation = income.PendingFastForwardOperation;
            if (operation == null) return null;
            var result = CommitPendingProfile(now);
            if (result == null || !result.IsApplied && result.Status != ProgressionResultStatus.Unchanged) return null;
            return income.CompleteFastForwardOperation(operation.Receipt,
                receipt => profile.Save.Player.AppliedInventoryEventIds.Contains(receipt));
        }
    }
}
