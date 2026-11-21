using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static int LoadAcquiredPassiveCompanion(OfflinePlayerProgress player, int active, string output)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_PASSIVE_SAVE");
            if (string.IsNullOrWhiteSpace(path)) return 0;
            var source = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            var plan = source.Player.Skills.Plans[source.Player.Skills.SelectedPlan];
            int itemId = plan.PassiveSkills[0];
            Require(active == 10047 && player.Wardrobe.Profession == 4 && source.Player.Wardrobe.Profession == 4
                && source.Player.Level == player.Level && source.Player.SubRank == player.SubRank
                && plan.ActiveSkills.All(id => id == 0) && (itemId == 10606 || itemId == 10607)
                && plan.PassiveSkills.Count(id => id > 0) == 1
                && OfflineSkillProgression.OwnedRank(source.Player.Skills, itemId) > 1
                && source.Player.Skills.PassiveSlotLevels[0] == 2,
                "The companion must be an actual isolated cold-glint or frigid-aura acquisition/cultivation save.");
            player.Skills = source.Player.Skills;
            File.WriteAllText(Path.Combine(output, "companion-cultivated-save.json"), JsonUtility.ToJson(source, true));
            return itemId;
        }

        private static void VerifyAcquiredPassiveAssembly(FightBattleUnitState actor, OfflineCharacterProgressionService progression, int itemId)
        {
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId);
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            var item = OfficialCombatAssetCatalog.LoadDefault().LoadSkillItems().Required(itemId);
            foreach (int statusId in item.PassiveStatusIds)
                Require(actor.Statuses.Any(status => status.ClassId == statusId && status.ApplyingSkillItemId == itemId
                    && status.CreatorId == actor.Id && status.SourceSkillRank == rank && status.SourceSkillLevel == 2
                    && status.SourceSubRank == actor.SubRank), "The battle assembler lost the cultivated passive listener.");
            var bonus = combat.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                item.PassiveRankFactorId, item.PassivePropFactors, rank, 2, actor.SubRank);
            var baseline = progression.PropertyCalculator.Evaluate(progression.Player,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).OutOfFightBattleProps;
            foreach (var pair in bonus)
                Require(actor.Properties.Single(value => value.Id == pair.Key).Value
                    == (baseline.TryGetValue(pair.Key, out long value) ? value : 0) + pair.Value,
                    "The real battle omitted or duplicated cold-glint water affinity.");
        }

        private static void VerifyAcquiredPassiveStep(FightBattleStepEvent step, FightBattleCheckpoint checkpoint,
            string output, int itemId, ref int coldHits, ref int auraHits)
        {
            if (itemId == 10607)
            {
                var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 12170).ToArray();
                foreach (var hit in hits)
                    Require(hit.SourceId == 1 && hit.TargetId == step.ActorId && hit.SkillItemId == 10607
                        && hit.HitEntityId == 12171 && hit.DamageEntityId == 12172 && hit.ElementType == 101
                        && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure,
                        "The real frigid aura lost its original water child or enemy-turn-end source.");
                auraHits += hits.Length;
            }
            var applications = step.StatusViewEvents.Where(view => view.Status.ClassId == 24266
                && view.Status.CreatorId == 1 && (view.Kind == FightBattleStatusViewEventKind.Start
                    || view.Kind == FightBattleStatusViewEventKind.Update)).ToArray();
            if (applications.Length == 0) return;
            Require(step.DamageEvents.Any(hit => hit.SourceId == 1 && hit.ElementType == 101
                && (itemId != 10607 || hit.SkillEntityId == 12170)
                && !hit.Dodge && !hit.Blinding && !hit.Immunity && (hit.Damage < 0 || hit.ShieldDamage > 0)),
                "Cold glint triggered without a successful water hit.");
            foreach (var application in applications)
            {
                var owner = checkpoint.Units.Single(unit => unit.Id == application.OwnerId);
                var cold = owner.Statuses.Single(status => status.ClassId == 24266 && status.CreatorId == 1);
                Require(owner.Side != checkpoint.Units.Single(unit => unit.Id == 1).Side
                    && cold.ApplyingSkillItemId == itemId && cold.SourceSkillLevel == 2
                    && cold.StackCount >= 1 && cold.StackCount <= 10 && cold.DurationRound == 2,
                    "Cold glint lost its enemy target, secret source, cultivation or original cold rules.");
            }
            coldHits += applications.Length;
            File.WriteAllText(Path.Combine(output, "companion-cold-checkpoint.json"), FightBattleCheckpointSerializer.Save(checkpoint));
            Debug.Log("[Acquired passive combat] " + itemId + " cultivated passive and original cold application passed.");
        }
    }
}
