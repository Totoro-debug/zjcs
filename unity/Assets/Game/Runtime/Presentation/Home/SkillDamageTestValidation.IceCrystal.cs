using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredIceCrystal(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var bursts = step.DamageEvents.Where(hit => hit.SkillEntityId == 11410).ToArray();
            if (bursts.Length == 0) return;
            foreach (var hit in bursts)
            {
                Require(hit.SourceId == 1 && hit.TargetId != 1 && hit.SkillItemId == 10557
                    && hit.HitEntityId == 11411 && hit.DamageEntityId == 10005 && hit.DamageType == 1
                    && hit.ElementType == 101 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure,
                    "The cultivated ice crystal did not deal its original water damage.");
                Require(step.StatusViewEvents.Any(value => value.OwnerId == hit.TargetId && value.Status.ClassId == 24215
                    && value.Kind == FightBattleStatusViewEventKind.End)
                    && step.StatusViewEvents.Any(value => value.OwnerId == hit.TargetId && value.Status.ClassId == 24487
                        && value.Status.CreatorId == hit.SourceId && value.Kind == FightBattleStatusViewEventKind.Invoke),
                    "The cultivated water strike did not originate from the last accepted freeze ending.");
            }
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "The ice crystal thaw changed or repeated after restoring the battle.");
            File.WriteAllText(Path.Combine(output, "companion-ice-crystal-checkpoint.json"), state);
            UnityEngine.Debug.Log("[Acquired passive combat] 10557 cultivated ice crystal thaw and restore replay passed.");
        }
    }
}
