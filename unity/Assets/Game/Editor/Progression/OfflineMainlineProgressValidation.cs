using System;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Progression
{
    public static class OfflineMainlineProgressValidation
    {
        [MenuItem("Zjcs/Validate/Offline Mainline Progress")]
        public static void Run()
        {
            OfficialMainlineTaskCatalog catalog = OfficialMainlineTaskCatalog.LoadDefault();
            string root = Path.Combine(Path.GetTempPath(), "zjcs-mainline-validation-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                string fantasyTowerSavePath = Path.Combine(slots.GetSlotDirectory(slot), "fantasy-tower-save.json");
                Func<int, int> readDemonBattleLayer = entryIndex =>
                    OfflineFantasyTowerSession.ReadClearedLayer(fantasyTowerSavePath, entryIndex);
                long slotCreatedAt = slots.GetCreatedAtUnixSeconds(slot);
                Require(slotCreatedAt > 0, "Persist the local profile creation time");
                OfflineMainlineProgressSession session = OfflineMainlineProgressSession.Open(
                    slots, slot, catalog, readDemonBattleLayer);
                Require(session.Save.CreatedAtUnixSeconds == slotCreatedAt,
                    "Copy the profile creation time into its progress save");
                Require(session.InitializeMap(10, new[] { 0, 1, 2, 3, 4 }).IsApplied,
                    "Initialize the five official Map10 regions");
                Require(OfficialMainlineMapRuntime.ResolveHomeReturnMap(session.Save) == 10,
                    "Keep Map11 closed until Map10 task 1040 is claimed");
                session.Save.Tasks.ClaimedMainlineTaskIds.Add(1040);
                Require(OfficialMainlineMapRuntime.ResolveHomeReturnMap(session.Save) == 11,
                    "Open Map11 after Map10 task 1040 is claimed");
                session.Save.Tasks.ClaimedMainlineTaskIds.Remove(1040);

                Require(session.RecordMapBattle(10, 0, 1, "map10-battle-1").IsApplied,
                    "Record the first actual victory");
                Require(session.RecordMapBattle(10, 0, 1, "map10-battle-1").Status == ProgressionResultStatus.Unchanged,
                    "Ignore a replayed battle receipt");
                Require(session.RecordMapBattle(10, 0, 1, "map10-battle-2").IsApplied,
                    "Record the second actual victory");
                ProgressionResult firstTaskResult = session.GetCurrentTask(
                    out OfficialMainlineTaskDefinition task, out bool ready);
                Require(firstTaskResult.Status == ProgressionResultStatus.Ready
                    && task.classId == 1000 && ready,
                    "Unlock original mainline task 1000 from two region 0 wins (status="
                    + firstTaskResult.Status + ", task=" + (task == null ? 0 : task.classId)
                    + ", candidate=" + firstTaskResult.TaskClassId + ", ready=" + ready
                    + ", unsupported=" + firstTaskResult.UnsupportedCondition + ")");
                Require(session.ClaimTask(1000).IsApplied && session.InventoryCount(2) == 30,
                    "Claim task 1000 and persist its original 30 item 2 reward");

                Require(session.SetMapRegionProgress(10, 0, 10000).IsApplied,
                    "Record the official completed region 0 progress");
                Require(session.GetCurrentTask(out task, out ready).Status == ProgressionResultStatus.Ready
                    && task.classId == 1005 && ready,
                    "Unlock task 1005 from the saved region progress and task receipt");
                Require(session.ClaimTask(1005).IsApplied && session.InventoryCount(2) == 60,
                    "Claim task 1005 and persist its original reward");

                OfflineMainlineProgressSession restored = OfflineMainlineProgressSession.Open(
                    slots, slot, catalog, readDemonBattleLayer);
                Require(restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1000)
                    && restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1005)
                    && restored.InventoryCount(2) == 60,
                    "Restore task receipts and reward inventory from the profile save");
                Require(restored.GetCurrentTask(out task, out ready).Status == ProgressionResultStatus.NotReady
                    && task.classId == 1010 && !ready,
                    "Expose the next task only after its own region progress condition");

                Require(catalog.OrderedTasks.Count(value => value.classId >= 1045
                    && value.classId <= 1160 && value.classId % 5 == 0) == 24,
                    "Load all 24 fixed-version Map11 mainline tasks");
                Require(catalog.TryGetTask(1070, out OfficialMainlineTaskDefinition demonTask),
                    "Load Map11 demon battlefield task 1070");
                restored.Save.Tasks.ClaimedMainlineTaskIds.Add(1065);
                OfflineFantasyTowerSession tower = OfflineFantasyTowerSession.Open(slots, slot);
                Require(tower.SetClearedLayer(1, 1) == FantasyTowerResult.Applied,
                    "Persist the first cleared demon battlefield layer");
                ProgressionResult towerProgress = restored.GetTaskProgress(demonTask.finishCondition,
                    out long towerCurrent, out long towerTarget);
                Require(towerProgress.Status == ProgressionResultStatus.Ready
                    && towerCurrent == 2 && towerTarget == 3
                    && restored.ClaimTask(1070).Status == ProgressionResultStatus.NotReady,
                    "Match official layer-plus-one display and reject a premature task claim");
                FantasyTowerResult secondLayer = tower.SetClearedLayer(1, 2);
                ProgressionResult demonTaskClaim = restored.ClaimTask(1070);
                Require(secondLayer == FantasyTowerResult.Applied && demonTaskClaim.IsApplied
                    && restored.InventoryCount(2) == 90 && restored.Save.Player.Experience == 50,
                    "Claim task 1070 after clearing its configured layer and grant fixed rewards (layer="
                    + secondLayer + ", claim=" + demonTaskClaim.Status + ", unsupported="
                    + demonTaskClaim.UnsupportedCondition + ", message=" + demonTaskClaim.Message
                    + ", cleared=" + readDemonBattleLayer(1) + ", item2=" + restored.InventoryCount(2)
                    + ", experience=" + restored.Save.Player.Experience + ")");
                OfflineMainlineProgressSession towerRestored = OfflineMainlineProgressSession.Open(
                    slots, slot, catalog, readDemonBattleLayer);
                Require(towerRestored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1070)
                    && towerRestored.InventoryCount(2) == 90 && towerRestored.Save.Player.Experience == 50,
                    "Restore Map11 task claim and rewards from the local save");

                Require(restored.InitializeMap(11, new[] { 0 }).IsApplied,
                    "Initialize a second map for the shared witch activation count");
                Require(restored.RecordWitchActivated(10, 9001, "witch-map10-9001").IsApplied
                    && restored.ActiveWitchCount == 1,
                    "Count the first activated witch from Map10");
                Require(restored.RecordWitchActivated(11, 11001, "witch-map11-11001").IsApplied
                    && restored.ActiveWitchCount == 2,
                    "Accumulate activated witches across maps");
                Require(restored.RecordWitchActivated(11, 11001, "witch-map11-11001-replay").Status
                    == ProgressionResultStatus.Unchanged && restored.ActiveWitchCount == 2,
                    "Do not count a previously activated witch twice");
                OfflineMainlineProgressSession witchRestored = OfflineMainlineProgressSession.Open(
                    slots, slot, catalog, readDemonBattleLayer);
                Require(witchRestored.ActiveWitchCount == 2,
                    "Restore the global activated witch count from the profile save");

                Require(witchRestored.ApplyStatueVisit("20261025", "witch", 11, 2801, "witch-daily-1",
                    -1f, new[] { 28500 }, new[] { new OfflineItemStack { ItemId = 110, Count = 2 } }, 100).IsApplied,
                    "Commit a witch visit, item reward and next-battle effects atomically");
                Require(witchRestored.ApplyStatueVisit("20261025", "hero", 10, 9002, "hero-daily-1",
                    0f, new[] { 28501 }, Array.Empty<OfflineItemStack>(), 100).Status == ProgressionResultStatus.NotReady,
                    "A witch visit consumes the shared daily hero and witch quota");
                var statueRestored = OfflineMainlineProgressSession.Open(slots, slot, catalog, readDemonBattleLayer);
                Require(statueRestored.HasVisitedStatueOn("20261025")
                    && statueRestored.Save.Player.PendingStatueBattleEffects.Count == 1
                    && statueRestored.Save.Player.PendingStatueBattleEffects[0].HpPercentChange == -1f
                    && statueRestored.Save.Player.PendingStatueBattleEffects[0].StatusEffectClassIds.SequenceEqual(new[] { 28500 })
                    && statueRestored.InventoryCount(110) == 2,
                    "Quit and reload retain the daily quota, item reward and unapplied battle effects");
                Require(statueRestored.ApplyStatueVisit("20261025", "witch", 11, 2801, "witch-daily-1",
                    -1f, new[] { 28500 }, new[] { new OfflineItemStack { ItemId = 110, Count = 2 } }, 100).Status
                    == ProgressionResultStatus.Unchanged && statueRestored.InventoryCount(110) == 2,
                    "Replayed statue receipts cannot grant rewards twice");
                Require(statueRestored.ClearPendingStatueBattleEffects("battle-end-1").IsApplied
                    && statueRestored.HasVisitedStatueOn("20261025")
                    && statueRestored.Save.Player.PendingStatueBattleEffects.Count == 0,
                    "Battle end clears effects while keeping the shared daily quota");
                Require(statueRestored.ApplyStatueVisit("20261026", "hero", 10, 9002, "hero-daily-2",
                    0f, new[] { 28501 }, Array.Empty<OfflineItemStack>(), 100).IsApplied,
                    "A new day permits another shared statue visit");

                Require(restored.ApplyInventoryTransaction("inventory-units", save =>
                {
                    Require(OfflineInventoryOperations.Grant(save.Player, 140, 1, 100, 200, true).IsApplied,
                        "Track the first timed item unit");
                    Require(OfflineInventoryOperations.Grant(save.Player, 140, 1, 150, 300, true).IsApplied,
                        "Track the second timed item unit");
                    save.Player.InventoryRandomState = 42;
                }).IsApplied, "Commit item units and random state together");
                Require(restored.ApplyInventoryTransaction("inventory-units", save =>
                    save.Player.InventoryRandomState = 99).Status == ProgressionResultStatus.Unchanged
                    && restored.Save.Player.InventoryRandomState == 42,
                    "Ignore a replayed inventory transaction without advancing random state");
                Require(restored.ApplyInventoryTransaction("inventory-boundary", save =>
                    Require(OfflineInventoryOperations.ExpiredCount(save.Player.Inventory
                        .Single(value => value.ItemId == 140), 200) == 0,
                        "Keep a timed item valid at its exact expiry timestamp")).IsApplied,
                    "Use the original strict-greater-than expiry boundary");
                Require(restored.ApplyInventoryTransaction("inventory-expire", save =>
                    Require(OfflineInventoryOperations.RemoveExpired(save.Player, 140, 201) == 1,
                        "Remove only the earliest expired unit")).IsApplied
                    && restored.InventoryCount(140) == 1,
                    "Persist timed unit expiry without removing later units");
                ProgressionResult rollback = restored.ApplyInventoryTransaction("inventory-rollback", save =>
                {
                    save.Player.InventoryRandomState = 100;
                    throw new InvalidOperationException("cancel");
                });
                Require(rollback.Status == ProgressionResultStatus.InvalidState
                    && restored.Save.Player.InventoryRandomState == 42,
                    "Roll back cancelled item and random mutations");

                Require(restored.ApplyInventoryTransaction("hand-in-seed", save =>
                    Require(OfflineInventoryOperations.Grant(save.Player, 2208, 1, 100).IsApplied,
                        "Seed the configured official hand-in item")).IsApplied,
                    "Persist hand-in inventory before submission");
                var handInItems = new System.Collections.Generic.Dictionary<int, long> { [2208] = 1 };
                Require(restored.CanHandInItem(4, 1)
                    && restored.SubmitHandInItem(4, 1, handInItems, 100, "map14:hand-in:4:1").IsApplied
                    && restored.InventoryCount(2208) == 0 && restored.HandInItemCount(4) == 1,
                    "Consume the configured item and advance its submission count atomically");
                Require(restored.SubmitHandInItem(4, 1, handInItems, 100, "map14:hand-in:4:1").Status
                    == ProgressionResultStatus.Unchanged,
                    "Ignore a replayed hand-in receipt after reaching its maximum");
                Require(restored.SubmitHandInItem(4, 1, handInItems, 100, "map14:hand-in:4:2").Status
                    == ProgressionResultStatus.InvalidState,
                    "Reject submissions beyond the configured maximum");
                OfflineMainlineProgressSession handInRestored = OfflineMainlineProgressSession.Open(
                    slots, slot, catalog, readDemonBattleLayer);
                Require(handInRestored.HandInItemCount(4) == 1 && !handInRestored.CanHandInItem(4, 1)
                    && handInRestored.InventoryCount(2208) == 0,
                    "Restore hand-in count and consumed inventory from the profile save");

                Debug.Log("Offline mainline progress validation passed: tasks, rewards, statue transactions, hand-in transactions and save reload.");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Require(bool condition, string step)
        {
            if (!condition) throw new InvalidOperationException("Offline mainline validation failed: " + step);
        }
    }
}
