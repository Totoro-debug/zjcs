using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredFireLeap(FightBattleStepEvent step, FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, FightBattleCheckpoint checkpoint, string output, bool first)
        {
            var move = step.Movements.Single(value => value.UnitId == cast.ActorId && value.MoveBehaviourId == 12103);
            var hit = hits.Single();
            var source = new FightGridPoint(cast.Presentation.SourceX, cast.Presentation.SourceY);
            var target = new FightGridPoint(hit.DamageX, hit.DamageY);
            var landing = move.Path.Last();
            int distance = Math.Abs(target.X - source.X) + Math.Abs(target.Y - source.Y);
            Require(cast.SkillEntityId == 12100 && cast.TargetIds.SequenceEqual(new[] { hit.TargetId })
                && distance >= 1 && distance <= 2 && move.Path.Count == 2 && move.Path[0].Equals(source)
                && Math.Abs(target.X - landing.X) + Math.Abs(target.Y - landing.Y) == 1
                && Math.Abs(move.Delay - cast.Delay - .45f) < .0001f
                && Math.Abs(move.MoveDuration - .05f) < .0001f
                && Math.Abs(hit.Delay - cast.Delay - .5f) < .0001f
                && hit.HasDamagePosition && hit.HitEntityId == 12102 && hit.DamageEntityId == 12104
                && hit.DamageType == 1 && hit.ElementType == 102 && hit.Damage < 0
                && !hit.IsCure && !hit.TrueDamage && !hit.Dodge && !hit.Blinding && !hit.Immunity,
                "The cultivated fire leap lost its confirmed target range, direct landing or original fire impact.");
            if (!first) return;
            var active = checkpoint.Units.Single(unit => unit.Id == cast.ActorId).Skills.Single(skill => skill.Slot == 0);
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var replay = Zjcs.Presentation.Combat.OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                .Step(restored.Sequence, restored.Counter);
            Require(replay.Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Restoring the cultivated fire leap changed its landing or repeated its settled impact.");
            File.WriteAllText(Path.Combine(output, "acquired-fire-leap-checkpoint.json"), saved);
            File.WriteAllText(Path.Combine(output, "acquired-fire-leap-receipt.json"), JsonUtility.ToJson(
                new FireLeapReceipt { target = hit.TargetId, distance = distance, pathCount = move.Path.Count,
                    sourceX = source.X, sourceY = source.Y, landingX = landing.X, landingY = landing.Y,
                    targetX = target.X, targetY = target.Y, rank = active.Rank, level = active.Level,
                    damage = hit.Damage, restored = true }, true));
            Debug.Log("[Acquired active combat] 10101 cultivated direct jump, adjacent landing, original fire impact and restore replay passed.");
        }

        [Serializable]
        private sealed class FireLeapReceipt
        {
            public int target, distance, pathCount, sourceX, sourceY, landingX, landingY, targetX, targetY, rank, level;
            public long damage;
            public bool restored;
        }
    }
}
