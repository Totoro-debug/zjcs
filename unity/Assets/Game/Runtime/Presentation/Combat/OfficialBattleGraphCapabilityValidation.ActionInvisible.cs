using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateActionInvisible => Environment.GetEnvironmentVariable("ZJCS_ACTION_INVISIBLE_ACCEPTANCE") == "1";
        private int invisiblePhase, invisibleEnemyId, invisibleSummonId;
        private float invisibleAttackClock, invisibleStarted;
        private bool invisibleAttackSettled, invisibleGraphObserved;
        private string invisibleMeshFailure;
        private readonly Dictionary<int, long> invisibleProtectedHp = new Dictionary<int, long>();
        private static readonly string[] InvisiblePhases = { "protected", "disabled", "reenabled", "removed" };

        private void ConfigureActionInvisibleDiagnostic(FightBattleCheckpoint checkpoint)
        {
            if (!ValidateActionInvisible) return;
            Require(new[] { 10025, 10058 }.Contains(firstPromotionItemId) && !ValidateSummonDeath,
                "The protection diagnostic requires original 10025/10058 without the death diagnostic.");
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var enemy = checkpoint.Units.First(unit => unit.Side != player.Side);
            // Begin computes the scheduler's intervals from these real inputs.
            // Changing Speed after Begin would leave its existing interval intact.
            foreach (var prop in enemy.Properties.Concat(enemy.BaseProperties ?? enemy.Properties))
                if (prop.Id == catalog.PropertyType("Speed")) prop.Value = 20000;
            enemy.Turn.Speed = 20000;
        }

        private bool ObserveActionInvisibleFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateActionInvisible || !captured || invisiblePhase == 5) return false;
            Require(invisibleMeshFailure == null, invisibleMeshFailure);
            var checkpoint = controller.Checkpoint;
            if (invisiblePhase == 0)
            {
                Require(new[] { 10025, 10058 }.Contains(firstPromotionItemId) && !ValidateSummonDeath,
                    "The protection diagnostic requires original 10025/10058 without the death diagnostic.");
                Require(firstPromotionSpawnedUnitIds.Length == firstPromotionExpectedSummonCount
                    && firstPromotionSpawnedUnitIds.Length > 0, "The original protected summons are missing.");
                var player = checkpoint.Units.Single(unit => unit.Id == 1);
                var enemy = checkpoint.Units.First(unit => unit.Side != player.Side && unit.Hp > 0);
                Require(enemy.MonsterId == 3331 && catalog.MonsterActiveSkillItems(3331, 0).Contains(14678),
                    "The original protection probe enemy attack binding changed.");
                foreach (int id in firstPromotionSpawnedUnitIds)
                {
                    var summon = checkpoint.Units.Single(unit => unit.Id == id);
                    var protection = summon.Statuses.Single(status => status.ClassId == 24008);
                    Require(protection.ForbiddenType == 0 && protection.DurationRound == -1
                        && protection.ActionType == 50 && summon.Targetable,
                        "The original summon did not receive its infinite active 24008 status.");
                }
                // Only the isolated roster and enemy equipment are controlled here.
                // Original AI, targeting, damage, hit graph and summon statuses remain live.
                foreach (var prop in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                    if (prop.Id == catalog.PropertyType("MaxHp")) prop.Value = 1000000000000;
                player.Hp = player.Turn.Hp = player.MaxHp = player.LastMaxHp = 1000000000000;
                var attack = catalog.CreateSkill(59460, 0, 14678, 2, 20, player.SubRank,
                    catalog.CalculateSkillProps(59460, 2, 20, player.SubRank)
                        .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
                enemy.Skills.Add(attack); enemy.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
                invisibleEnemyId = enemy.Id; invisibleSummonId = firstPromotionSpawnedUnitIds[0];
                invisibleStarted = Time.realtimeSinceStartup;
                checkpoint.MaxRound = 24;
                SetActionInvisiblePhase(checkpoint, 1);
            }
            Require(Time.realtimeSinceStartup - invisibleStarted < 90f,
                "Original protection battle did not complete phase " + invisiblePhase);
            Require(firstPromotionSpawnedUnitIds.All(id => checkpoint.Units.Any(unit => unit.Id == id && unit.Hp > 0 && !unit.Ended)),
                "A summon expired before the real protection/restore checks completed.");
            if (!invisibleAttackSettled) return true;
            var enemyBody = controller.TryGetUnitTransform(invisibleEnemyId);
            if (!invisibleGraphObserved && Time.time - invisibleAttackClock >= .1f)
            {
                invisibleGraphObserved = UnityEngine.Object.FindObjectsOfType<MyFightSkill>().Any(view =>
                    view.Component?.Entity?.ClassId == 59460 && view.Source && view.Source.transform == enemyBody);
            }
            if (Time.time - invisibleAttackClock < .55f) return true;
            Require(invisibleGraphObserved, "The original enemy attack graph did not render in the actual scene.");
            foreach (int id in firstPromotionSpawnedUnitIds.Concat(new[] { invisibleEnemyId }))
            {
                var body = controller.TryGetUnitTransform(id);
                var renderer = body ? body.GetComponentInChildren<BattleNativeMonsterRenderer>() : null;
                var meshRenderer = renderer ? renderer.GetComponent<MeshRenderer>() : null;
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                Require(meshRenderer && meshRenderer.enabled && !meshRenderer.forceRenderingOff
                    && mesh && mesh.vertexCount > 0 && mesh.vertices.All(vertex =>
                        FiniteAirCombo(vertex.x) && FiniteAirCombo(vertex.y) && FiniteAirCombo(vertex.z)),
                    "Protection hid or corrupted an original visible summon/enemy mesh: " + id);
                // The original enemy can approach from outside the player-follow camera.
                // A culled enemy must have its center outside the viewport; the protected
                // summons themselves must remain visible in every captured phase.
                var cameras = Camera.allCameras.Where(camera =>
                    (camera.cullingMask & (1 << meshRenderer.gameObject.layer)) != 0).ToArray();
                Require(cameras.Length > 0, "No active camera renders the original summon/enemy layer.");
                bool outside = cameras.All(camera => {
                    Vector3 point = camera.WorldToViewportPoint(meshRenderer.bounds.center);
                    return point.z <= 0 || point.x <= 0 || point.x >= 1 || point.y <= 0 || point.y >= 1;
                });
                if (!meshRenderer.isVisible && !(id == invisibleEnemyId && outside))
                {
                    invisibleMeshFailure = "An in-frame original summon/enemy mesh did not render: " + id
                        + "; bounds=" + meshRenderer.bounds + "; position=" + body.position
                        + "; cameras=" + string.Join(",", cameras.Select(camera => camera.name
                            + ": viewport=" + camera.WorldToViewportPoint(meshRenderer.bounds.center)
                            + ", bounds viewport min=" + camera.WorldToViewportPoint(meshRenderer.bounds.min)
                            + ", max=" + camera.WorldToViewportPoint(meshRenderer.bounds.max)
                            + ", pixels=" + camera.pixelRect));
                    CaptureName = "action-invisible-mesh-failure";
                    return true;
                }
                if (!meshRenderer.isVisible)
                    Debug.Log("[Action invisible] Original enemy center outside camera viewport; bounds=" + meshRenderer.bounds);
            }
            CaptureName = "action-invisible-" + firstPromotionItemId + "-" + InvisiblePhases[invisiblePhase - 1];
            if (invisiblePhase < 4) SetActionInvisiblePhase(checkpoint, invisiblePhase + 1);
            else
            {
                invisiblePhase = 5;
                checkpoint.Units.Single(unit => unit.Id == 1).Targetable = true;
                var enemy = checkpoint.Units.Single(unit => unit.Id == invisibleEnemyId);
                enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
                Debug.Log("[Action invisible] Actual original enemy AI/59460 graph, protection, disable/re-enable, removal, visible meshes and Unity disk checkpoints passed: "
                    + firstPromotionItemId);
            }
            return true;
        }

        private void SetActionInvisiblePhase(FightBattleCheckpoint checkpoint, int phase)
        {
            invisiblePhase = phase; invisibleAttackSettled = invisibleGraphObserved = false;
            var summon = checkpoint.Units.Single(unit => unit.Id == invisibleSummonId);
            var status = summon.Statuses.Single(value => value.ClassId == 24008);
            if (phase == 2 || phase == 3)
                Require(FightStatusLifecycle.SetForbidden(summon, status, FightStatusLifecycle.ForbiddenStatusTrigger, phase == 2),
                    "The real protection status did not disable/re-enable.");
            if (phase == 4) Require(FightStatusLifecycle.End(summon, status), "The real protection status did not end.");
            checkpoint.Units.Single(unit => unit.Id == 1).Targetable = phase == 1 || phase == 3;
            invisibleProtectedHp.Clear();
            foreach (int id in firstPromotionSpawnedUnitIds)
            {
                var unit = checkpoint.Units.Single(value => value.Id == id);
                if (unit.Statuses.Any(value => value.ClassId == 24008 && value.ForbiddenType == 0))
                    invisibleProtectedHp.Add(id, unit.Hp);
            }
        }

        private void ObserveActionInvisibleStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateActionInvisible || invisiblePhase == 0 || invisiblePhase == 5) return;
            var checkpoint = controller.Checkpoint;
            var step = checkpoint.Receipts.Last().Event;
            var attacks = step.SkillCasts.Where(cast => cast.ActorId == invisibleEnemyId && cast.SkillEntityId == 59460).ToArray();
            if (attacks.Length == 0) return;
            Require(!invisibleAttackSettled && attacks.Length == 1, "The real enemy attacked twice before its phase was observed.");
            int expected = invisiblePhase == 1 || invisiblePhase == 3 ? 1 : invisibleSummonId;
            Require(attacks[0].TargetIds.SequenceEqual(new[] { expected }),
                "Original enemy AI selected the wrong target in protection phase " + invisiblePhase);
            var hits = step.DamageEvents.Where(hit => hit.SourceId == invisibleEnemyId && !hit.IsCure).ToArray();
            Require(hits.Length > 0 && hits.All(hit => hit.TargetId == expected)
                && hits.Any(hit => hit.AfterHp < hit.BeforeHp),
                "The actual enemy attack did not damage the sole eligible target.");
            Require(invisibleProtectedHp.All(pair => checkpoint.Units.Single(unit => unit.Id == pair.Key).Hp == pair.Value),
                "An active protected summon took damage from the original enemy attack.");
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Require(!string.IsNullOrEmpty(output), "The protection diagnostic requires its isolated output directory.");
            string path = Path.Combine(output, "action-invisible-" + firstPromotionItemId + "-" + InvisiblePhases[invisiblePhase - 1] + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(checkpoint));
            var restored = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                JsonUtility.FromJson<FightBattleCheckpoint>(File.ReadAllText(path))).State;
            var restoredEnemy = restored.Units.Single(unit => unit.Id == invisibleEnemyId);
            Require(FightBattleTargeting.Select(restoredEnemy, restored.Units, 1)
                .Select(unit => unit.Id).SequenceEqual(new[] { expected }),
                "Unity disk restore changed protection/disabled/removed target eligibility.");
            Debug.Log("[Action invisible] Phase=" + InvisiblePhases[invisiblePhase - 1]
                + "; original enemy target=" + expected + "; damage=" + hits.Sum(hit => hit.BeforeHp - hit.AfterHp)
                + "; protected=" + string.Join(",", invisibleProtectedHp.Keys));
            invisibleAttackSettled = true; invisibleAttackClock = Time.time + attacks[0].Delay;
        }

        private void VerifyActionInvisibleDiagnostic()
        {
            if (ValidateActionInvisible) Require(invisiblePhase == 5,
                "The original protected summon did not complete all four actual enemy attack phases.");
        }
    }
}
