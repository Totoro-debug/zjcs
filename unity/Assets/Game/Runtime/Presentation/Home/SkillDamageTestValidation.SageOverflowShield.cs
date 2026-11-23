using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static int VerifyAcquiredSageOverflowShield(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, FightBattleUnitState actor, string output)
        {
            var starts = step.StatusViewEvents.Where(view => view.Status.ClassId == 24174
                && view.Kind == FightBattleStatusViewEventKind.Start).ToArray();
            var cures = step.DamageEvents.Where(hit => hit.SourceId == actor.Id && hit.SkillItemId == 10096
                && hit.IsCure && hit.DamageValue > hit.Damage).ToArray();
            Require(starts.Length == cures.Length,
                "The cultivated passive did not apply one original shield for each overflowing cure.");
            if (starts.Length == 0) return 0;
            var listener = actor.Statuses.Single(status => status.ClassId == 25106
                && status.ApplyingSkillItemId == 10601);
            for (int index = 0; index < starts.Length; index++)
            {
                var cure = cures[index];
                var start = starts[index];
                long overflow = cure.DamageValue - cure.Damage;
                var shield = checkpoint.Units.Single(unit => unit.Id == cure.TargetId).Statuses.Single(status =>
                    status.ClassId == 24174 && status.InstanceId == start.Status.InstanceId);
                Require(start.OwnerId == cure.TargetId && start.Status.ShieldMaximum == overflow
                    && start.Status.ShieldRemaining == overflow && shield.ShieldRemainValue == overflow
                    && shield.DurationRound == 2 && shield.CreatorId == actor.Id && !shield.CanDispersed
                    && shield.ApplyingSkillItemId == 10601 && shield.SourceSkillRank == listener.SourceSkillRank
                    && shield.SourceSkillLevel == listener.SourceSkillLevel,
                    "The original overflow shield changed amount, recipient, duration or cultivation.");
            }
            string path = Path.Combine(output, "companion-overflow-shield-checkpoint.json");
            if (!File.Exists(path))
            {
                string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(serialized);
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
                Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == serialized,
                    "Restoring the cultivated overflow shield changed its settled cure or shield.");
                File.WriteAllText(path, serialized);
            }
            return starts.Length;
        }
    }
}
