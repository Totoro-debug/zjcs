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
        }
        private readonly Dictionary<int, SummonLifeRecord> summonLives = new Dictionary<int, SummonLifeRecord>();
        private readonly HashSet<int> summonLifeBorn = new HashSet<int>();
        private readonly HashSet<int> summonLifeLastRound = new HashSet<int>();
        private readonly HashSet<int> summonLifeRetired = new HashSet<int>();
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
                    Require(life == null && unit.SkillState.Round == pair.Value.ExpireRound
                        && summonLifeLastRound.Contains(unit.Id),
                        "Summon ended outside its original final turn or before the last live frame: " + unit.Id);
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
                if (unit.Ended)
                {
                    Require(!root, "The original expired summon retained its scene model: " + unit.Id);
                    if (!summonLifeRetired.Add(unit.Id)) continue;
                    SaveSummonLifeCheckpoint(checkpoint, unit.Id, "retired", false);
                    CaptureName = "summon-life-" + firstPromotionItemId + "-retired-" + unit.Id;
                    return true;
                }
                var meshes = root ? root.GetComponentsInChildren<MeshRenderer>() : Array.Empty<MeshRenderer>();
                bool visible = meshes.Any(mesh => mesh.enabled && mesh.isVisible && !mesh.forceRenderingOff
                    && mesh.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0);
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
            if (summonLifeRetired.Count == summonLives.Count)
            {
                Require(summonLifeBorn.Count == summonLives.Count && summonLifeLastRound.Count == summonLives.Count,
                    "The actual summon birth, last live round and retirement were not all rendered.");
                summonLifeComplete = true;
                Debug.Log("[Summon lifespan] Original applicator turns, natural status End, live meshes, retirement and disk replay passed: "
                    + firstPromotionItemId + "; units=" + string.Join(",", summonLives.Keys));
                return false;
            }
            // Once the original skill is captured, wait for real turns instead of
            // allowing the fixture's finishing attack to end the battle early.
            return captured;
        }

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
