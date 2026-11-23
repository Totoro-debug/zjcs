using System;
using System.IO;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredHeavenlyRain(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] heals, FightBattleCheckpoint checkpoint, string output, bool firstCure)
        {
            Require(heals.Length == 1, "The acquired heavenly rain did not make its original single cure.");
            var cure = heals[0];
            Require(cure.TargetId == cast.ActorId && cure.HitEntityId == 12021 && cure.DamageEntityId == 12022
                && cure.IsCure && !cure.TrueDamage && Math.Abs(cure.Delay - cast.Delay - 1f) < .0001f
                && cure.Damage > 0 && cure.AfterHp - cure.BeforeHp == cure.Damage && cure.AfterHp <= cure.MaxHp,
                "The acquired heavenly rain lost its original recipient, healing or impact timing.");
            if (!firstCure) return;
            Require(cure.BeforeHp < cure.MaxHp / 2L, "The first heavenly rain cure did not exercise its low-HP branch.");
            string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(serialized);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == serialized,
                "Restoring the acquired heavenly rain changed its settled healing.");
            File.WriteAllText(Path.Combine(output, "acquired-heavenly-rain-checkpoint.json"), serialized);
        }
    }
}
