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
                    && saved.AvgDamageCountPerTarget == row.AvgDamageCountPerTarget, "Unity checkpoint serialization lost report statistics.");
                totalHits += row.DamageHits;
            }
            Require(totalHits > 0, "Completed battle did not exercise report damage-hit counting.");
            Debug.Log("Battle report statistics passed in Play Mode: " + report.Skills.Count
                + " skill rows, " + totalHits + " damage hits, per-cast targets, confirmed average formulas and Unity checkpoint persistence.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
