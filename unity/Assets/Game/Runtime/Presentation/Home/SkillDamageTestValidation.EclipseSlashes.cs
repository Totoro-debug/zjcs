using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredEclipseSlashes(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] damage, FightBattleCheckpoint checkpoint, string output)
        {
            Require(damage.Length == 6 && damage.Select(hit => hit.TargetId).Distinct().Count() == 1,
                "Eclipse Slashes did not preserve all six hits on its selected enemy.");
            float[] delays = { .3f, .4f, .78f, .9f, 1.04f, 1.48f };
            for (int index = 0; index < damage.Length; index++)
            {
                var hit = damage[index];
                Require(hit.SkillItemId == 10065 && hit.SourceId == 1
                    && hit.HitEntityId == (index == 5 ? 11442 : 11441)
                    && hit.DamageEntityId == (index == 5 ? 11443 : 10020)
                    && hit.ElementType == 0 && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure
                    && hit.Damage < 0 && hit.AfterHp == hit.BeforeHp
                    && checkpoint.DamageTest.EnableMonsterUnlimitHp
                    && Math.Abs(hit.Delay - cast.Delay - delays[index]) < .0001f,
                    "Eclipse Slashes lost its physical hit graph, final strike, native timing or test HP settlement.");
            }
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var runner = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Restoring Eclipse Slashes repeated one of its six physical hits.");
            File.WriteAllText(Path.Combine(output, "acquired-eclipse-slashes-checkpoint.json"), saved);
            Debug.Log("[Acquired active combat] 10065 cultivated six physical hits, final strike, timing and restoration passed.");
        }
    }
}
