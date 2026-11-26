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
        private static LightScorchReceipt lightScorch;
        private static bool lightScorchStatusCaptured;

        private static void PrepareAcquiredLightScorch()
        {
            lightScorch = new LightScorchReceipt();
            lightScorchStatusCaptured = false;
        }

        private static void VerifyAcquiredLightScorchStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25046
                && status.ApplyingSkillItemId == 10577);
            lightScorch.parentHits += step.DamageEvents.Count(hit => hit.SourceId == 1
                && hit.SkillItemId == 10098 && hit.Damage < 0 && !hit.IsCure
                && !hit.Dodge && !hit.Blinding && !hit.Immunity);
            foreach (var cast in step.SkillCasts.Where(value => value.ActorId == 1 && value.SkillEntityId == 11680))
            {
                Require(cast.SkillItemId == 10577 && cast.Slot < 0,
                    "The cultivated light scorch child lost its passive source.");
                var hits = step.DamageEvents.Where(hit => cast.DamageEventIds.Contains(hit.DamageId)).ToArray();
                Require(hits.Length == 1 && hits[0].SourceId == 1 && hits[0].TargetId != 1
                    && hits[0].SkillEntityId == 11680 && hits[0].HitEntityId == 11681
                    && hits[0].DamageEntityId == 11682 && hits[0].ElementType == 103
                    && hits[0].DamageType == 1 && !hits[0].IsCure && !hits[0].TrueDamage
                    && Math.Abs(hits[0].Delay - cast.Delay - .1f) < .0001f,
                    "The light scorch child lost its original light damage or authored hit delay.");
                Require(step.DamageEvents.Any(parent => parent.SourceId == 1 && parent.SkillItemId == 10098
                    && parent.TargetId == hits[0].TargetId && parent.Damage < 0
                    && Math.Abs(parent.Delay - cast.Delay) < .0001f),
                    "The light scorch child did not begin at its actual triggering hit.");
                lightScorch.procs++;
                lightScorch.lightDamage += -hits[0].Damage;
            }
            foreach (var view in step.StatusViewEvents.Where(value => value.Status.ClassId == 24234
                && value.Status.CreatorId == 1))
            {
                Require(view.OwnerId != 1, "Light scorch applied blinding to its caster.");
                if (view.Kind == FightBattleStatusViewEventKind.Start) lightScorch.starts++;
                if (view.Kind == FightBattleStatusViewEventKind.End)
                {
                    Require(step.SkillCasts.Any(cast => cast.ActorId == view.OwnerId),
                        "Blinding ended without the affected unit's next eligible skill.");
                    lightScorch.ends++;
                }
            }
            lightScorch.misses += step.DamageEvents.Count(hit => hit.SourceId != 1 && hit.Blinding);
            foreach (var status in checkpoint.Units.SelectMany(unit => unit.Statuses)
                .Where(status => status.ClassId == 24234 && status.CreatorId == 1))
                Require(status.ApplyingSkillItemId == 10577 && status.SourceSkillRank == listener.SourceSkillRank
                    && status.SourceSkillLevel == 2 && status.SourceSubRank == actor.SubRank,
                    "The light scorch blinding status lost its inherited cultivation.");
            Require(lightScorch.procs <= lightScorch.parentHits,
                "The light scorch child recursively triggered another child.");
            if (lightScorch.procs == 0) return;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring the light scorch receipt repeated damage or consumed blinding twice.");
            File.WriteAllText(Path.Combine(output, "companion-light-scorch-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "companion-light-scorch-receipt.json"),
                JsonUtility.ToJson(lightScorch, true));
        }

        private static void CompleteAcquiredLightScorch(bool captured)
        {
            Require(captured && lightScorchStatusCaptured && lightScorch.procs > 0
                && lightScorch.lightDamage > 0 && lightScorch.starts > 0 && lightScorch.ends > 0,
                "The cultivated light scorch did not render its light hit and blinding, and consume blinding.");
            Debug.Log("[Acquired passive combat] 10577 light damage, blinding consumption, hit timing and restore passed.");
        }

        [Serializable]
        private sealed class LightScorchReceipt
        {
            public int parentHits, procs, starts, ends, misses;
            public long lightDamage;
        }
    }
}
