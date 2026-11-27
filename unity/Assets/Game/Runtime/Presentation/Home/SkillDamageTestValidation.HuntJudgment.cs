using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        [Serializable] private sealed class HuntJudgmentReceipt
        {
            public int casts, hits, pulls;
            public int pullGraphSamples, pullOwnerSamples, pullVisibleSamples, pullMotionSamples;
            public bool restored, castCaptured, pullCaptured, launchCaptured, recoveryCaptured;
        }
        private static HuntJudgmentReceipt huntJudgment;
        private static readonly Dictionary<int, Vector3> huntPullPositions = new Dictionary<int, Vector3>();
        private static float huntMaximumDeltaTime;

        private static void PrepareAcquiredHuntJudgment(FightBattleCheckpoint checkpoint)
        {
            huntJudgment = new HuntJudgmentReceipt();
            huntPullPositions.Clear();
            huntMaximumDeltaTime = Time.maximumDeltaTime;
            // Keep a cold asset/JIT frame from consuming the entire authored
            // 0.1-second pull between two rendered diagnostic observations.
            Time.maximumDeltaTime = 1f / 60f;
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var enemy = checkpoint.Units.Single(unit => unit.Id == 2);
            Require(Math.Abs(enemy.X - actor.X) + Math.Abs(enemy.Y - actor.Y) == 4,
                "Review the isolated four-cell grab scenario before changing its initial roster.");
            // Hold the diagnostic roster at its normal spawn points until the
            // original cooldown permits the first real grab. Skill data is intact.
            SetAcquiredBattleProperty(actor, "MoveDist", 0);
            SetAcquiredBattleProperty(enemy, "MoveDist", 0);
            actor.MoveDist = 0; enemy.MoveDist = 0;
        }

        private static void VerifyAcquiredHuntJudgment(FightBattleStepEvent step,
            FightBattleSkillCastEvent cast, FightBattleDamageEvent[] hits,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == cast.ActorId);
            var active = actor.Skills.Single(skill => skill.Slot == 0);
            var grab = hits.Where(hit => hit.HitEntityId == 11821).ToArray();
            var physical = hits.Where(hit => hit.HitEntityId == 11823).ToArray();
            Require(cast.SkillEntityId == 11820 && cast.SkillItemId == 10074 && active.Level == 2
                && grab.Length > 0 && grab.All(hit => hit.Damage == 0 && hit.DamageType == 0
                    && hit.DamageEntityId == 11822 && hit.TargetId == 2)
                && physical.Length == 3 * grab.Length && physical.All(hit => hit.Damage < 0
                    && hit.SkillEntityId == 11820 && hit.SkillItemId == 10074 && hit.TargetId == 2
                    && hit.DamageEntityId == 11824 && hit.ElementType == 0 && hit.DamageType == 1
                    && !hit.TrueDamage && !hit.IsCure),
                "Hunt Judgment lost its original nondamaging grab or three cultivated physical strikes.");
            foreach (float at in new[] { 1.1f, 1.2f, 1.3f })
                Require(physical.Count(hit => Math.Abs(hit.Delay - cast.Delay - at) < .0001f) == grab.Length,
                    "Hunt Judgment changed a configured physical-strike delay.");
            var children = step.SkillCasts.Where(value => value.ActorId == actor.Id && value.SkillEntityId == 11830).ToArray();
            Require(children.Length == grab.Length && children.All(value => Math.Abs(value.Delay - cast.Delay - .05f) < .0001f),
                "Hunt Judgment did not preserve each original grab child and its nested timing.");
            var source = new FightGridPoint(cast.Presentation.SourceX, cast.Presentation.SourceY);
            var front = FightGridTransformMath.CalcLocalToWorldPos(new FightGridPoint(0, 1), source,
                0f, 0f, FightGridTransformMath.DirectionFromFacingIndex(cast.Presentation.FacingIndex), cast.Presentation.FlipX);
            foreach (var move in step.Movements.Where(value => value.MoveBehaviourId == 11833))
            {
                Require(move.UnitId == 2 && move.Path.Count > 1 && move.Path.Last().Equals(front)
                    && Math.Abs(move.Delay - cast.Delay - .45f) < .0001f
                    && Math.Abs(move.MoveDuration - .1f) < .0001f,
                    "Hunt Judgment lost its captured-facing front landing, original move action or nested delay.");
                huntJudgment.pulls++;
            }
            huntJudgment.casts++; huntJudgment.hits += physical.Length;
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Restoring Hunt Judgment duplicated its grab, movement or physical strikes.");
            huntJudgment.restored = true;
            File.WriteAllText(Path.Combine(output, "acquired-hunt-judgment-checkpoint.json"), saved);
        }

        private static bool ObserveAcquiredHuntPull()
        {
            // The original move graph contains a track, with no particles.
            // Require movement of the real rendered enemy across two frames.
            foreach (var playable in FindObjectsOfType<MyPlayable>())
            {
                if (!playable.name.StartsWith("playable11830_move", StringComparison.Ordinal)
                    || playable.myStatus == MyPlayable.PlayableStatus.Stop) continue;
                huntJudgment.pullGraphSamples++;
                var owner = playable.mySource ? playable.mySource.GetComponentInParent<MyEntity>() : null;
                if (!owner || owner.Id.EntityId != 2) continue;
                huntJudgment.pullOwnerSamples++;
                var renderer = owner.GetComponentInChildren<Zjcs.Offline.ProfessionUI.BattleNativeMonsterRenderer>();
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                var visible = renderer ? renderer.GetComponent<MeshRenderer>() : null;
                if (!mesh || mesh.vertexCount == 0 || !visible || !visible.isVisible
                    || !visible.enabled || visible.forceRenderingOff) continue;
                huntJudgment.pullVisibleSamples++;
                int key = playable.GetInstanceID();
                Vector3 position = renderer.transform.position;
                bool moved = huntPullPositions.TryGetValue(key, out var previous)
                    && Vector3.Distance(previous, position) > .001f;
                huntPullPositions[key] = position;
                if (moved) { huntJudgment.pullMotionSamples++; return true; }
            }
            return false;
        }

        private static void CompleteAcquiredHuntJudgment(string output)
        {
            Time.maximumDeltaTime = huntMaximumDeltaTime;
            File.WriteAllText(Path.Combine(output, "acquired-hunt-judgment-receipt.json"), JsonUtility.ToJson(huntJudgment, true));
            Require(huntJudgment.casts > 0 && huntJudgment.hits >= 3 && huntJudgment.pulls > 0
                && huntJudgment.restored && huntJudgment.castCaptured && huntJudgment.pullCaptured
                && huntJudgment.launchCaptured && huntJudgment.recoveryCaptured,
                "Hunt Judgment must render its original grab, moving pull, three strikes and both airborne phases.");
            Debug.Log("[Acquired active combat] 10074 cultivated facing-relative grab, nested pull, three physical strikes, native airborne phases and restoration passed.");
        }
    }
}
