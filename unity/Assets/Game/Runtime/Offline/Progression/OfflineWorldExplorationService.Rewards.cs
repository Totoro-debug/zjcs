using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineWorldExplorationService
    {
        public bool HasGroupRewardCandidates(OfficialExploreEntityGroup group, OfflineExploreContext context, OfflineMainlineSave save)
        {
            foreach (var raw in Catalog.ExtraData(group.Id).Select(Object))
                if (Text(raw, "_type") == "MapEntityGroupInfoExtraAwardItemData" && Convert.ToBoolean(raw["IsExpendRandomItem"]))
                    if (!Ints(raw, "AwardItemIds").Any(id => PackageItems(id, context, save).Count > 0)) return false;
            return true;
        }
        public IReadOnlyList<OfflineItemStack> ResolvePackage(int item, int count, OfflineExploreContext context, OfflineMainlineSave save)
        {
            var package = Data.randomRewards.Single(v => v.Id == item);
            var candidates = PackageItems(item, context, save);
            if (candidates.Count == 0) throw new InvalidOperationException("Native exploration package has no eligible region items.");
            var result = new Dictionary<int, long>();
            for (int index = 0; index < count; index++)
            {
                int id = candidates.Keys.ElementAt(Choose(candidates.Values.ToArray(), ref save.Player.InventoryRandomState));
                long amount = package.MaxCount > 1 ? package.MinCount + (long)(RewardRandom(ref save.Player.InventoryRandomState)
                    * (package.MaxCount - package.MinCount + 1)) : 1;
                result[id] = checked(result.GetValueOrDefault(id) + amount);
            }
            return result.Select(v => new OfflineItemStack { ItemId = v.Key, Count = v.Value }).ToArray();
        }
        private Dictionary<int, int> PackageItems(int item, OfflineExploreContext context, OfflineMainlineSave save)
        {
            var package = Data.randomRewards.Single(v => v.Id == item);
            var result = new Dictionary<int, int>();
            if (package.RandomItemRangeSelector == null)
            {
                if (package.IdList == null || package.IdList.Length == 0)
                    throw new InvalidDataException("Unconverted native item-type package selector.");
                for (int index = 0; index < package.IdList.Length; index++)
                    result.Add(package.IdList[index], package.WeightOfIdList == null || package.WeightOfIdList.Length == 0 ? 100 : package.WeightOfIdList[index]);
                return result;
            }
            var selector = package.RandomItemRangeSelector;
            if (selector._type != "RandomItemRangeTreasureGachaSelector" || selector.IsPiece)
                throw new InvalidDataException("Unconverted native exploration random-item selector.");
            void Add(OfficialExploreTreasureGacha gacha, bool check)
            {
                if (check && selector.IsNeedMet && !IsMet(gacha.Condition, context, save)) return;
                foreach (int id in gacha.ChestGachaItems)
                {
                    var info = Data.items.SingleOrDefault(v => v.ClassId == id);
                    if (info != null && info.Quality == selector.Quality && (selector.ElementTypes == null
                        || selector.ElementTypes.Length == 0 || selector.ElementTypes.Contains(info.ElementType))) result.TryAdd(id, 100);
                }
            }
            foreach (var gacha in Data.treasureGachas.Where(v => selector.MapClassId == -1 || v.MapClassId == selector.MapClassId)) Add(gacha, true);
            if (result.Count == 0)
                foreach (int id in selector.DefaultGachaIds ?? Array.Empty<int>()) Add(Data.treasureGachas.Single(v => v.ChestGachaId == id), false);
            // Confirmed offline server substitution: intersect the native selector
            // with the current region's original DropItems, preserving weights.
            foreach (int id in result.Keys.Where(v => context.DropItems == null || !context.DropItems.Contains(v)).ToArray()) result.Remove(id);
            return result;
        }
        public ProgressionResult CollectEntity(int serial, OfflineExploreContext context, long now,
            out IReadOnlyDictionary<int, long> awards)
        {
            session.Reload();
            var output = new Dictionary<int, long>(); awards = output;
            var entity = session.Save.World.Exploration.Entities.SingleOrDefault(v => v.Serial == serial);
            if (entity == null || entity.Completed || !entity.Active(now) || entity.MapClassId != context.MapClassId)
                return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            var components = Catalog.Components(entity.EntityClassId);
            var stage = Object(components["WorldStageComponent"]);
            var states = Values(stage, "StateInfos");
            var sub = Object(Values(Object(states[entity.State]), "SubStateList").Single());
            var results = Values(sub, "Results").Select(Object).ToArray();
            if (Int(Object(sub["Behaviour"]), "CompType") != 2 || Values(sub, "Conditions").Count != 0
                || results.Any(v => Int(v, "CompType") != 200 && Int(v, "CompType") != 232 && Int(v, "CompType") != 268))
                return Failure("Native exploration entity requires its stage interaction handler.", ProgressionResultStatus.Unsupported);
            Action<OfflineMainlineSave> mutation = save =>
            {
                var next = save.World.Exploration.Entities.Single(v => v.Serial == serial);
                int oldLevel = context.Level, oldRank = context.SubRank;
                try
                {
                    context.Level = next.Level; context.SubRank = next.SubRank;
                    foreach (var result in results)
                    {
                        int type = Int(result, "CompType"), index = Int(result, "CompIndex");
                        if (type == 232)
                        {
                            foreach (var item in next.Extra.Where(v => v.ItemId > 0)) GrantExplore(save.Player, item.ItemId, item.Count, now, output);
                            continue;
                        }
                        var component = Object(components[type == 268 ? "StageResultCooperateChestComponent" : "StageResultAwardComponent"]);
                        var info = Object(Values(component, type == 268 ? "AwardRuleIds" : "AwardInfos")[index]);
                        foreach (int rule in Ints(info, "RuleIds")) GrantRule(save, rule, context, now, output);
                    }
                    next.State = Catalog.Parameters(next.EntityClassId).CompleteState;
                    next.Completed = true;
                }
                finally { context.Level = oldLevel; context.SubRank = oldRank; }
            };
            string receipt = "world-explore-collect:" + serial;
            var transaction = Catalog.Parameters(entity.EntityClassId).WorldStageType == 521
                ? session.ApplyInventoryTransaction(receipt, mutation, entity.EntityClassId, 1)
                : session.ApplyInventoryTransaction(receipt, mutation);
            if (!transaction.IsApplied) output.Clear();
            return transaction;
        }
        private void GrantRule(OfflineMainlineSave save, int id, OfflineExploreContext context, long now, Dictionary<int, long> output,
            IReadOnlyList<OfficialExploreRewardRule> rules = null)
        {
            var rule = (rules ?? Data.rewardRules).Where(v => v.Id == id).OrderBy(v => v.Order == -1 ? int.MaxValue : v.Order)
                .FirstOrDefault(v => IsMet(v.Condition, context, save));
            // Native RewardRuleInfoParser.Get returns null for empty references.
            // The seven pinned empty references have explicit user approval.
            if (rule == null || rule.Percent <= 0) return;
            int index = Choose(rule.WeightList, ref save.Player.InventoryRandomState);
            float amount = rule.CountList[index] * rule.Percent;
            float Scale(int[] ids, int level) => ids == null || ids.Length <= index || ids[index] == 0 ? 1
                : Data.rewardLevelNumbers.Single(v => v.Id == ids[index]).Nums[Math.Max(0, Math.Min(level,
                    Data.rewardLevelNumbers.Single(v => v.Id == ids[index]).Nums.Length - 1))];
            amount *= Scale(rule.LevelCountId, context.Level); amount *= Scale(rule.SubRankCountId, context.SubRank);
            long count = checked((long)Math.Floor(amount));
            if (amount - count > .00001 && RewardRandom(ref save.Player.InventoryRandomState) < amount - count) count++;
            if (count <= 0) return;
            int item = rule.IdList[index];
            if (Data.randomRewards.Any(v => v.Id == item && v.IsCanAutoExpend))
                foreach (var expanded in ResolvePackage(item, checked((int)count), context, save)) GrantExplore(save.Player, expanded.ItemId, expanded.Count, now, output);
            else GrantExplore(save.Player, item, count, now, output);
        }
        private static void GrantExplore(OfflinePlayerProgress player, int item, long count, long now, Dictionary<int, long> output)
        {
            if (!OfflineInventoryOperations.Grant(player, item, count, now).IsApplied) throw new InvalidOperationException("Native explore reward could not be saved.");
            output[item] = checked(output.GetValueOrDefault(item) + count);
        }
        private static double RewardRandom(ref long state)
        {
            if (state == 0) state = 88172645463325252L;
            ulong value = unchecked((ulong)state);
            value ^= value << 13; value ^= value >> 7; value ^= value << 17; value &= long.MaxValue;
            if (value == 0) value = 88172645463325252UL;
            state = (long)value;
            return (value >> 10) * (1.0 / 9007199254740992.0);
        }
    }
}
