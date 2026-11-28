using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static RepellingWindReceipt repellingWind;
        private static void PrepareAcquiredRepellingWind() => repellingWind = new RepellingWindReceipt();

        private static void VerifyAcquiredRepellingWindStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var owner = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = owner.Statuses.Single(status => status.ClassId == 25032 && status.ApplyingSkillItemId == 10566);
            Require(listener.SourceSkillLevel == 2 && listener.SourceSubRank == owner.SubRank
                && listener.DurationRound == -1 && listener.MoveNearEnterRangeCauseFilter == 1
                && listener.MoveNearRate == .75f, "The cultivated repelling wind listener lost its original entry rule.");
            foreach (var cast in step.SkillCasts.Where(value => value.SkillEntityId == 11370))
            {
                var hits = step.DamageEvents.Where(hit => cast.DamageEventIds.Contains(hit.DamageId)).ToArray();
                Require(cast.ActorId == 1 && cast.SkillItemId == 10566 && cast.Slot < 0 && hits.Length == 1
                    && hits[0].SourceId == 1 && hits[0].TargetId != 1 && hits[0].ElementType == 100
                    && hits[0].HitEntityId == 11371 && hits[0].DamageEntityId == 11372
                    && hits[0].DamageType == 1 && !hits[0].TrueDamage && !hits[0].IsCure,
                    "Repelling wind lost its original cultivated wind child or target.");
                int target = hits[0].TargetId;
                Require(step.Movements.Any(move => move.UnitId == target && move.MoveCause == EEntityMoveCause.SelfMove
                    && move.Path.Count > 1
                    && Math.Abs(move.Path[0].X - cast.Presentation.SourceX) + Math.Abs(move.Path[0].Y - cast.Presentation.SourceY) > 2
                    && Math.Abs(move.Path.Last().X - cast.Presentation.SourceX) + Math.Abs(move.Path.Last().Y - cast.Presentation.SourceY) <= 2),
                    "Repelling wind triggered without an enemy actively entering its two-cell range.");
                repellingWind.procs++;
                repellingWind.windDamage += Math.Max(0, -hits[0].Damage);
                foreach (var move in step.Movements.Where(value => value.UnitId == target && value.MoveBehaviourId == 11373))
                {
                    Require(move.MoveCause == EEntityMoveCause.Displaced && move.MoveSpeed == 2.5f
                        && move.Path.Count >= 2 && move.Path.Count <= 3,
                        "Repelling wind lost its original outward displacement action.");
                    repellingWind.pushes++;
                }
            }
            if (repellingWind.procs == 0) return;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var executor = Zjcs.Presentation.Combat.OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring repelling wind repeated an entry roll, wind hit or displacement.");
            repellingWind.restored = true;
            File.WriteAllText(Path.Combine(output, "companion-repelling-wind-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "companion-repelling-wind-receipt.json"), JsonUtility.ToJson(repellingWind, true));
        }

        private static void CompleteAcquiredRepellingWind(bool captured)
        {
            Require(captured && repellingWind.procs > 0 && repellingWind.windDamage > 0
                && repellingWind.pushes > 0 && repellingWind.restored,
                "The cultivated repelling wind did not render its actual hit, displace an entrant and restore.");
            Debug.Log("[Acquired passive combat] 10566 active entry, wind damage, knockback, cultivation and restore passed.");
        }

        [Serializable]
        private sealed class RepellingWindReceipt
        {
            public int procs, pushes;
            public long windDamage;
            public bool restored;
        }
    }
}
