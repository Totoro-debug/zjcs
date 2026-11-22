using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredSpiralWindBlade(FightBattleStepEvent step, FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, FightBattleCheckpoint checkpoint, string output, ref int applications)
        {
            Require(hits.Length >= 3 && hits.All(hit => hit.SourceId == 1 && hit.TargetId != 1
                && hit.SkillItemId == 10091 && hit.HitEntityId == 11961 && hit.DamageEntityId == 11962
                && hit.ElementType == 100 && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                && new[] { .65f, .85f, 1.05f }.Any(delay => Math.Abs(hit.Delay - cast.Delay - delay) < .0001f)
                && hit.HasDamagePosition && Math.Abs(hit.DamageX - cast.Presentation.TargetX) <= 1
                && Math.Abs(hit.DamageY - cast.Presentation.TargetY) <= 1),
                "The cultivated spiral wind blade lost its original three wind hits, square area or timing.");
            int current = 0;
            foreach (var view in step.StatusViewEvents.Where(view => view.Status.ClassId == 24376
                && view.Status.CreatorId == 1 && (view.Kind == FightBattleStatusViewEventKind.Start
                    || view.Kind == FightBattleStatusViewEventKind.Update)))
            {
                var owner = checkpoint.Units.Single(unit => unit.Id == view.OwnerId);
                var status = owner.Statuses.Single(value => value.ClassId == 24376 && value.CreatorId == 1);
                Require(owner.Side != 1 && status.ApplyingSkillItemId == 10091 && status.SourceSkillLevel == 2
                    && status.SourceSkillRank == checkpoint.Units.Single(unit => unit.Id == 1).Skills.Single(skill => skill.Slot == 0).Rank
                    && status.SourceSubRank == 8 && status.DurationRound == 1 && status.RoundTarget == 2
                    && status.RoundUpdateTiming == 1 && status.ActionStartRulesSupported,
                    "The cultivated spiral wind blade lost its original one-round laceration.");
                current++;
            }
            applications += current;
            if (current > 0) File.WriteAllText(Path.Combine(output, "acquired-spiral-wind-blade-checkpoint.json"),
                FightBattleCheckpointSerializer.Save(checkpoint));
        }

        private static void VerifyAcquiredLacerationHits(FightBattleStepEvent step, ref int count)
        {
            foreach (var hit in step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.SkillEntityId == 11950))
            {
                Require(hit.TargetId != 1 && hit.SkillItemId == 10091 && hit.HitEntityId == 11951
                    && hit.DamageEntityId == 10004 && hit.ElementType == 100 && hit.Damage < 0 && !hit.IsCure && !hit.TrueDamage,
                    "Movement laceration lost its actual cultivated wind child or creator attribution.");
                count++;
            }
        }
    }
}
