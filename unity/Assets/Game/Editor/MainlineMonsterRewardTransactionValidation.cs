using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor
{
    public static class MainlineMonsterRewardTransactionValidation
    {
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")
                ?? throw new InvalidOperationException("Validation workspace is required.");
            string root = Path.Combine(workspace, "monster-profile");
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var session = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
            if (!session.InitializeMap(11, new[] { 11 }).IsApplied)
                throw new InvalidOperationException("Map fixture failed.");
            var service = new OfflineWorldExplorationService(session);
            var catalog = OfficialMainlineMonsterRewardCatalog.Load();
            var context = new OfflineExploreContext { MapClassId = 11, MapGroupId = 11, Region = 11, Level = 20, SubRank = 1 };
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long before = session.InventoryCount(42200);
            var result = service.SettleMainlineMonsterVictory(catalog, 1081, new[] { 3401 }, 20, 1,
                context, "monster-test", "battle-test", "win-test", now, out var awards);
            if (!result.IsApplied || awards[42200] != 24 || awards[11] != 3
                || session.InventoryCount(42200) != before + 24 || session.StageWinCount(1081) != 1
                || session.Save.World.BattleCounts.Single(v => v.MapClassId == 11 && v.Region == 11).Count != 1)
                throw new InvalidOperationException("Original stage 1081 victory transaction failed: " + result.Message);
            string snapshot = JsonUtility.ToJson(session.Save);
            result = service.SettleMainlineMonsterVictory(catalog, 1081, new[] { 3401 }, 20, 1,
                context, "monster-test", "battle-test", "win-test", now, out awards);
            if (result.Status != ProgressionResultStatus.Unchanged || awards.Count != 0 || JsonUtility.ToJson(session.Save) != snapshot)
                throw new InvalidOperationException("Victory replay duplicated a reward or changed RNG.");
            result = service.SettleMainlineMonsterVictory(catalog, 1081, new[] { 3401 }, 20, 1,
                context, "conflict-test", "battle-test", "other-win", now, out awards);
            if (result.Status != ProgressionResultStatus.InvalidState || awards.Count != 0 || JsonUtility.ToJson(session.Save) != snapshot)
                throw new InvalidOperationException("Conflicting victory receipts did not roll back.");
            var originalRule = catalog.Data.rewardRules.First(v => v.Id == 14);
            int[] oldWeights = originalRule.WeightList;
            originalRule.WeightList = new[] { -1 };
            result = service.SettleMainlineMonsterVictory(catalog, 1081, new[] { 3401 }, 20, 1,
                context, "bad-rule", "bad-battle", "bad-win", now, out awards);
            originalRule.WeightList = oldWeights;
            if (result.Status != ProgressionResultStatus.InvalidState || awards.Count != 0 || JsonUtility.ToJson(session.Save) != snapshot)
                throw new InvalidOperationException("Invalid reward did not roll back inventory, receipts and RNG.");
            var reloaded = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
            if (JsonUtility.ToJson(reloaded.Save) != snapshot)
                throw new InvalidOperationException("Victory inventory, RNG and receipts did not survive a cold read.");
            // This original stage can choose package 50007, which was absent
            // from the older shared exploration catalog. Force that weighted
            // branch while retaining the original package and quantity data.
            var packageRule = catalog.Data.rewardRules.Single(v => v.Id == 889);
            int[] packageWeights = packageRule.WeightList;
            packageRule.WeightList = new[] { 100, 0, 0, 0, 0 };
            result = service.SettleMainlineMonsterVictory(catalog, 20018, Array.Empty<int>(), 20, 1,
                context, "package-test", "package-battle", "package-win", now, out awards);
            packageRule.WeightList = packageWeights;
            if (!result.IsApplied || awards.ContainsKey(50007) || !awards.TryGetValue(1, out long diamonds)
                || diamonds < 720 || diamonds > 1080 || awards[42200] != 24)
                throw new InvalidOperationException("Original stage reward package was not expanded atomically: " + result.Message);
            Debug.Log("Mainline monster victory transaction passed.");
            ProjectTools.Validate();
        }
    }
}
