using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static partial class DailyDungeonModuleRuleAcceptance
    {
        private static void ValidateSettlement(ProfileSlotCatalog slots, DailyDungeonRuntimeData catalog,
            DailyDungeonPendingClaimSave pending)
        {
            var characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            var character = characterAssets.Load();
            var equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
            foreach (string phase in new[] { "reservation", "inventory", "confirmation", "success" })
            {
                string slot = slots.CreateSlot();
                var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                var setup = profile.ApplyCharacterMutation(player => { player.Level = 40; }, "settlement:setup");
                if (!setup.IsApplied) throw new InvalidOperationException(setup.Message);
                var progression = new OfflineCharacterProgressionService(profile, character);
                progression.AttachEquipmentCatalog(equipment);
                progression.AttachSkillRules(characterAssets.LoadSkillRules());
                string dungeonPath = Path.Combine(slots.GetSlotDirectory(slot), "daily-dungeon-save.json");
                var state = new DailyDungeonSave();
                state.pendingClaims.Add(JsonUtility.FromJson<DailyDungeonPendingClaimSave>(JsonUtility.ToJson(pending)));
                File.WriteAllText(dungeonPath, JsonUtility.ToJson(state));
                var dungeon = DailyDungeonModule.Open(slots, slot, catalog);
                int remaining = dungeon.RemainEntries;
                var settlement = new OfflineDailyDungeonSettlementModule(dungeon, profile, progression, () => 1700000000);
                if (settlement.Claim(pending.id, 0).Committed || settlement.Claim(pending.id, remaining + 1).Committed)
                    throw new InvalidOperationException("Invalid claim counts were accepted.");
                string blocked = phase == "inventory" ? slots.GetProgressPath(slot) + ".tmp" : dungeonPath + ".tmp";
                if (phase == "confirmation")
                {
                    // Simulate death after inventory commit by making the next dungeon write fail.
                    dungeon.ClaimPendingReward(pending.id, (receipt, run, times) => {
                        var result = settlement.SettleClearedRooms(receipt, run);
                        if (!result.InventoryCommitted) throw new InvalidOperationException(result.Message);
                        return false;
                    });
                }
                if (phase != "success") Directory.CreateDirectory(blocked);
                try
                {
                    var result = settlement.Claim(pending.id, 1);
                    if (phase != "success" && result.Committed)
                        throw new InvalidOperationException("A failed persistence stage reported confirmation.");
                }
                finally { if (phase != "success") Directory.Delete(blocked); }
                // Reopen both files: recovery cannot depend on the previous module instances.
                profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                progression = new OfflineCharacterProgressionService(profile, character);
                progression.AttachEquipmentCatalog(equipment);
                progression.AttachSkillRules(characterAssets.LoadSkillRules());
                dungeon = DailyDungeonModule.Open(slots, slot, catalog);
                settlement = new OfflineDailyDungeonSettlementModule(dungeon, profile, progression, () => 1700000000);
                var recovered = settlement.Claim(pending.id, 1);
                string saved = JsonUtility.ToJson(profile.Save.Player);
                var repeated = settlement.Claim(pending.id, 1);
                if (!recovered.Committed || !recovered.HasRewardDetails || !repeated.Committed
                    || JsonUtility.ToJson(profile.Save.Player) != saved || dungeon.RemainEntries != remaining - 1
                    || dungeon.DailyDungeonGetRewardCount != 1 || dungeon.PendingClaims.Count != 0
                    || profile.Save.Player.DailyDungeonSettlements.Count != 1
                    || JsonUtility.ToJson(profile.Save.Player.DailyDungeonSettlements.Single()) == "{}")
                    throw new InvalidOperationException("Settlement recovery or duplicate receipt changed rewards/counts: " + phase);
                var record = profile.Save.Player.DailyDungeonSettlements.Single();
                if (record.EquipmentInstanceIds.Any(id => !profile.Save.Player.Equipment.Instances.Any(item => item.InstanceId == id))
                    || record.Items.Any(item => character.ItemType(item.ItemId) == 110))
                    throw new InvalidOperationException("Settlement details contain placeholders or missing equipment instances.");
            }
            Debug.Log("Dungeon settlement passed: durable reward details, reservation/inventory/confirmation failures, cold reopen and duplicate receipts.");
        }
    }
}
