using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Progression
{
    public static class OfflineMapStaminaValidation
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "zjcs-map-stamina-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                var session = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                session.Reload();
                var costs = new Dictionary<int, long> { [57] = 5 };
                var service = new OfflineMapStaminaService(session);
                string before = JsonUtility.ToJson(session.Save);
                Require(service.Collect(11, 11, 115, 8011, costs, 20, 1, 0, now, now + 3, out _).Status
                    == ProgressionResultStatus.InvalidState && JsonUtility.ToJson(session.Save) == before,
                    "Insufficient stamina leaves inventory, RNG and refresh state unchanged");
                Require(session.ApplyInventoryTransaction("map-stamina-fixture", save =>
                {
                    if (!OfflineInventoryOperations.Grant(save.Player, 57, 200, now).IsApplied)
                        throw new InvalidOperationException("Stamina fixture failed.");
                }).IsApplied, "Isolated stamina inventory");
                Require(service.Collect(11, 11, 115, 8011, costs, 20, 1, 0, now, now + 3, out var gold).IsApplied
                    && gold.Count == 1 && gold[1] >= 960 && gold[1] <= 1440 && session.InventoryCount(57) == 195,
                    "Gold expands twelve original 80..120 packages and spends five stamina");
                long rng = session.Save.Player.InventoryRandomState;
                Require(service.Collect(11, 11, 115, 8011, costs, 20, 1, 0, now + 1, now + 4, out _).Status
                    == ProgressionResultStatus.NotReady && session.InventoryCount(57) == 195
                    && session.Save.Player.InventoryRandomState == rng, "Repeated click cannot reroll or charge during respawn");
                session = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
                service = new OfflineMapStaminaService(session);
                Require(!service.Available(11, 115, now + 2) && service.Available(11, 115, now + 3),
                    "A cold save reload counts elapsed offline time toward respawn");
                Require(service.Collect(11, 11, 147, 8021, costs, 20, 1, 0, now, now + 3, out var iron).IsApplied
                    && iron[41300] >= 96 && iron[41300] <= 144, "Iron expands the original package range");
                Require(service.Collect(11, 11, 1011, 8026, costs, 20, 1, 0, now, now + 3, out var sand).IsApplied
                    && sand[41400] >= 80 && sand[41400] <= 120, "Time sand expands ten original packages");
                Require(service.StartBattle(11, 135, costs, now).IsApplied, "Monster entry spends stamina once");
                long stamina = session.InventoryCount(57);
                Require(service.StartBattle(11, 135, costs, now).Status == ProgressionResultStatus.Unchanged
                    && session.InventoryCount(57) == stamina, "Resuming an active battle does not charge again");
                Require(service.FinishBattle(11, 11, 135, 5011, false, 20, 2, 5000, now, now + 10, out var lost).IsApplied
                    && lost.Count == 0 && service.Available(11, 135, now), "A defeat grants no reward and permits a new attempt");
                Require(service.StartBattle(11, 135, costs, now).IsApplied && session.InventoryCount(57) == stamina - 5,
                    "The next battle attempt has its own cost receipt");
                Require(service.FinishBattle(11, 11, 135, 5011, true, 20, 2, 5000, now, now + 10, out var won).IsApplied
                    && won[42200] == 360 && session.InventoryCount(42200) == 360,
                    "Victory applies the original 1.6 rank multiplier and confirmed 50 percent entity reward bonus");
                Require(service.FinishBattle(11, 11, 135, 5011, true, 20, 2, 5000, now, now + 10, out _).Status
                    == ProgressionResultStatus.Unchanged && session.InventoryCount(42200) == 360,
                    "Replayed victory cannot duplicate rewards");
                Require(service.StartBattle(11, 135, costs, now + 9).Status == ProgressionResultStatus.NotReady
                    && service.StartBattle(11, 135, costs, now + 10).IsApplied, "Monster restores at its original ten second group interval");
                string json = File.ReadAllText(slots.GetProgressPath(slot));
                var old = JsonUtility.FromJson<OfflineMainlineSave>(json);
                old.World.MapInteractions = null;
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(old));
                Require(OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault())
                    .Save.World.MapInteractions != null, "Older saves normalize the new optional map interaction list");
                Debug.Log("Map stamina validation passed: transactional costs and RNG, package rewards, defeat/retry, resumed battle, duplicate victory and offline respawn.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
