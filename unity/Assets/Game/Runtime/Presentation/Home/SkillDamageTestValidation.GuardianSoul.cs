using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed partial class AcquiredGuardianProbe
        {
            private long soulInitialHp;
            private bool soulOpened, soulReturned;

            private void ConfigureSoulGuard(FightBattleUnitState actor)
            {
                Require(actor.Statuses.Any(status => status.ClassId == 25042
                    && status.ApplyingSkillItemId == 10575), "The actual soul guard secret was not assembled.");
                soulInitialHp = actor.Hp;
            }

            private void ObserveSoulGuard(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var cost = step.DamageEvents.SingleOrDefault(hit => hit.SkillEntityId == 10160
                    && hit.SourceId == actor.Id && hit.TargetId == actor.Id);
                if (cost != null)
                {
                    var shield = actor.Statuses.Single(status => status.ClassId == 24239
                        && status.ApplyingSkillItemId == item);
                    long expected = soulInitialHp / 2;
                    Require(!soulOpened && !cost.IsCure && cost.BeforeHp == soulInitialHp
                        && cost.BeforeHp - cost.AfterHp == expected && cost.ShieldDamage == 0
                        && shield.ShieldValue == expected && shield.Components.ShieldConversion.Started
                        && shield.Components.ShieldConversion.ConvertedHp == expected
                        && shield.SourceSkillLevel == 2 && actor.Statuses.Any(status => status.ClassId == 24489
                            && status.ApplyingSkillItemId == item && status.SourceSkillLevel == 2),
                        "The cultivated soul guard changed its HP cost, conversion order or original two shields.");
                    SaveSoulGuardCheckpoint(checkpoint, "opening");
                    soulOpened = true;
                    Debug.Log("[Acquired passive combat] 10575 cultivated opening HP cost, converted shield and restore replay passed.");
                }
                var cure = step.DamageEvents.SingleOrDefault(hit => hit.SkillEntityId == 10163
                    && hit.SourceId == actor.Id && hit.TargetId == actor.Id);
                if (cure == null) return;
                var converted = actor.Statuses.Single(status => status.ClassId == 24239
                    && status.ApplyingSkillItemId == item);
                long principal = (long)((float)converted.Components.ShieldConversion.ConvertedHp
                    * ((float)converted.ShieldRemainValue / (float)converted.ShieldValue));
                var properties = actor.Properties.ToDictionary(value => value.Id, value => value.Value);
                long expectedCure = assets.LoadDamageFormula().CalculateCure(properties, properties, 0, 0, principal).Cure;
                Require(soulOpened && !soulReturned && checkpoint.Finished && checkpoint.Outcome.Won
                    && actor.Hp > 0 && converted.Components.ShieldConversion.Returned
                    && cure.IsCure && cure.SkillItemId == item && cure.Damage == expectedCure
                    && cure.AfterHp - cure.BeforeHp == Math.Min(expectedCure, actor.MaxHp - cure.BeforeHp),
                    "The actual victory did not return the remaining converted shield through the original cure.");
                SaveSoulGuardCheckpoint(checkpoint, "return");
                File.WriteAllText(Path.Combine(output, "companion-soul-guard-receipt.json"),
                    JsonUtility.ToJson(new SoulGuardReceipt { cost = soulInitialHp / 2,
                        principal = principal, expected = expectedCure, actual = cure.Damage,
                        healed = cure.AfterHp - cure.BeforeHp, restored = true }, true));
                soulReturned = true;
                Debug.Log("[Acquired passive combat] 10575 surviving victory return, original cure and restore replay passed.");
            }

            private void SaveSoulGuardCheckpoint(FightBattleCheckpoint checkpoint, string phase)
            {
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                var replay = assets.RestoreBattle(restored).Step(restored.Sequence, restored.Counter);
                Require(replay.Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring soul guard changed its HP, shield or settled receipt.");
                File.WriteAllText(Path.Combine(output, "companion-soul-guard-" + phase + ".json"), saved);
            }

            public void Captured(FightBattleCheckpoint checkpoint)
            {
                if (item != 10575) return;
                Require(soulOpened, "Soul guard must settle and render its opening before the controlled victory.");
                // Only this isolated diagnostic changes the dummy's HP after the opening was rendered.
                checkpoint.DamageTest.EnableMonsterUnlimitHp = false;
                foreach (var enemy in checkpoint.Units.Where(unit => unit.Side != 1 && unit.Hp > 0))
                    enemy.Hp = enemy.Turn.Hp = 1;
            }
        }

        [Serializable]
        private sealed class SoulGuardReceipt
        {
            public long cost, principal, expected, actual, healed;
            public bool restored;
        }
    }
}
