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
        private bool ValidateSummonLife => Environment.GetEnvironmentVariable("ZJCS_SUMMON_LIFE_ACCEPTANCE") == "1";
        private sealed class SummonLifeRecord
        {
            public int StatusId, StartRound, Duration, ExpireRound;
            public float EndClock = -1f, EndDelay, NativeEndDelay;
            public readonly HashSet<string> CastAnimations = new HashSet<string>();
        }
        private readonly Dictionary<int, SummonLifeRecord> summonLives = new Dictionary<int, SummonLifeRecord>();
        private readonly HashSet<int> summonLifeBorn = new HashSet<int>();
        private readonly HashSet<int> summonLifeLastRound = new HashSet<int>();
        private readonly HashSet<int> summonLifeRetired = new HashSet<int>();
        private readonly HashSet<int> summonLifeFinalCast = new HashSet<int>();
        private readonly HashSet<int> summonLifeFinalFrame = new HashSet<int>();
        private readonly HashSet<int> summonLifeSettled = new HashSet<int>();
        private float summonLifeStarted;
        private bool summonLifeComplete;

        private void ConfigureSummonLifeDiagnostic(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateSummonLife) return;
            Require(new[] { 10019, 10025, 10058 }.Contains(firstPromotionItemId)
                && !ValidateSummonDeath && !ValidateActionInvisible,
                "The natural lifespan diagnostic requires one original summon skill without another lifecycle probe.");
            // Only extend the isolated battle limit. Keep original summon skills,
            // speed, lifetime statuses, scheduler and ordinary turn execution.
            checkpoint.MaxRound = 64;
        }

        private void ObserveSummonLifeStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateSummonLife || firstPromotionSpawnedUnitIds.Length == 0 || summonLifeComplete) return;
            if (summonLives.Count == 0)
            {
                Require(firstPromotionSpawnedUnitIds.Length == firstPromotionExpectedSummonCount,
                    "The natural lifespan diagnostic received an unexpected original summon count.");
                summonLifeStarted = Time.realtimeSinceStartup;
                foreach (int id in firstPromotionSpawnedUnitIds)
                {
                    var unit = checkpoint.Units.Single(value => value.Id == id);
                    Require(new[] { 3181, 3182, 3183 }.Contains(unit.MonsterId),
                        "The natural lifespan diagnostic received an unexpected original summon model.");
                    int statusId = unit.MonsterId == 3181 ? 10712 : unit.MonsterId == 3183 ? 10772 : 11142;
                    var life = unit.Statuses.Single(value => value.ClassId == statusId);
                    int duration = unit.MonsterId == 3182 ? 2 : 5;
                    if (Environment.GetEnvironmentVariable("ZJCS_STATUS_LIFE_REDUCTION_ACCEPTANCE") == "1") duration -= 2;
                    Require(life.DurationRound == (unit.MonsterId == 3182 ? 2 : 5)
                        && life.CurDurationRound == duration && duration > 0 && life.StartRound >= 0
                        && life.RoundTarget == FightStatusLifecycle.TargetApplicator
                        && life.OwnerId == id && life.RoundUpdateTiming == FightStatusLifecycle.TimingEnd
                        && life.RemoveApplyEntityOnEnd && !unit.Ended && unit.Hp > 0,
                        "Original summon lifespan, applicator clock or end binding differs: " + id);
                    summonLives.Add(id, new SummonLifeRecord { StatusId = statusId, StartRound = life.StartRound,
                        Duration = duration, ExpireRound = life.StartRound + duration });
                }
            }
            foreach (var pair in summonLives)
            {
                var unit = checkpoint.Units.Single(value => value.Id == pair.Key);
                var life = unit.Statuses.SingleOrDefault(value => value.ClassId == pair.Value.StatusId);
                Require(unit.Hp > 0, "A summon died instead of naturally expiring: " + unit.Id);
                if (unit.Ended)
                {
                    Require(life == null && unit.SkillState.Round == pair.Value.ExpireRound
                        && summonLifeLastRound.Contains(unit.Id),
                        "Summon ended outside its original final turn or before the last live frame: " + unit.Id);
                    if (pair.Value.EndClock < 0f)
                    {
                        var receipt = checkpoint.Receipts.Last().Event;
                        var end = receipt.UnitEndEvents.Single(value => value.UnitId == unit.Id);
                        Require(end.StatusId == pair.Value.StatusId && end.Delay > .2f
                            && Mathf.Abs(end.Delay - receipt.ActionDuration) < .00001f
                            && receipt.SkillCasts.Any(value => value.ActorId == unit.Id),
                            "The original summon final cast has no exact action-end removal receipt: " + unit.Id);
                        pair.Value.EndClock = Time.time;
                        pair.Value.EndDelay = end.Delay;
                        var views = receipt.SkillCasts.Where(value => value.ActorId == unit.Id
                            && value.Presentation != null).ToArray();
                        Require(views.Length > 0 && views.All(value => value.Presentation.Duration >= 0f
                            && !float.IsInfinity(value.Presentation.Duration) && !float.IsNaN(value.Presentation.Duration)),
                            "The original final summon cast requires finite native entity lifetimes.");
                        pair.Value.NativeEndDelay = Mathf.Max(end.Delay,
                            views.Max(value => value.Delay + value.Presentation.Duration));
                    }
                }
                else
                    Require(life != null && life.StartRound == pair.Value.StartRound
                        && life.CurDurationRound == pair.Value.Duration && unit.SkillState.Round < pair.Value.ExpireRound,
                        "Original lifespan changed or failed to expire on its applicator's turn: " + unit.Id);
            }
        }

        private bool ObserveSummonLifeFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateSummonLife || summonLives.Count == 0 || summonLifeComplete) return false;
            Require(Time.realtimeSinceStartup - summonLifeStarted < 180f,
                "The actual summon lifespan battle did not reach natural expiry.");
            var checkpoint = controller.Checkpoint;
            foreach (var pair in summonLives)
            {
                var unit = checkpoint.Units.Single(value => value.Id == pair.Key);
                var root = controller.TryGetUnitTransform(unit.Id);
                var record = pair.Value;
                var renderer = root ? root.GetComponentInChildren<Zjcs.Offline.ProfessionUI.BattleNativeMonsterRenderer>() : null;
                var meshes = root ? root.GetComponentsInChildren<MeshRenderer>() : Array.Empty<MeshRenderer>();
                bool visible = meshes.Any(mesh => mesh.enabled && mesh.isVisible && !mesh.forceRenderingOff
                    && mesh.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0);
                string animation = renderer ? renderer.Animation : null;
                bool casting = !string.IsNullOrEmpty(animation)
                    && animation != "idle_01" && animation != "run_01" && animation != "generate_01" && animation != "die_01";
                if (!unit.Ended && casting) record.CastAnimations.Add(animation);
                if (unit.Ended)
                {
                    float elapsed = Time.time - record.EndClock;
                    float tolerance = Mathf.Max(.0001f, Time.deltaTime * 2f);
                    if (root)
                    {
                        Require(elapsed < record.EndDelay + tolerance,
                            "The original expired summon exceeded its recorded entity-removal time: " + unit.Id);
                        if (!visible) continue;
                        if (elapsed >= record.EndDelay * .15f && casting && record.CastAnimations.Contains(animation)
                            && renderer.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > .05f
                            && summonLifeFinalCast.Add(unit.Id))
                        {
                            SaveSummonLifeCheckpoint(checkpoint, unit.Id, "final-cast", false);
                            Debug.Log("[Summon lifespan] Final original cast visible: unit=" + unit.Id + "; animation=" + animation);
                            CaptureName = "summon-life-" + firstPromotionItemId + "-final-cast-" + unit.Id;
                            return true;
                        }
                        if (elapsed >= record.EndDelay * .75f && summonLifeFinalFrame.Add(unit.Id))
                        {
                            Require(summonLifeFinalCast.Contains(unit.Id), "The original final action pose was not rendered.");
                            SaveSummonLifeCheckpoint(checkpoint, unit.Id, "final-frame", false);
                            CaptureName = "summon-life-" + firstPromotionItemId + "-final-frame-" + unit.Id;
                            return true;
                        }
                        continue;
                    }
                    Require(elapsed + tolerance >= record.EndDelay && summonLifeFinalFrame.Contains(unit.Id),
                        "The original summon model disappeared before its final action ended: " + unit.Id);
                    if (summonLifeRetired.Add(unit.Id))
                    {
                        if (record.NativeEndDelay > record.EndDelay + .05f)
                            Require(HasOriginalSummonSkillView(unit.Id),
                                "Removing the model also cut short its original native skill lifetime: " + unit.Id);
                        SaveSummonLifeCheckpoint(checkpoint, unit.Id, "retired", false);
                        CaptureName = "summon-life-" + firstPromotionItemId + "-retired-" + unit.Id;
                        return true;
                    }
                    if (elapsed < record.NativeEndDelay + Mathf.Max(.05f, Time.deltaTime * 2f)) continue;
                    Require(!HasOriginalSummonSkillView(unit.Id),
                        "The original final skill entity survived its recorded lifetime: " + unit.Id);
                    if (summonLifeSettled.Add(unit.Id))
                    {
                        SaveSummonLifeCheckpoint(checkpoint, unit.Id, "post-retirement", false);
                        CaptureName = "summon-life-" + firstPromotionItemId + "-post-retirement-" + unit.Id;
                        return true;
                    }
                    continue;
                }
                if (!visible) continue;
                if (summonLifeBorn.Add(unit.Id))
                {
                    SaveSummonLifeCheckpoint(checkpoint, unit.Id, "born", false);
                    CaptureName = "summon-life-" + firstPromotionItemId + "-born-" + unit.Id;
                    return true;
                }
                if (unit.SkillState.Round == pair.Value.ExpireRound - 1 && summonLifeLastRound.Add(unit.Id))
                {
                    Require(meshes.All(mesh => !mesh.GetComponent<MeshFilter>()?.sharedMesh
                        || mesh.GetComponent<MeshFilter>().sharedMesh.vertices.All(vertex =>
                            FiniteAirCombo(vertex.x) && FiniteAirCombo(vertex.y) && FiniteAirCombo(vertex.z))),
                        "The last live original summon pose contains invalid vertices: " + unit.Id);
                    SaveSummonLifeCheckpoint(checkpoint, unit.Id, "last-round", true);
                    CaptureName = "summon-life-" + firstPromotionItemId + "-last-round-" + unit.Id;
                    return true;
                }
            }
            if (summonLifeSettled.Count == summonLives.Count)
            {
                Require(summonLifeBorn.Count == summonLives.Count && summonLifeLastRound.Count == summonLives.Count,
                    "The actual summon birth, last live round and retirement were not all rendered.");
                Require(summonLifeFinalCast.Count == summonLives.Count && summonLifeFinalFrame.Count == summonLives.Count,
                    "The original final cast and pre-removal model were not both rendered.");
                Require(summonLifeRetired.Count == summonLives.Count,
                    "The original models were not removed before their native skill lifetimes settled.");
                summonLifeComplete = true;
                Debug.Log("[Summon lifespan] Original applicator turns, natural status End, live meshes, retirement, native skill cleanup and disk replay passed: "
                    + firstPromotionItemId + "; units=" + string.Join(",", summonLives.Keys));
                return false;
            }
            // Once the original skill is captured, wait for real turns instead of
            // allowing the fixture's finishing attack to end the battle early.
            return captured;
        }

        private static bool HasOriginalSummonSkillView(int id) =>
            UnityEngine.Object.FindObjectsOfType<Zjcs.Official.BattleSkillPlayables.MyFightSkill>()
                .Any(value => value && value.Component != null
                    && value.Component.SrcEntityId.ContainerId == 1 && value.Component.SrcEntityId.EntityId == id);

        private void SaveSummonLifeCheckpoint(FightBattleCheckpoint checkpoint, int id, string phase, bool advance)
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Require(!string.IsNullOrWhiteSpace(output), "The lifespan diagnostic requires its isolated output directory.");
            string path = Path.Combine(output, "summon-life-" + firstPromotionItemId + "-" + phase + "-" + id + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(checkpoint));
            var restored = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                JsonUtility.FromJson<FightBattleCheckpoint>(File.ReadAllText(path)));
            var current = checkpoint.Units.Single(value => value.Id == id);
            var saved = restored.State.Units.Single(value => value.Id == id);
            var record = summonLives[id];
            Require(saved.Ended == current.Ended && saved.Hp == current.Hp && saved.SkillState.Round == current.SkillState.Round,
                "Disk restore changed the summon lifecycle: " + id);
            var endEvents = checkpoint.Receipts.SelectMany(value => value.Event.UnitEndEvents).ToArray();
            Require(!current.Ended || endEvents.Any(value => value.UnitId == id),
                "The original removal receipt disappeared before its native skill finished.");
            foreach (var receipt in endEvents)
            {
                var persisted = restored.State.Receipts.SelectMany(value => value.Event.UnitEndEvents)
                    .Single(value => value.UnitId == receipt.UnitId);
                Require(persisted.Delay == receipt.Delay && persisted.StatusId == receipt.StatusId,
                    "Unity disk restore lost the original entity-removal time.");
            }
            if (!current.Ended)
            {
                var life = saved.Statuses.Single(value => value.ClassId == record.StatusId);
                Require(life.StartRound == record.StartRound && life.CurDurationRound == record.Duration,
                    "Disk restore restarted or debited the original lifespan: " + id);
            }
            for (int i = 0; advance && !saved.Ended && !restored.State.Finished && i < 512; i++)
                restored.Step(restored.State.Sequence, restored.State.Counter + 1);
            if (advance)
                Require(saved.Ended && saved.Hp > 0 && saved.SkillState.Round == record.ExpireRound
                    && !saved.Statuses.Any(value => value.ClassId == record.StatusId),
                    "Disk replay did not naturally end the summon on its original final turn: " + id);
            Debug.Log("[Summon lifespan] " + phase + "; unit=" + id + "; round=" + current.SkillState.Round
                + "; expires=" + record.ExpireRound + "; hp=" + current.Hp + "; ended=" + current.Ended);
        }

        private void VerifySummonLifeDiagnostic()
        {
            if (ValidateSummonLife) Require(summonLifeComplete,
                "The original summon natural expiry was not settled, rendered and restored.");
        }
    }
}
