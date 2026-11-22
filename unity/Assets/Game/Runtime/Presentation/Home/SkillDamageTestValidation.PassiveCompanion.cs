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
            bool supported = active == 10047 && player.Wardrobe.Profession == 4 && (itemId == 10606 || itemId == 10607)
                || active == 10088 && player.Wardrobe.Profession == 8 && (itemId == 10608 || itemId == 10568 || itemId == 10595 || itemId == 10543)
                || active == 10068 && player.Wardrobe.Profession == 8 && itemId == 10609;
            supported |= active == 10089 && player.Wardrobe.Profession == 8 && itemId == 10594;
            supported |= active == 10084 && player.Wardrobe.Profession == 4 && itemId == 10592;
            Require(supported && source.Player.Wardrobe.Profession == player.Wardrobe.Profession
                && source.Player.Level == player.Level && source.Player.SubRank == player.SubRank
                && plan.ActiveSkills.All(id => id == 0)
                && plan.PassiveSkills.Count(id => id > 0) == 1
                && OfflineSkillProgression.OwnedRank(source.Player.Skills, itemId) > 1
                && source.Player.Skills.PassiveSlotLevels[0] == 2,
                "The companion must be an actual isolated acquisition/cultivation save for the selected profession.");
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
                    "The real battle omitted or duplicated the cultivated passive properties.");
        }

        private static void VerifyAcquiredPassiveStep(FightBattleStepEvent step, FightBattleCheckpoint checkpoint,
            string output, int itemId, ref int coldHits, ref int auraHits, ref int standfastTurns, ref int assaultHits, ref int soulBreakHits, ref int windHits, ref int flameHits)
        {
            if (itemId == 10592)
            {
                VerifyAcquiredSteelFaith(checkpoint, output);
                return;
            }
            if (itemId == 10543)
            {
                var primary = step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.SkillEntityId == 11910
                    && hit.Critical && hit.HasDamageValue && hit.DamageValue < 0).ToArray();
                var children = step.SkillCasts.Where(cast => cast.ActorId == 1 && cast.SkillEntityId == 11150).ToArray();
                Require(children.Length == primary.Length && children.All(cast => cast.SkillItemId == itemId
                    && cast.Presentation.HasEntityPosition && primary.Any(hit => hit.HasDamagePosition
                        && hit.DamageX == cast.Presentation.EntityX && hit.DamageY == cast.Presentation.EntityY
                        && Math.Abs(hit.Delay - cast.Delay) < .0001f)),
                    "The cultivated critical blast lost its original damage centre or trigger count.");
                foreach (var hit in step.DamageEvents.Where(value => value.SkillEntityId == 11150))
                {
                    Require(hit.SourceId == 1 && hit.SkillItemId == itemId && hit.HitEntityId == 11151
                        && hit.DamageEntityId == 10023 && hit.ElementType == 102 && hit.DamageType == 1
                        && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure,
                        "The cultivated critical blast lost its original fire damage or source.");
                    flameHits++;
                }
                if (flameHits > 0) File.WriteAllText(Path.Combine(output, "companion-flame-momentum-checkpoint.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                return;
            }
            if (itemId == 10595)
            {
                foreach (var hit in step.DamageEvents.Where(value => value.SkillEntityId == 12030))
                {
                    Require(hit.SourceId == 1 && hit.SkillItemId == itemId && hit.HitEntityId == 12031
                        && hit.DamageEntityId == 12032 && hit.ElementType == 100 && hit.DamageType == 1
                        && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure,
                        "The cultivated wind orb lost its original child damage or source.");
                    windHits++;
                }
                foreach (var view in step.StatusViewEvents.Where(value => value.Status.ClassId == 24385))
                    Require(view.OwnerId == 1 && view.Status.CreatorId == 1
                        && view.Status.StackCount >= 0 && view.Status.StackCount <= 10
                        && checkpoint.Units.Single(unit => unit.Id == 1).Statuses.Any(status => status.ClassId == 24385
                            && status.ApplyingSkillItemId == itemId && status.SourceSkillLevel == 2),
                        "The cultivated wind orb lost its holder, cultivation or shared stack limit.");
                if (windHits > 0) File.WriteAllText(Path.Combine(output, "companion-wind-orb-checkpoint.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                return;
            }
            if (itemId == 10594)
            {
                foreach (var hit in step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.SkillItemId == 10089
                    && hit.Damage < 0 && !hit.IsCure && !hit.Dodge && !hit.Blinding && !hit.Immunity))
                {
                    var target = checkpoint.Units.Single(unit => unit.Id == hit.TargetId);
                    if (target.MaxHp - hit.BeforeHp >= target.MaxHp * .15m) soulBreakHits++;
                }
                if (soulBreakHits > 0) File.WriteAllText(Path.Combine(output, "companion-soul-breaker-checkpoint.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                return;
            }
            if (itemId == 10568)
            {
                var owner = checkpoint.Units.Single(unit => unit.Id == 1);
                var passive = owner.Statuses.Single(status => status.ClassId == 25081);
                assaultHits += step.DamageEvents.Count(hit => hit.SourceId == 1 && !hit.IsCure
                    && !hit.Dodge && !hit.Blinding && !hit.Immunity && (hit.Damage < 0 || hit.ShieldDamage > 0));
                Require(passive.Components.HitDmgAddPer.InvokeCount == assaultHits,
                    "The original defence-ignore component did not follow each successful cultivated hit.");
                if (assaultHits > 0) File.WriteAllText(Path.Combine(output, "companion-assault-checkpoint.json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                return;
            }
            if (itemId == 10609)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == 25118);
                var children = actor.Statuses.Where(status => status.ClassId == 24393 || status.ClassId == 24394).ToArray();
                Require(children.Length == 1 && children[0].ClassId == (listener.MoveRangeLastTargetCount <= 3 ? 24394 : 24393)
                    && children[0].ApplyingSkillItemId == itemId && children[0].CreatorId == actor.Id
                    && children[0].OwnerId == actor.Id && children[0].SourceSkillRank == listener.SourceSkillRank
                    && children[0].SourceSkillLevel == 2 && children[0].SourceSubRank == actor.SubRank
                    && children[0].DurationRound == -1 && !children[0].CanDispersed,
                    "The real tactical adaptation lost its exclusive threshold effect or cultivated source.");
                File.WriteAllText(Path.Combine(output, "companion-tactical-" + children[0].ClassId + ".json"),
                    FightBattleCheckpointSerializer.Save(checkpoint));
                return;
            }
            if (itemId == 10608)
            {
                var starts = step.StatusViewEvents.Where(view => view.Status.ClassId == 24392
                    && view.Kind == FightBattleStatusViewEventKind.Start).ToArray();
                foreach (var start in starts)
                    Require(step.ActorId == 1 && start.OwnerId == 1 && start.Status.CreatorId == 1
                        && step.StatusViewEvents.Any(end => end.Kind == FightBattleStatusViewEventKind.End
                            && end.OwnerId == 1 && end.Status.InstanceId == start.Status.InstanceId
                            && end.Delay >= start.Delay),
                        "The real standfast soul escaped its owner's turn boundaries.");
                Require(!checkpoint.Units.Single(unit => unit.Id == 1).Statuses.Any(status => status.ClassId == 24392),
                    "The real standfast soul remained after its owner's turn ended.");
                standfastTurns += starts.Length;
                return;
            }
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
