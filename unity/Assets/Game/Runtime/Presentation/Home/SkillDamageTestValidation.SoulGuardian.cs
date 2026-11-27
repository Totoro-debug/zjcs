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
        [Serializable] private sealed class SoulGuardianReceipt
        {
            public int casts, costEvents, reducedHits;
            public long paid;
            public bool restored, castCaptured;
        }
        private static SoulGuardianReceipt soulGuardian;
        private static long soulGuardianInitialHp;
        private static string soulGuardianPrevious;

        private static void PrepareAcquiredSoulGuardian(FightBattleUnitState actor)
        {
            soulGuardian = new SoulGuardianReceipt();
            soulGuardianInitialHp = actor.Hp;
            soulGuardianPrevious = null;
        }

        private static void PrepareAcquiredSoulGuardianCheckpoint(FightBattleCheckpoint checkpoint) =>
            soulGuardianPrevious = FightBattleCheckpointSerializer.Save(checkpoint);

        private static void VerifyAcquiredSoulGuardianStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25093);
            soulGuardian.casts += step.SkillCasts.Count(cast => cast.ActorId == 1
                && cast.SkillEntityId == 11870 && cast.SkillItemId == 10590);
            foreach (var cost in step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.SkillEntityId == 11870 && hit.IsSkillCost))
            {
                Require(cost.TargetId == 1 && cost.BeforeHp == soulGuardianInitialHp
                    && cost.AfterHp == cost.BeforeHp - Math.Min((long)(cost.BeforeHp * .5), cost.BeforeHp - 1)
                    && cost.AfterHp >= 1, "Soul Guardian changed its original current-HP payment or one-HP floor.");
                soulGuardian.costEvents++; soulGuardian.paid -= cost.Damage;
            }
            var guard = actor.Statuses.SingleOrDefault(status => status.ClassId == 24372);
            Require(soulGuardian.casts == 1 && soulGuardian.costEvents == 1 && listener.RoundStartTriggeredCount == 1
                && guard != null && !guard.CanDispersed && guard.DurationRound == -1
                && guard.CreatorId == 1 && guard.ApplyingSkillItemId == 10590
                && guard.SourceSkillRank == listener.SourceSkillRank && guard.SourceSkillLevel == 2
                && guard.CustomShieldValue == soulGuardian.paid && guard.ShieldKind == FightBattleShieldKind.None,
                "Soul Guardian lost its one-time opening trigger, cultivated permanent reduction or cost value.");
            foreach (var property in guard.ResolvedProperties)
                Require(actor.Properties.Single(value => value.Id == property.Id).Value
                    == actor.BaseProperties.Where(value => value.Id == property.Id).Select(value => value.Value).DefaultIfEmpty().Single()
                        + property.Value, "Soul Guardian reduction was omitted or applied twice to final properties.");

            var incoming = step.DamageEvents.Where(hit => hit.TargetId == 1 && hit.SourceId != 1
                && hit.Damage < 0 && !hit.IsCure && !hit.IsSkillCost && !hit.TrueDamage).ToArray();
            if (incoming.Length > 0)
            {
                var comparison = FightBattleCheckpointSerializer.Load(soulGuardianPrevious);
                var payer = comparison.Units.Single(unit => unit.Id == 1);
                if (payer.Statuses.RemoveAll(status => status.ClassId == 24372) == 1)
                {
                    var withoutGuard = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(comparison)
                        .Step(comparison.Sequence, comparison.Counter + 1).Event.DamageEvents
                        .Where(hit => hit.TargetId == 1 && hit.SourceId != 1 && hit.Damage < 0
                            && !hit.IsCure && !hit.IsSkillCost && !hit.TrueDamage).ToArray();
                    Require(withoutGuard.Length == incoming.Length,
                        "Soul Guardian comparison did not replay the same incoming attack sequence.");
                    for (int i = 0; i < incoming.Length; i++)
                    {
                        Require(withoutGuard[i].SkillEntityId == incoming[i].SkillEntityId
                            && withoutGuard[i].HitEntityId == incoming[i].HitEntityId
                            && withoutGuard[i].SourceId == incoming[i].SourceId
                            && withoutGuard[i].Damage < incoming[i].Damage,
                            "The cultivated Soul Guardian did not reduce the same actual incoming hit.");
                        soulGuardian.reducedHits++;
                    }
                }
            }
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Soul Guardian charged HP or duplicated protection after restoring a completed receipt.");
            soulGuardian.restored = true;
            soulGuardianPrevious = saved;
            File.WriteAllText(Path.Combine(output, "companion-soul-guardian-checkpoint.json"), saved);
        }

        private static void CompleteAcquiredSoulGuardian(string output)
        {
            Require(soulGuardian.casts == 1 && soulGuardian.costEvents == 1 && soulGuardian.paid > 0
                && soulGuardian.reducedHits > 0 && soulGuardian.restored
                && soulGuardian.castCaptured,
                "Soul Guardian must pay HP once, reduce actual incoming damage, render its original cast and restore.");
            File.WriteAllText(Path.Combine(output, "companion-soul-guardian-receipt.json"), JsonUtility.ToJson(soulGuardian, true));
            Debug.Log("[Acquired passive combat] 10590 cultivated opening HP cost, permanent damage reduction, native effects and restoration passed.");
        }
    }
}
