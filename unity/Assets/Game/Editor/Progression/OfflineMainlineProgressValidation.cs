using System;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
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
                long slotCreatedAt = slots.GetCreatedAtUnixSeconds(slot);
                Require(slotCreatedAt > 0, "Persist the local profile creation time");
                OfflineMainlineProgressSession session = OfflineMainlineProgressSession.Open(slots, slot, catalog);
                Require(session.Save.CreatedAtUnixSeconds == slotCreatedAt,
                    "Copy the profile creation time into its progress save");
                Require(session.InitializeMap(10, new[] { 0, 1, 2, 3, 4 }).IsApplied,
                    "Initialize the five official Map10 regions");

                Require(session.RecordMapBattle(10, 0, 1, "map10-battle-1").IsApplied,
                    "Record the first actual victory");
                Require(session.RecordMapBattle(10, 0, 1, "map10-battle-1").Status == ProgressionResultStatus.Unchanged,
                    "Ignore a replayed battle receipt");
                Require(session.RecordMapBattle(10, 0, 1, "map10-battle-2").IsApplied,
                    "Record the second actual victory");
                Require(session.GetCurrentTask(out OfficialMainlineTaskDefinition task, out bool ready).Status
                    == ProgressionResultStatus.Ready && task.classId == 1000 && ready,
                    "Unlock original mainline task 1000 from two region 0 wins");
                Require(session.ClaimTask(1000).IsApplied && session.InventoryCount(2) == 30,
                    "Claim task 1000 and persist its original 30 item 2 reward");

                Require(session.SetMapRegionProgress(10, 0, 10000).IsApplied,
                    "Record the official completed region 0 progress");
                Require(session.GetCurrentTask(out task, out ready).Status == ProgressionResultStatus.Ready
                    && task.classId == 1005 && ready,
                    "Unlock task 1005 from the saved region progress and task receipt");
                Require(session.ClaimTask(1005).IsApplied && session.InventoryCount(2) == 60,
                    "Claim task 1005 and persist its original reward");

                OfflineMainlineProgressSession restored = OfflineMainlineProgressSession.Open(slots, slot, catalog);
                Require(restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1000)
                    && restored.Save.Tasks.ClaimedMainlineTaskIds.Contains(1005)
                    && restored.InventoryCount(2) == 60,
                    "Restore task receipts and reward inventory from the profile save");
                Require(restored.GetCurrentTask(out task, out ready).Status == ProgressionResultStatus.NotReady
                    && task.classId == 1010 && !ready,
                    "Expose the next task only after its own region progress condition");

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

                Debug.Log("Offline mainline progress validation passed: tasks 1000/1005, battle replay, region progress, rewards and save reload.");
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
