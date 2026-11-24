using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static int VerifyAcquiredFlowingFire(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output, bool skin)
        {
            int[] entities = skin ? new[] { 13590, 13593, 13594 } : new[] { 12090, 12093, 12094 };
            var casts = step.SkillCasts.Where(value => value.ActorId == 1 && value.SkillItemId == 10100).ToArray();
            Require(casts.Length >= 1 && casts.Length <= 3, "The cultivated flowing fire exceeded its extra-cast limit.");
            var catalog = Zjcs.Presentation.Combat.OfficialCombatAssetCatalog.LoadDefault().Load();
            var active = checkpoint.Units.Single(value => value.Id == 1).Skills.Single(value => value.Slot == 0);
            float start = casts[0].Delay;
            for (int index = 0; index < casts.Length; index++)
            {
                var cast = casts[index];
                var definition = catalog.CreateSkill(entities[index], -1, 10100,
                    active.Rank, active.Level, active.SubRank, active.Properties);
                var hit = step.DamageEvents.Single(value => value.SourceId == 1 && value.SkillEntityId == entities[index]);
                Require(cast.SkillEntityId == entities[index] && cast.Slot == (index == 0 ? 0 : -1)
                    && cast.TargetIds.SequenceEqual(casts[0].TargetIds)
                    && Math.Abs(cast.Delay - start) < .0001f
                    && Math.Abs(cast.Presentation.Duration - definition.Duration) < .0001f
                    && Math.Abs(cast.Presentation.ActionDuration - definition.ActionDuration) < .0001f
                    && hit.TargetId == casts[0].TargetIds.Single() && hit.SkillItemId == 10100
                    && hit.HitEntityId == (skin ? 13591 : 12091) && hit.DamageEntityId == (skin ? 13592 : 12092)
                    && hit.DamageType == 1 && hit.ElementType == 102 && hit.Damage < 0 && !hit.IsCure
                    && !hit.TrueDamage && !hit.Dodge && !hit.Immunity,
                    "The acquired flowing fire lost its sequential target, authored timing, fire damage or parent attribution.");
                start += definition.ActionDuration;
            }
            if (casts.Length > 1)
            {
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                var replay = Zjcs.Presentation.Combat.OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                    .Step(restored.Sequence, restored.Counter);
                Require(replay.Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring cultivated flowing fire repeated or changed its settled extra casts.");
                File.WriteAllText(Path.Combine(output, "acquired-flowing-fire-checkpoint.json"), saved);
                File.WriteAllText(Path.Combine(output, "acquired-flowing-fire-receipt.json"), JsonUtility.ToJson(
                    new FlowingFireReceipt { skin = skin, entities = casts.Select(value => value.SkillEntityId).ToArray(),
                        extras = casts.Length - 1, target = casts[0].TargetIds.Single(), rank = active.Rank,
                        level = active.Level, restored = true }, true));
                Debug.Log("[Acquired active combat] 10100 cultivated sequential extras, original fire impacts and restore replay passed.");
            }
            return casts.Length - 1;
        }

        [Serializable]
        private sealed class FlowingFireReceipt
        {
            public bool skin, restored;
            public int[] entities;
            public int extras, target, rank, level;
        }
    }
}
