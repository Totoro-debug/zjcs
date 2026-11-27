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
        [Serializable] private sealed class JudgmentBladeReceipt
        {
            public int hits, strikes, remaining;
            public bool restored, markCaptured, strikeCaptured;
        }
        private static JudgmentBladeReceipt judgmentBlade;
        private static void PrepareAcquiredJudgmentBlade() => judgmentBlade = new JudgmentBladeReceipt();

        private static void VerifyAcquiredJudgmentBladeStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25080);
            Require(!listener.CanDispersed && listener.DurationRound == -1
                && listener.ApplyingSkillItemId == 10574 && listener.SourceSkillLevel == 2
                && listener.CreatorId == actor.Id && listener.SourceSubRank == actor.SubRank,
                "Judgment Blade lost its original cultivated hit listener.");
            var eligible = step.DamageEvents.Where(hit => hit.SourceId == actor.Id && hit.SkillEntityId == 11440
                && hit.Damage < 0 && !hit.Dodge && !hit.Blinding && !hit.Immunity && !hit.IsCure).ToArray();
            var strikes = step.DamageEvents.Where(hit => hit.SkillEntityId == 11810).ToArray();
            Require(strikes.All(hit => hit.SourceId == actor.Id && hit.SkillItemId == 10574 && hit.TargetId == 2
                && hit.HitEntityId == 11811 && hit.DamageEntityId == 10004 && hit.ElementType == 0
                && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure),
                "Judgment Blade's threshold did not use its original cultivated physical child strike.");
            judgmentBlade.hits += eligible.Length; judgmentBlade.strikes += strikes.Length;
            var target = checkpoint.Units.Single(unit => unit.Id == 2);
            var marks = target.Statuses.Where(status => status.ClassId == 24316).ToArray();
            judgmentBlade.remaining = judgmentBlade.hits % 10;
            Require(judgmentBlade.strikes == judgmentBlade.hits / 10
                && listener.Components.HitSkill.InvokeCount == judgmentBlade.hits
                && marks.Length == (judgmentBlade.remaining == 0 ? 0 : 1)
                && marks.All(mark => mark.StackCount == judgmentBlade.remaining && mark.CanDispersed
                    && mark.DurationRound == -1 && mark.CreatorId == actor.Id && mark.ApplyingSkillItemId == 10574
                    && mark.SourceSkillRank == listener.SourceSkillRank && mark.SourceSkillLevel == 2),
                "Judgment Blade lost its ten-layer threshold, consumption, remainder or cultivated mark source.");
            if (eligible.Length == 0 && strikes.Length == 0) return;
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Judgment Blade repeated marks or threshold damage after restoration.");
            judgmentBlade.restored = true;
            File.WriteAllText(Path.Combine(output, "companion-judgment-blade-checkpoint.json"), saved);
        }

        private static void CompleteAcquiredJudgmentBlade(string output)
        {
            Require(judgmentBlade.hits >= 10 && judgmentBlade.strikes > 0 && judgmentBlade.restored
                && judgmentBlade.markCaptured && judgmentBlade.strikeCaptured,
                "Judgment Blade must render its original mark and threshold attack during actual cultivated combat.");
            File.WriteAllText(Path.Combine(output, "companion-judgment-blade-receipt.json"),
                JsonUtility.ToJson(judgmentBlade, true));
            Debug.Log("[Acquired passive combat] 10574 cultivated ten-layer mark, physical threshold strike, native effects and restoration passed.");
        }
    }
}
