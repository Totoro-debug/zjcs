using System;
using System.IO;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredSageDarkBlast(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, FightBattleCheckpoint checkpoint, string output, bool firstCast)
        {
            float[] delays = { .98f, 1.14f, 1.34f };
            Require(hits.Length > 0 && hits.Length <= delays.Length
                && (hits.Length == delays.Length || !firstCast && hits[hits.Length - 1].AfterHp == 0),
                "The cultivated dark blast lost one of its original hits.");
            int target = hits[0].TargetId;
            long hp = hits[0].BeforeHp;
            for (int index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];
                Require(hit.TargetId == target && target != cast.ActorId && hit.HitEntityId == 12121
                    && hit.DamageEntityId == 10004 && hit.ElementType == 104 && hit.DamageType == 1
                    && !hit.TrueDamage && !hit.IsCure && !hit.IsDamageTriggerStatus
                    && Math.Abs(hit.Delay - cast.Delay - delays[index]) < .0001f
                    && hit.BeforeHp == hp && hit.Damage < 0 && hit.AfterHp == hp + hit.Damage,
                    "The cultivated dark blast changed target, timing, damage type or HP settlement.");
                hp = hit.AfterHp;
            }
            if (!firstCast) return;
            string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(serialized);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == serialized,
                "Restoring the cultivated dark blast changed its settled damage.");
            File.WriteAllText(Path.Combine(output, "acquired-sage-dark-blast-checkpoint.json"), serialized);
        }
    }
}
