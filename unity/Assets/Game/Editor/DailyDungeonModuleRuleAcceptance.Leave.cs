using System;
using System.Collections.Generic;
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
        private static void ValidatePartialLeave(ProfileSlotCatalog slots, DailyDungeonRuntimeData catalog,
            DailyDungeonRuntimeBattle dungeon, DailyDungeonRuntimeDifficulty difficulty, List<int> route)
        {
            string slot = slots.CreateSlot();
            var module = DailyDungeonModule.Open(slots, slot, catalog);
            int claims = module.RemainEntries;
            if (!module.TryBegin(dungeon.id, difficulty.difficulty, _ => true, (_, __) => true,
                    (_, __, ___) => true, catalog.settings.DailyDungeonBuyCostItemId,
                    DateTimeOffset.Now.ToUnixTimeSeconds(), out string error))
                throw new InvalidOperationException(error);
            int grade = 0;
            for (int index = 0; index < route.Count - 1; index++)
            {
                if (!module.RecordRoomCleared(route[index], string.Empty))
                    throw new InvalidOperationException("Partial route could not clear its official room.");
                grade = module.CurrentGrade();
                if (!module.ChooseNextRoom(route[index + 1]))
                    throw new InvalidOperationException("Partial route could not enter its next official room.");
                if (grade > 0) break;
            }
            if (grade <= 0) throw new InvalidOperationException("Official route has no nonterminal grade fixture.");
            int currentRoom = module.ActiveRun.currentRoomId;
            int[] cleared = module.ActiveRun.clearedRoomIds.ToArray();
            if (!module.Leave((_, __) => throw new InvalidOperationException("Graded exit bypassed the normal claim transaction.")))
                throw new InvalidOperationException("Partial daily dungeon could not settle its grade.");
            module = DailyDungeonModule.Open(slots, slot, catalog);
            var pending = module.PendingClaims.Single();
            if (module.ActiveRun != null || pending.run.grade != grade
                || !pending.run.clearedRoomIds.SequenceEqual(cleared)
                || pending.run.clearedRoomIds.Contains(currentRoom)
                || !string.IsNullOrEmpty(pending.run.activeBattleCheckpoint)
                || module.RemainEntries != claims || module.Save.completionCounts.Count != 0
                || module.Save.leaderboard.Any(value => !value.npc))
                throw new InvalidOperationException("Partial exit fabricated a cleared room, full completion, leaderboard or claim charge.");
            if (!module.ConfiguredRewardRuleIds(pending.run, true).SequenceEqual(
                    module.ClearedRoomRewardRuleIds(pending.run).Concat(module.FindMap(pending.run.mapGroupId)
                        .gradeRewards.Single(value => value.difficulty == difficulty.difficulty && value.grade == grade).rewardRuleIds)))
                throw new InvalidOperationException("Partial exit did not use the original room and current-grade reward rules.");
            var characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            if (!profile.ApplyCharacterMutation(player => player.Level = 40, "partial-leave-fixture").IsApplied)
                throw new InvalidOperationException("Partial settlement player fixture could not be saved.");
            var progression = new OfflineCharacterProgressionService(profile, characterAssets.Load());
            progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
            progression.AttachSkillRules(characterAssets.LoadSkillRules());
            var settlement = new OfflineDailyDungeonSettlementModule(module, profile, progression, () => 1700000000);
            var result = settlement.Claim(pending.id, 1);
            string inventory = JsonUtility.ToJson(profile.Save.Player);
            var repeated = settlement.Claim(pending.id, 1);
            if (!result.Committed || !repeated.Committed || !result.HasRewardDetails
                || !result.Items.Select(value => value.ItemId + ":" + value.Count)
                    .SequenceEqual(repeated.Items.Select(value => value.ItemId + ":" + value.Count))
                || inventory != JsonUtility.ToJson(profile.Save.Player) || module.RemainEntries != claims - 1
                || module.DailyDungeonGetRewardCount != 1 || module.PendingClaims.Count != 0)
                throw new InvalidOperationException("Partial-grade rewards were lost, duplicated or charged incorrectly.");
            Debug.Log("Daily dungeon partial exit passed: cleared-row grade, room/configured grade rewards, cold reopen, normal claim and duplicate receipt.");
        }
    }
}
