using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static int VerifyAcquiredSagePsychicWard(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, FightBattleUnitState actor, string output)
        {
            var starts = step.StatusViewEvents.Where(view => view.Status.ClassId == 24181
                && view.Kind == FightBattleStatusViewEventKind.Start).ToArray();
            foreach (var start in starts)
            {
                var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11760);
                var mark = actor.Statuses.Single(value => value.ClassId == 24181
                    && value.InstanceId == start.Status.InstanceId);
                Require(start.OwnerId == actor.Id && mark.CreatorId == actor.Id
                    && mark.ApplyingSkillItemId == 10077 && mark.SourceSkillLevel == 2
                    && mark.DurationRound == -1 && mark.CanDispersed && mark.ShieldKind == FightBattleShieldKind.None
                    && Math.Abs(start.Delay - cast.Delay - .4f) < .0001f,
                    "The cultivated psychic ward changed target, origin, duration or impact timing.");
            }
            bool sufferedDamage = step.DamageEvents.Any(hit => hit.TargetId == actor.Id
                && hit.SourceId != actor.Id && hit.Damage < 0 && !hit.IsCure && !hit.TrueDamage);
            if (!sufferedDamage) return 0;
            var ends = step.StatusViewEvents.Where(view => view.OwnerId == actor.Id
                && view.Status.ClassId == 24181 && view.Kind == FightBattleStatusViewEventKind.End).ToArray();
            if (ends.Length == 0) return 0;
            Require(ends.Length == 1 && actor.Statuses.All(status => status.ClassId != 24181),
                "The original psychic mark did not consume once on a settled incoming hit.");
            string path = Path.Combine(output, "acquired-sage-psychic-ward-checkpoint.json");
            if (!File.Exists(path))
            {
                string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(serialized);
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
                Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == serialized,
                    "Restoring the psychic mark changed the consumed status or settled hit.");
                File.WriteAllText(path, serialized);
            }
            return ends.Length;
        }
    }
}
