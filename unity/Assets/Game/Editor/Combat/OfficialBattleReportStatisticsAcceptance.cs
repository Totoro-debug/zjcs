using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportStatisticsAcceptance
    {
        public static void InstallShieldFixture(Zjcs.Presentation.Combat.OfficialBattleRuntimeController controller)
        {
            var player = controller.Checkpoint.Units.First(value => value.Id == 1);
            var skill = player.Skills.First(value => value.Hits.Count > 0 || value.HitGraphEntities.Any(hit => hit.HasFixedDamage));
            var applications = skill.Hits.Count > 0 ? skill.Hits[0].AppliedStatuses
                : skill.HitGraphEntities.First(hit => hit.HasFixedDamage).AppliedStatuses;
            applications.Add(new FightBattleStatusApplication
            {
                StatusId = 24174, TargetType = 1, Chance = 1f, AffectedByProp = false,
                CustomShieldValue = 250
            });
        }

        public static void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(Application.isPlaying && checkpoint.Finished, "Report statistics require a completed Play Mode battle.");
            var report = FightBattleReportBuilder.Build(checkpoint);
            var restored = JsonUtility.FromJson<FightBattleCheckpoint>(JsonUtility.ToJson(checkpoint));
            var savedReport = FightBattleReportBuilder.Build(restored);
            int totalHits = 0;
            foreach (FightBattleReportSkill row in report.Skills)
            {
                var events = checkpoint.Receipts.SelectMany(receipt => receipt.Event.DamageEvents)
                    .Where(hit => hit.SourceId == row.UnitId && hit.SkillEntityId == row.SkillEntityId
                        && !hit.IsSkillCost && !hit.IsCure && !hit.Dodge && !hit.Blinding && hit.DamageType != 0).ToArray();
                Require(row.HasDamageHitCounts && row.DamageHits == events.Length,
                    "Report damage-hit numerator differs from resolved battle events: " + row.SkillEntityId);
                int targets = 0;
                bool unambiguous = true;
                foreach (FightBattleStepReceipt receipt in checkpoint.Receipts)
                {
                    int casts = receipt.Event.SkillCasts.Count(cast => cast.ActorId == row.UnitId
                        && cast.SkillEntityId == row.SkillEntityId);
                    if (casts > 1) unambiguous = false;
                    targets += events.Where(hit => receipt.Event.DamageEvents.Contains(hit))
                        .Select(hit => hit.TargetId).Distinct().Count();
                }
                if (unambiguous) Require(row.DamageTargets == targets, "Report per-cast target denominator differs from battle receipts.");
                Require(row.AvgDamageCount == (row.Casts > 0 ? (float)events.Length / row.Casts : 0f)
                    && row.AvgDamageCountPerTarget == (row.DamageTargets > 0 ? (float)events.Length / row.DamageTargets : 0f),
                    "Report average formulas changed after battle execution.");
                FightBattleReportSkill saved = savedReport.Skills.First(value => value.UnitId == row.UnitId && value.SkillEntityId == row.SkillEntityId);
                Require(saved.HasDamageHitCounts && saved.DamageHits == row.DamageHits
                    && saved.DamageTargets == row.DamageTargets && saved.AvgDamageCount == row.AvgDamageCount
                    && saved.AvgDamageCountPerTarget == row.AvgDamageCountPerTarget
                    && saved.HasShieldGrantCounts == row.HasShieldGrantCounts && saved.ShieldGranted == row.ShieldGranted
                    && saved.DamagePer == row.DamagePer && saved.HealPer == row.HealPer && saved.ShieldPer == row.ShieldPer
                    && saved.HealMaxHpPer == row.HealMaxHpPer && saved.ShieldMaxHpPer == row.ShieldMaxHpPer,
                    "Unity checkpoint serialization lost report statistics.");
                var ownSkills = report.Skills.Where(value => value.UnitId == row.UnitId).ToArray();
                long ownDamage = ownSkills.Sum(value => value.Damage), ownHeal = ownSkills.Sum(value => value.Heal),
                    ownShield = ownSkills.Sum(value => value.ShieldGranted);
                Require(row.DamagePer == (ownDamage > 0 ? (float)((double)row.Damage / ownDamage) : 0f)
                    && row.HealPer == (ownHeal > 0 ? (float)((double)row.Heal / ownHeal) : 0f)
                    && row.ShieldPer == (ownShield > 0 ? (float)((double)row.ShieldGranted / ownShield) : 0f),
                    "Report distributions use a different caster or denominator.");
                var source = report.Unit(row.UnitId);
                Require(row.HasMaxHpPercentages && row.HealMaxHpPer == (float)((double)row.Heal / source.MaxHp)
                    && row.ShieldMaxHpPer == (float)((double)row.ShieldGranted / source.MaxHp),
                    "Report HP ratios use a different caster or denominator.");
                totalHits += row.DamageHits;
            }
            Require(totalHits > 0, "Completed battle did not exercise report damage-hit counting.");
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_SHIELDS") == "1")
            {
                long shield = report.Skills.Where(value => value.UnitId == 1).Sum(value => value.ShieldGranted);
                Require(shield >= 250 && report.Unit(1).ShieldGranted == shield && report.TotalShieldGranted >= shield,
                    "Live battle did not attribute the successful original-component shield fixture.");
                Debug.Log("Battle report shield grants passed in Play Mode: " + shield
                    + " successfully added shield, per-caster distributions, caster HP ratios and Unity checkpoint persistence.");
            }
            Debug.Log("Battle report statistics passed in Play Mode: " + report.Skills.Count
                + " skill rows, " + totalHits + " damage hits, per-cast targets, confirmed average formulas and Unity checkpoint persistence.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
