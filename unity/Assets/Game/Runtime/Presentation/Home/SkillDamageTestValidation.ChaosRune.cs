using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredChaosRune(FightBattleStepEvent step, FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, FightBattleCheckpoint checkpoint, string output, ref int applications)
        {
            Require(hits.Length == 1, "The acquired chaos rune did not make its original single hit.");
            var hit = hits[0];
            Require(hit.TargetId != cast.ActorId && hit.HitEntityId == 11381 && hit.DamageEntityId == 11382
                && hit.ElementType == 104 && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                && Math.Abs(hit.Delay - cast.Delay - 1f) < .0001f,
                "The acquired chaos rune lost its original dark damage or impact timing.");
            var starts = step.StatusViewEvents.Where(view => view.OwnerId == hit.TargetId
                && view.Kind == FightBattleStatusViewEventKind.Start && view.Status.ClassId == 24217).ToArray();
            if (starts.Length == 0) return;
            Require(starts.Length == 1 && hit.IsDamageTriggerStatus, "The acquired confusion lost its damage application.");
            var status = checkpoint.Units.Single(unit => unit.Id == hit.TargetId).Statuses.Single(value =>
                value.ClassId == 24217 && value.InstanceId == starts[0].Status.InstanceId);
            Require(status.CreatorId == cast.ActorId && status.ApplyingSkillItemId == 10059
                && status.SourceSkillLevel == 2 && status.DurationRound == 1 && status.ActionType == 54,
                "The acquired confusion lost its source, cultivation, action type or duration.");
            applications++;
            if (applications != 1) return;
            string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(serialized);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == serialized,
                "Restoring the acquired chaos rune changed its settled status.");
            File.WriteAllText(Path.Combine(output, "acquired-chaos-rune-checkpoint.json"), serialized);
        }
    }
}
