using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineWorldExplorationService
    {
        // The profile transaction owns drops, RNG and both victory counters.
        // A retry uses the same receipts and cannot reroll or duplicate awards.
        public ProgressionResult SettleMainlineMonsterVictory(OfficialMainlineMonsterRewardCatalog catalog,
            int stageId, IEnumerable<int> monsterIds, int rewardLevel, int rewardSubRank,
            OfflineExploreContext context, string receipt, string battleReceipt, string winReceipt,
            long now, out IReadOnlyDictionary<int, long> awards)
        {
            if (catalog == null || monsterIds == null || context == null) throw new ArgumentNullException();
            var output = new Dictionary<int, long>();
            awards = output;
            if (stageId <= 0 || context.MapClassId <= 0 || context.Region < 0 || now <= 0
                || string.IsNullOrWhiteSpace(receipt) || string.IsNullOrWhiteSpace(battleReceipt)
                || string.IsNullOrWhiteSpace(winReceipt))
                return Failure("Invalid mainline monster victory transaction.", ProgressionResultStatus.InvalidState);
            int[] monsters = monsterIds.ToArray();
            var rewardContext = new OfflineExploreContext
            {
                MapClassId = context.MapClassId, MapGroupId = context.MapGroupId,
                Area = context.Area, Region = context.Region, X = context.X, Y = context.Y,
                Level = rewardLevel, SubRank = rewardSubRank, RegionLevel = context.RegionLevel,
                RegionSubRank = context.RegionSubRank, RegionCode = context.RegionCode,
                DropItems = context.DropItems, MapCanExplore = context.MapCanExplore,
                RegionForbidsExplore = context.RegionForbidsExplore, IsAlive = context.IsAlive,
                IsActivityOpen = context.IsActivityOpen, HasOrUsedItem = context.HasOrUsedItem
            };
            session.Reload();
            ProgressionResult result = session.ApplyInventoryTransaction(receipt, save =>
            {
                if (save.World.AppliedEventIds.Contains(battleReceipt) || save.World.AppliedEventIds.Contains(winReceipt))
                    throw new InvalidOperationException("Victory receipt exists without its monster reward transaction.");
                bool Met(OfficialExploreCondition condition, int depth)
                {
                    if (condition == null) return true;
                    if (depth > 20) throw new InvalidDataException("Monster reward condition recursion exceeded its limit.");
                    if (condition._type == "CompositeAndCondition" || condition._type == "CompositeOrCondition")
                    {
                        var nested = (condition.Ids ?? Array.Empty<int>()).Select(id =>
                            catalog.Data.conditions.Single(row => row.ClassId == id).Condition);
                        return condition._type == "CompositeAndCondition"
                            ? nested.All(value => Met(value, depth + 1)) : nested.Any(value => Met(value, depth + 1));
                    }
                    return IsMet(condition, rewardContext, save);
                }
                long state = save.Player.InventoryRandomState;
                var rolled = catalog.Roll(stageId, monsters, rewardLevel, rewardSubRank,
                    condition => Met(condition, 0), () => RewardRandom(ref state));
                save.Player.InventoryRandomState = state;
                foreach (var drop in rolled)
                {
                    var package = catalog.Data.randomRewards.SingleOrDefault(v => v.Id == drop.Key);
                    if (package != null && package.IsCanAutoExpend)
                    {
                        foreach (var item in ResolvePackage(drop.Key, checked((int)drop.Value), rewardContext, save, package))
                            GrantExplore(save.Player, item.ItemId, item.Count, now, output);
                    }
                    else GrantExplore(save.Player, drop.Key, drop.Value, now, output);
                }
                var battle = save.World.BattleCounts.SingleOrDefault(v => v.MapClassId == context.MapClassId
                    && v.Region == context.Region && v.Result == 0);
                if (battle == null)
                {
                    battle = new OfflineMapBattleCount { MapClassId = context.MapClassId, Region = context.Region, Result = 0 };
                    save.World.BattleCounts.Add(battle);
                }
                battle.Count = checked(battle.Count + 1);
                var win = save.World.StageWinCounts.SingleOrDefault(v => v.StageId == stageId);
                if (win == null)
                {
                    win = new OfflineStageWinCount { StageId = stageId };
                    save.World.StageWinCounts.Add(win);
                }
                win.Count = checked(win.Count + 1);
                save.World.AppliedEventIds.Add(battleReceipt);
                save.World.AppliedEventIds.Add(winReceipt);
            });
            if (!result.IsApplied) output.Clear();
            return result;
        }
    }
}
