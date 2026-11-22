using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredStarBurst(FightBattleStepEvent step, FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, FightBattleCheckpoint checkpoint, string output, ref int applications)
        {
            Require(hits.Length > 0 && hits.All(hit => hit.SourceId == 1 && hit.TargetId != 1
                && hit.SkillItemId == 10002 && hit.HitEntityId == 11451 && hit.DamageEntityId == 11452
                && hit.ElementType == 103 && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                && Math.Abs(hit.Delay - cast.Delay - .5f) < .0001f
                && hit.HasDamagePosition && Math.Abs(hit.DamageX - cast.Presentation.TargetX)
                    + Math.Abs(hit.DamageY - cast.Presentation.TargetY) <= 2),
                "The cultivated star burst lost its original point-centred light area or timing.");
            foreach (var view in step.StatusViewEvents.Where(view => view.Status.ClassId == 24234
                && view.Status.CreatorId == 1 && (view.Kind == FightBattleStatusViewEventKind.Start
                    || view.Kind == FightBattleStatusViewEventKind.Update)))
            {
                var owner = checkpoint.Units.Single(unit => unit.Id == view.OwnerId);
                var status = owner.Statuses.Single(value => value.ClassId == 24234 && value.CreatorId == 1);
                Require(owner.Side != 1 && status.ApplyingSkillItemId == 10002 && status.SourceSkillLevel == 2
                    && status.SourceSkillRank == checkpoint.Units.Single(unit => unit.Id == 1).Skills.Single(skill => skill.Slot == 0).Rank
                    && status.SourceSubRank == 8 && status.ActionType == 63 && status.DurationRound == -1
                    && status.Components.HitDmgAddPer.DurationSkillCount == 1,
                    "The cultivated star burst lost its next-damaging-skill blinding status.");
                applications++;
            }
            if (applications > 0) File.WriteAllText(Path.Combine(output, "acquired-star-burst-blinding-checkpoint.json"),
                FightBattleCheckpointSerializer.Save(checkpoint));
        }
    }
}
