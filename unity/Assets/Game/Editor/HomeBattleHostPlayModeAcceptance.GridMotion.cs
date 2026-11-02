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
                Debug.Log("[Grid motion] original forward/back identities, delay, pause, independent non-linear reference, cell boundary and final grid passed.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(model);
            }
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
                    actions.Add(movement.MoveBehaviourId);
                    movements++;
                }
                if (step.Movements.Count > 0)
                {
                    var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(controller.Checkpoint));
                    Require(restored.Receipts.Last().Event.Movements[0].MoveBehaviourId == step.Movements[0].MoveBehaviourId,
                        "A real host checkpoint lost its movement identity.");
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
