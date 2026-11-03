using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static GridMotionProbe ConfigureGridMotionProbe(OfficialBattleRuntimeController controller)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_GRID_MOTION_ACCEPTANCE") != "1") return null;
            VerifyOriginalGridTracks();
            VerifyMovementFacing();
            return new GridMotionProbe(controller);
        }

        private static void VerifyOriginalGridTracks()
        {
            var rules = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = new FightBattleUnitState { Id = 1, CharacterType = 1, Hp = 100, MaxHp = 100 };
            FightBattleMovementEvent forward = rules.CreateRoleMovement(player, false);
            FightBattleMovementEvent back = rules.CreateRoleMovement(player, true);
            Require(forward.MoveSkillEntityId == 15300 && forward.MoveBehaviourId == 15301
                && Mathf.Abs(forward.MoveDuration - .3f) < .00001f, "Player forward action/configuration was lost.");
            Require(back.MoveSkillEntityId == 15310 && back.MoveBehaviourId == 15311,
                "Retreat reused the forward movement configuration.");
            Require(back.IsRetreat, "A retreat event lost its action kind.");
            var monster = new FightBattleUnitState { Id = 2, MonsterId = 3206, CharacterType = 2 };
            var monsterMove = rules.CreateRoleMovement(monster, false);
            Require(monsterMove.MoveBehaviourId == 19811 && monsterMove.MoveDuration < 0f
                && Mathf.Approximately(monsterMove.MoveSpeed, 5f), "Monster movement ignored its own source configuration.");
            Require(rules.CreateRoleMovement(monster, true).MoveBehaviourId == 0,
                "An unconfigured monster retreat was replaced with an invented original action.");
            var guard = new FightBattleUnitState { Id = 3, MonsterId = 3206, CharacterType = 1 };
            Require(rules.CreateRoleMovement(guard, false).MoveBehaviourId == 15301,
                "An arena role player used its stat-source monster movement.");
            var root = new GameObject("OriginalGridTrackFixture");
            var model = new GameObject("OriginalGridTrackAttachedModel");
            OfficialBattleSceneView scene = null;
            try
            {
                var grid = new FightBattleGrid { Width = 4, Height = 4 };
                var mapper = new OfficialBattleGridMapper(root.transform, grid);
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                    _ => null, mapper, (_, __) => null, (_, __) => { });
                var checkpoint = new FightBattleCheckpoint { Grid = grid };
                checkpoint.Units.Add(player);
                scene.Refresh(checkpoint);
                scene.AttachUnitTransform(1, model.transform);
                Transform body = root.transform.Find("Battle attached unit 3100");
                Require(body != null, "Movement fixture did not use the common entity attachment.");
                Vector3 start = mapper.GridToWorld(new FightGridPoint(0, 0));
                Vector3 middle = mapper.GridToWorld(new FightGridPoint(1, 0));
                Vector3 end = mapper.GridToWorld(new FightGridPoint(2, 0));
                forward.Path.Add(new FightGridPoint(0, 0));
                forward.Path.Add(new FightGridPoint(1, 0));
                forward.Path.Add(new FightGridPoint(2, 0));
                forward.Delay = .2f;
                var step = new FightBattleStepEvent { ActorId = 1 };
                step.Movements.Add(forward);
                player.X = 2;
                scene.Refresh(checkpoint, step);
                scene.Tick(.1f);
                Require((body.position - start).sqrMagnitude < 1e-8f, "Original move delay was ignored.");
                scene.Tick(0f);
                Require((body.position - start).sqrMagnitude < 1e-8f, "Paused movement advanced.");
                scene.Tick(.175f); // 0.075 seconds into the 0.3-second first segment.
                // Independent original Hermite endpoints/slopes, read from the selected prefab.
                float t = .075f * 3.333400011062622f;
                float value = (t * t * t - 2 * t * t + t) * 2.8207437992095947f
                    + (-2 * t * t * t + 3 * t * t)
                    + (t * t * t - t * t) * .15966713428497314f;
                Require((body.position - Vector3.LerpUnclamped(start, middle, value)).sqrMagnitude < 1e-7f,
                    "The common scene did not evaluate the original movement curve.");
                Require((body.position - Vector3.Lerp(start, middle, .25f)).sqrMagnitude > .001f,
                    "The movement regression fixture still accepts straight linear interpolation.");
                scene.Tick(.225f);
                Require((body.position - middle).sqrMagnitude < 1e-7f,
                    "A cell boundary did not land at its grid: actual=" + body.position.ToString("R")
                    + " expected=" + middle.ToString("R") + " duration=" + forward.MoveDuration.ToString("R"));
                scene.Tick(.31f);
                Require((body.position - end).sqrMagnitude < 1e-7f, "The path failed to restore its final grid.");

                back.MoveDuration = .3f;
                back.Delay = 0f;
                back.Path.AddRange(new[] { new FightGridPoint(2, 1), new FightGridPoint(1, 1) });
                player.X = 1;
                player.Y = 1;
                var retreatStep = new FightBattleStepEvent { ActorId = player.Id };
                retreatStep.Movements.Add(back);
                scene.Refresh(checkpoint, retreatStep);
                Vector3 retreatStart = mapper.GridToWorld(back.Path[0]);
                Vector3 retreatEnd = mapper.GridToWorld(back.Path[1]);
                scene.Tick(.03f);
                float u = .1f / .15035143494606018f;
                float u2 = u * u;
                float u3 = u2 * u;
                float segment = .15035143494606018f;
                float retreatProgress = (u3 - 2f * u2 + u) * segment * 2.932962656021118f
                    + (-2f * u3 + 3f * u2) * .34639856219291687f
                    + (u3 - u2) * segment * 1.7188221216201782f;
                Vector3 retreatExpected = Vector3.LerpUnclamped(retreatStart, retreatEnd, retreatProgress);
                Require((body.position - retreatExpected).sqrMagnitude < 1e-7f,
                    "Retreat did not follow its original non-linear curve: actual=" + body.position.ToString("R")
                    + " expected=" + retreatExpected.ToString("R"));
                Require((body.position - Vector3.Lerp(retreatStart, retreatEnd, .1f)).sqrMagnitude > .001f,
                    "The retreat regression still accepts straight linear interpolation.");
                scene.Tick(.27f);
                Require((body.position - retreatEnd).sqrMagnitude < 1e-7f,
                    "Retreat did not land on its final grid cell.");
                Debug.Log("[Grid motion] original forward/back identities, delay, pause, independent forward and retreat curve references, cell boundary and final grids passed.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(model);
            }
        }

        private static void VerifyMovementFacing()
        {
            var rules = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = new FightBattleUnitState { Id = 1, CharacterType = 1, Hp = 100, MaxHp = 100 };
            var root = new GameObject("MovementFacingFixture");
            var model = new GameObject("MovementFacingAttachedModel");
            OfficialBattleSceneView scene = null;
            try
            {
                var grid = new FightBattleGrid { Width = 4, Height = 4 };
                var mapper = new OfficialBattleGridMapper(root.transform, grid);
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                    _ => null, mapper, (_, __) => null, (target, unit) =>
                    {
                        Vector3 scale = target.localScale;
                        scale.x = Mathf.Abs(scale.x) * ((unit.HasFlipX ? unit.FlipX : unit.Direction == 1) ? -1 : 1);
                        target.localScale = scale;
                    });
                var checkpoint = new FightBattleCheckpoint { Grid = grid };
                checkpoint.Units.Add(player); scene.Refresh(checkpoint); scene.AttachUnitTransform(1, model.transform);
                Transform flipTarget = model.transform.parent;
                Vector3 scaleBefore = flipTarget.localScale;
                var back = rules.CreateRoleMovement(player, true);
                back.Path.AddRange(new[] { new FightGridPoint(2, 0), new FightGridPoint(1, 0), new FightGridPoint(0, 0) });
                rules.ApplyMovementFacing(player, back);
                Require(back.HasFacing && !back.EndFlipX && player.HasFlipX && !player.FlipX,
                    "Original retreat UpdateFlipX=false must retain authoritative facing.");
                var step = new FightBattleStepEvent { ActorId = 1 }; step.Movements.Add(back);
                scene.Refresh(checkpoint, step); scene.Tick(.01f);
                Require(flipTarget.localScale.x < 0, "Intermediate retreat cell did not face its original grid direction.");
                scene.Tick(back.MoveDuration);
                Require(flipTarget.localScale.x > 0, "Final retreat cell did not restore authoritative facing before motion ended.");
                scene.Tick(1f); scene.Refresh(checkpoint);
                Require(flipTarget.localScale == scaleBefore, "Finishing/refeshing movement changed the restored visual transform.");
                var forward = rules.CreateRoleMovement(player, false);
                forward.Path.AddRange(new[] { new FightGridPoint(2, 1), new FightGridPoint(1, 1) });
                rules.ApplyMovementFacing(player, forward);
                Require(forward.EndFlipX && player.FlipX, "Forward action did not commit the last grid segment facing.");
                var vertical = rules.CreateRoleMovement(player, false);
                vertical.Path.AddRange(new[] { new FightGridPoint(1, 1), new FightGridPoint(1, 2) });
                rules.ApplyMovementFacing(player, vertical);
                Require(vertical.EndFlipX && player.FlipX, "Vertical movement lost its previous authoritative facing.");
                var reverse = new FightBattleMovementEvent { UnitId = 1, MoveBehaviourId = 61771 };
                reverse.Path.AddRange(forward.Path); rules.ApplyMovementFacing(player, reverse);
                Require(!reverse.EndFlipX && !player.FlipX, "Original reverse-facing action ignored ReverseFlipX.");
                player.HasFlipX = false; player.Direction = 1;
                rules.ApplyMovementFacing(player, vertical);
                Require(player.FlipX, "Older checkpoints lost their pre-existing direction fallback.");
                var unknown = new FightBattleMovementEvent { UnitId = 1 };
                unknown.Path.AddRange(forward.Path); rules.ApplyMovementFacing(player, unknown);
                Require(!unknown.HasFacing && player.FlipX, "Unconfigured action invented an original facing change.");
                Debug.Log("[Movement facing] original forward/retreat/vertical, final-cell restore and legacy checkpoint fallback passed.");
            }
            finally { scene?.Dispose(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(model); }
        }

        private sealed class GridMotionProbe
        {
            private readonly OfficialBattleRuntimeController controller;
            private readonly HashSet<int> actions = new HashSet<int>();
            private int movements;
            private int unconfiguredRetreats;
            private readonly FightOfficialCombatCatalog rules;
            public GridMotionProbe(OfficialBattleRuntimeController controller)
            { this.controller = controller; rules = OfficialCombatAssetCatalog.LoadDefault().Load(); }
            public void Observe()
            {
                FightBattleStepEvent step = controller.Checkpoint.Receipts.LastOrDefault()?.Event;
                if (step?.Movements == null) return;
                foreach (FightBattleMovementEvent movement in step.Movements)
                {
                    if (movement.Path == null || movement.Path.Count < 2) continue;
                    if (movement.MoveBehaviourId <= 0)
                    {
                        var unit = controller.Checkpoint.Units.Single(value => value.Id == movement.UnitId);
                        Require(movement.IsRetreat && rules.CreateRoleMovement(unit, true).MoveBehaviourId == 0,
                            "An actual configured host movement lost its original action: unit=" + movement.UnitId);
                        unconfiguredRetreats++;
                        continue;
                    }
                    Require(movement.HasFacing, "Actual movement missed authoritative facing before action-start dispatch.");
                    actions.Add(movement.MoveBehaviourId);
                    movements++;
                }
                if (step.Movements.Count > 0)
                {
                    var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(controller.Checkpoint));
                    Require(restored.Receipts.Last().Event.Movements[0].MoveBehaviourId == step.Movements[0].MoveBehaviourId,
                        "A real host checkpoint lost its movement identity.");
                    var originalFacing = controller.Checkpoint.Units.Select(unit => unit.HasFlipX + ":" + unit.FlipX);
                    Require(originalFacing.SequenceEqual(restored.Units.Select(unit => unit.HasFlipX + ":" + unit.FlipX))
                        && restored.Receipts.Last().Event.Movements[0].HasFacing == step.Movements[0].HasFacing
                        && restored.Receipts.Last().Event.Movements[0].EndFlipX == step.Movements[0].EndFlipX,
                        "Actual checkpoint roundtrip lost unit or per-action facing.");
                }
            }
            public void Verify(string host)
            {
                Require(movements > 0, host + " did not exercise an actual movement.");
                Debug.Log("[Grid motion] " + host + " actual movements=" + movements
                    + " action IDs=" + string.Join(",", actions.OrderBy(value => value))
                    + " unconfigured original retreats=" + unconfiguredRetreats);
            }
        }
    }
}
