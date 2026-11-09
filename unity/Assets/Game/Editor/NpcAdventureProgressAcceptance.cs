using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class NpcAdventureProgressAcceptance
    {
        public static void Run()
        {
            try
            {
                ProjectTools.Validate();
                string validationRoot = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (string.IsNullOrWhiteSpace(validationRoot))
                    throw new InvalidOperationException("Set ZJCS_VALIDATION_DIR to an owned validation workspace.");
                string profileRoot = Path.Combine(Path.GetFullPath(validationRoot),
                    "npc-adventure-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(profileRoot);
                var slots = new ProfileSlotCatalog(profileRoot);
                string slotId = slots.CreateSlot();
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
                OfficialNpcTravelCatalog travel = OfficialNpcTravelCatalog.LoadDefault();
                OfficialNpcAdventureBattleCatalog battles = OfficialNpcAdventureBattleCatalog.LoadDefault();
                var adventure = new OfflineNpcAdventureSession(profile, travel);
                NpcTravelNode node = adventure.FirstNode;
                if (node == null) throw new InvalidOperationException("The authorized NPC Adventure has no first stage.");
                DateTime now = DateTime.Now.Date.AddHours(Math.Min(22, travel.Data.daily_reset_hour + 1));

                FightOfficialNpcAssistanceCatalog assistanceCatalog = OfficialCombatAssetCatalog.LoadDefault()
                    .LoadNpcAssistance();
                Zjcs.Presentation.Combat.OfficialBattleModelCatalog battleModels =
                    Zjcs.Presentation.Combat.OfficialBattleModelCatalog.LoadDefault();
                int[] selectedNpcIds = assistanceCatalog.FightAssistNpcClassIds()
                    .Where(battleModels.HasNpcBattleAnimations)
                    .Take(OfflineNpcAdventureSession.MaximumCompanions).ToArray();
                Require(selectedNpcIds.Length == OfflineNpcAdventureSession.MaximumCompanions,
                    "The official NPC catalog does not provide three assist candidates.");
                Require(adventure.SetSelectedNpcClassIds(selectedNpcIds).IsApplied,
                    "Could not persist the NPC Adventure selection.");
                Require(string.IsNullOrEmpty(adventure.CreateSweepReceipt(node, now)),
                    "The first Bond Adventure stage must be fought manually.");

                VerifyBattleAttempt(profile, adventure, battles, node, now);
                VerifyAssistSnapshot(slots, slotId, assistanceCatalog, selectedNpcIds, node, now);
                VerifySweepTransaction(profile, adventure, battles, adventure.NextNode, now);
                VerifyDailyLimit(profile, adventure, now);

                Debug.Log("NPC Adventure acceptance passed: battle attempts, loss settlement, PlayType 6 snapshots, sweep awards, progression, and daily limit.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void VerifyBattleAttempt(OfflineMainlineProgressSession profile,
            OfflineNpcAdventureSession adventure, OfficialNpcAdventureBattleCatalog battles,
            NpcTravelNode node, DateTime now)
        {
            ProgressionResult started = adventure.BeginBattle(node, now);
            Require(started.IsApplied && adventure.RemainingBattleCount(now) == adventure.DailyBattleLimit - 1,
                "Starting a Bond Adventure battle did not consume exactly one attempt.");
            Require(adventure.CancelBattleStart(node.stage_id).IsApplied
                && adventure.RemainingBattleCount(now) == adventure.DailyBattleLimit,
                "A pre-battle startup failure did not refund its attempt.");

            started = adventure.BeginBattle(node, now);
            Require(started.IsApplied, "A Bond Adventure battle could not start after setup rollback.");
            string receipt = adventure.State.ActiveBattleReceipt;
            ProgressionResult lost = adventure.CompleteBattle(node.stage_id, false,
                Array.Empty<KeyValuePair<int, long>>(), receipt);
            Require(lost.IsApplied && adventure.RemainingBattleCount(now) == adventure.DailyBattleLimit - 1
                && SameNode(adventure.NextNode, node),
                "A lost battle must keep its attempt, award nothing, and leave the next stage unchanged.");

            started = adventure.BeginBattle(node, now);
            Require(started.IsApplied, "The next Bond Adventure battle could not start after a loss.");
            receipt = adventure.State.ActiveBattleReceipt;
            List<NpcAdventureRewardItem> rewards = RollConfiguredRewards(battles, node.stage_id,
                profile.Save.Player.Level, profile.Save.Player.SubRank);
            Require(rewards.Count > 0, "The winning stage.Awards rules did not produce a reward.");
            NpcAdventureRewardItem reward = rewards[0];
            long before = profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == reward.ItemId)?.Count ?? 0;
            var rewardPairs = rewards.Select(value =>
                new KeyValuePair<int, long>(value.ItemId, value.Count)).ToList();
            ProgressionResult won = adventure.CompleteBattle(node.stage_id, true, rewardPairs, receipt);
            profile.Reload();
            long after = profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == reward.ItemId)?.Count ?? 0;
            Require(won.IsApplied && adventure.RemainingBattleCount(now) == adventure.DailyBattleLimit - 2
                && !SameNode(adventure.NextNode, node) && after == before + reward.Count,
                "A winning battle did not atomically preserve its attempt, award stage.Awards, and advance the node.");
        }

        private static void VerifyAssistSnapshot(ProfileSlotCatalog slots, string slotId,
            FightOfficialNpcAssistanceCatalog catalog, IReadOnlyList<int> npcIds,
            NpcTravelNode node, DateTime now)
        {
            int playType = (int)NpcAssistPlayType.BondAdventure;
            FightOfficialNpcInviteRule rule = catalog.InviteRule(playType);
            OfflineNpcAssistanceProgress progress = OfflineNpcAssistanceProgress.Open(slots, slotId);
            const string finishedEntry = "npc-adventure-acceptance-finished";
            Require(progress.SetSelection(playType, finishedEntry, npcIds, rule.MaxPlayer),
                "The PlayType 6 selection could not be saved.");
            OfflineNpcAssistBattle active = progress.CommitBattle(playType, finishedEntry, node.stage_id,
                rule.MaxPlayer, rule.OneNpcMaxInviteCount, rule.MaxInviteCount,
                (NpcAssistRefreshCycle)rule.RefreshCycle, new DateTimeOffset(now).ToUnixTimeSeconds());
            Require(active != null && active.npcClassIds.SequenceEqual(npcIds),
                "The PlayType 6 battle snapshot does not contain the selected partners.");
            progress.FinishBattle(playType, finishedEntry, node.stage_id);
            Require(progress.ActiveBattle == null && progress.AssistCount(playType, npcIds[0],
                    (NpcAssistRefreshCycle)rule.RefreshCycle, new DateTimeOffset(now).ToUnixTimeSeconds()) == 1,
                "Finishing the battle did not preserve the committed assistance count.");

            const string cancelledEntry = "npc-adventure-acceptance-cancelled";
            Require(progress.SetSelection(playType, cancelledEntry, npcIds, rule.MaxPlayer),
                "The second PlayType 6 selection could not be saved.");
            progress.CommitBattle(playType, cancelledEntry, node.stage_id, rule.MaxPlayer,
                rule.OneNpcMaxInviteCount, rule.MaxInviteCount, (NpcAssistRefreshCycle)rule.RefreshCycle,
                new DateTimeOffset(now).ToUnixTimeSeconds());
            progress.CancelBattle(playType, cancelledEntry, node.stage_id);
            Require(progress.ActiveBattle == null && progress.AssistCount(playType, npcIds[0],
                    (NpcAssistRefreshCycle)rule.RefreshCycle, new DateTimeOffset(now).ToUnixTimeSeconds()) == 1,
                "Cancelling a battle startup did not roll back only its assistance count.");
        }

        private static void VerifySweepTransaction(OfflineMainlineProgressSession profile,
            OfflineNpcAdventureSession adventure, OfficialNpcAdventureBattleCatalog battles,
            NpcTravelNode node, DateTime now)
        {
            string receipt = adventure.CreateSweepReceipt(node, now);
            Require(!string.IsNullOrEmpty(receipt), "The current next stage did not produce a sweep receipt.");
            List<NpcAdventureRewardItem> rewards = RollConfiguredRewards(battles, node.stage_id,
                profile.Save.Player.Level, profile.Save.Player.SubRank);
            Require(rewards.Count > 0,
                "The configured stage.Awards did not produce a deterministic acceptance reward.");
            NpcAdventureRewardItem award = rewards[0];
            long before = profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == award.ItemId)?.Count ?? 0;
            ProgressionResult invalid = adventure.SweepNextStage(node,
                new[] { new KeyValuePair<int, long>(0, 1) }, receipt, now);
            Require(!invalid.IsApplied && SameNode(adventure.NextNode, node)
                && adventure.State.DailyBattleCount == 2,
                "A rejected sweep reward changed progress or consumed an attempt.");

            ProgressionResult swept = adventure.SweepNextStage(node,
                new[] { new KeyValuePair<int, long>(award.ItemId, award.Count) }, receipt, now);
            profile.Reload();
            long after = profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == award.ItemId)?.Count ?? 0;
            Require(swept.IsApplied && !SameNode(adventure.NextNode, node)
                && adventure.State.DailyBattleCount == 3 && after == before + award.Count,
                "A valid sweep did not atomically consume one attempt, grant stage.Awards, and advance the node.");
            Require(!SameNode(adventure.NextNode, node)
                && string.IsNullOrEmpty(adventure.CreateSweepReceipt(node, now)),
                "A completed stage remained eligible for another sweep.");
        }

        private static void VerifyDailyLimit(OfflineMainlineProgressSession profile,
            OfflineNpcAdventureSession adventure, DateTime now)
        {
            int localDayKey = adventure.State.LocalDayKey;
            ProgressionResult filled = profile.ApplyCharacterMutationIfChanged(player =>
            {
                player.NpcAdventure.LocalDayKey = localDayKey;
                player.NpcAdventure.DailyBattleCount = adventure.DailyBattleLimit;
                return true;
            });
            Require(filled.IsApplied && adventure.RemainingBattleCount(now) == 0,
                "The official daily battle limit was not enforced.");
            Require(string.IsNullOrEmpty(adventure.CreateSweepReceipt(adventure.NextNode, now)),
                "Sweep created a receipt after the daily attempt limit was reached.");
        }

        private static bool SameNode(NpcTravelNode left, NpcTravelNode right) => left != null && right != null
            && left.map_group_id == right.map_group_id && left.region_id == right.region_id
            && left.node_id == right.node_id && left.stage_id == right.stage_id;

        private static List<NpcAdventureRewardItem> RollConfiguredRewards(
            OfficialNpcAdventureBattleCatalog battles, int stageId, int level, int subRank)
        {
            for (int seed = 1; seed <= 4096; seed++)
            {
                ulong random = (ulong)seed;
                List<NpcAdventureRewardItem> rewards = battles.RollRewards(stageId, level, subRank, ref random);
                if (rewards.Count > 0) return rewards;
            }
            return new List<NpcAdventureRewardItem>();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
