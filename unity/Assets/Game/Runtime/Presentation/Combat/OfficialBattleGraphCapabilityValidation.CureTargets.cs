using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateHitCustomCureTargets => firstPromotionItemId != 0
            && Environment.GetEnvironmentVariable("ZJCS_HIT_CUSTOM_CURE_TARGETS") == "1";
        private int[] cureTargetIds = Array.Empty<int>();
        private float[] expectedCureDelays = Array.Empty<float>();
        private readonly List<float> nativeCureDelays = new List<float>();
        private string cureTargetCheckpoint;
        private bool cureTargetsCaptured;

        [Serializable] private sealed class CureTargetEvidence
        {
            public bool passed;
            public int ItemId, DamageHits, NativeCures, TriggerCount;
            public string Combination, Checkpoint;
            public int[] TargetIds;
        }

        private void ConfigureHitCustomCureTargets(FightBattleCheckpoint checkpoint,
            FightBattleUnitState enemy, int dx, int dy)
        {
            if (!ValidateHitCustomCureTargets) return;
            Require(firstPromotionItemId == 10029 || firstPromotionItemId == 10072,
                "Multi-target absorption uses original 10029/10810 or 10072/11690.");
            var other = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(enemy));
            other.Id = checkpoint.Units.Max(value => value.Id) + 1;
            other.Turn.Id = other.Id; other.X += dx; other.Y += dy;
            Require(other.X >= 0 && other.X < checkpoint.Grid.Width
                && other.Y >= 0 && other.Y < checkpoint.Grid.Height,
                "The second absorption target must occupy an original legal grid cell.");
            checkpoint.Units.Add(other);
            cureTargetIds = new[] { enemy.Id, other.Id };
        }

        private void ObserveHitCustomCureTargets(FightBattleCheckpoint checkpoint, FightBattleStepEvent step)
        {
            if (!ValidateHitCustomCureTargets) return;
            int stages = firstPromotionItemId == 10072 ? 3 : 1;
            Require(cureTargetIds.Length == 2, "Two original absorption targets are required.");
            foreach (int target in cureTargetIds)
                Require(step.DamageEvents.Count(value => value.SourceId == 1 && value.TargetId == target
                    && value.SkillEntityId == skillId && !value.IsCure && !value.Dodge
                    && !value.Immunity && !value.Blinding) == stages,
                    "Original per-target absorption damage occurrences differ: " + target);
            var children = step.SkillCasts.Where(value => value.ActorId == 1
                && value.SkillEntityId == 10520).ToArray();
            Require(children.Length == stages * 2 && children.All(value => value.Slot == -1
                && value.TargetIds.SequenceEqual(new[] { 1 })),
                "Each successful original hit must dispatch one independent self-cure child.");
            expectedCureDelays = children.Select(value => value.Delay).ToArray();
            var status = checkpoint.Units.Single(value => value.Id == 1).Statuses.Single(value => value.ClassId == 25023);
            Require(status.ApplyingSkillItemId == 10544 && status.SourceSkillRank == 2
                && status.SourceSkillLevel == 20 && status.Components.HitCustomCure.Filter.InvokeCount == stages * 2,
                "Original absorption cultivation or independent occurrence count differs.");
            cureTargetCheckpoint = FightBattleCheckpointSerializer.Save(checkpoint);
        }

        private void ObserveNativeHitCustomCure(FightBattleSkillCastEvent cast)
        {
            if (!ValidateHitCustomCureTargets || finisherScheduled || cast.SkillEntityId != 10520) return;
            Require(cast.ActorId == 1 && cast.Slot == -1 && cast.TargetIds.SequenceEqual(new[] { 1 }),
                "The native original absorption graph must bind the source's self-cure child.");
            // CastStarted follows the original prefab's creation, binding and MyStart,
            // including 10520's authored pure logic graph with no substitute animation.
            nativeCureDelays.Add(cast.Delay);
        }

        private bool ObserveHitCustomCureTargetsFrame()
        {
            if (!ValidateHitCustomCureTargets || !captured || cureTargetsCaptured) return false;
            Require(Time.realtimeSinceStartup - castTime < 90f,
                "Original native self-cure children did not finish dispatching.");
            if (nativeCureDelays.Count < expectedCureDelays.Length) return true;
            Require(expectedCureDelays.Length > 0 && nativeCureDelays.SequenceEqual(expectedCureDelays),
                "Native self-cure graph count or per-occurrence delay differs from original receipts.");
            var saved = FightBattleCheckpointSerializer.Load(cureTargetCheckpoint);
            int count = saved.Units.Single(value => value.Id == 1).Statuses.Single(value => value.ClassId == 25023)
                .Components.HitCustomCure.Filter.InvokeCount;
            Require(count == nativeCureDelays.Count, "Saved absorption count differs from native children.");
            string mode = Environment.GetEnvironmentVariable("ZJCS_STATUS_HIT_CUSTOM_CURE_ACCEPTANCE");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "hit-custom-cure-targets.json"), JsonUtility.ToJson(new CureTargetEvidence {
                    passed = true, ItemId = firstPromotionItemId, Combination = mode,
                    TargetIds = cureTargetIds, DamageHits = count, NativeCures = nativeCureDelays.Count,
                    TriggerCount = count, Checkpoint = cureTargetCheckpoint }, true));
            cureTargetsCaptured = true;
            CaptureName = "hit-custom-cure-" + firstPromotionItemId + "-" + mode;
            Debug.Log("[Hit custom cure targets] Two original targets, independent self-cure native graphs and checkpoint passed: "
                + firstPromotionItemId + "/" + mode + "/hits=" + count);
            return true;
        }

        private void VerifyHitCustomCureTargets()
        {
            if (ValidateHitCustomCureTargets)
                Require(cureTargetsCaptured && nativeCureDelays.SequenceEqual(expectedCureDelays),
                    "Original multi-target absorption never completed its native graph observation.");
        }
    }
}
