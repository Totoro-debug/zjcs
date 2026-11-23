using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredOverloadProtection(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var starts = step.StatusViewEvents.Where(view => view.OwnerId == 1
                && view.Status.ClassId == 24240 && view.Kind == FightBattleStatusViewEventKind.Start).ToArray();
            if (starts.Length == 0) return;
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25043);
            bool lostHp = step.DamageEvents.Any(hit => hit.TargetId == actor.Id && hit.BeforeHp > hit.AfterHp);
            Require(starts.All(view => view.Status.CreatorId == actor.Id && view.Status.ShieldMaximum > 0)
                && listener.ApplyingSkillItemId == 10573 && listener.SourceSkillLevel == 2 && listener.HpAccTriggeredCount > 0
                && lostHp, "The cultivated overload shield lost its applied value or provenance. "
                    + $"Visual shield={starts[0].Status.ShieldMaximum}, trigger count={listener.HpAccTriggeredCount}, HP loss={lostHp}.");
            var report = FightBattleReportBuilder.Build(checkpoint).Skills.Single(skill => skill.SkillItemId == 10573);
            Require(report.Casts == listener.HpAccTriggeredCount && report.ShieldGranted > 0,
                "The cultivated overload shield or trigger count was missing from the battle report.");
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "The overload shield changed or repeated after restoring the battle.");
            File.WriteAllText(Path.Combine(output, "companion-overload-protection-checkpoint.json"), state);
            UnityEngine.Debug.Log("[Acquired passive combat] 10573 cultivated overload protection shield and restore replay passed.");
        }
    }
}
