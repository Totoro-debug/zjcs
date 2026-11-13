using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        public sealed class SummonColdSceneSnapshot
        {
            internal Transform[] Bodies;
            internal MyFightStatus[] Views;
            public string Checkpoint { get; internal set; }
        }

        public bool ReadyForSummonColdScene(OfficialBattleRuntimeController controller) =>
            ValidateSummonLife && !summonLifeComplete && !finisherScheduled && captured
            && summonLives.Count == firstPromotionExpectedSummonCount
            && summonLifeBorn.Count == summonLives.Count && !controller.IsPresentingStep
            && summonLives.Keys.All(id => !controller.Checkpoint.Units.Single(unit => unit.Id == id).Ended
                && !HasOriginalSummonSkillView(id));

        public SummonColdSceneSnapshot CaptureSummonColdScene(OfficialBattleRuntimeController controller)
        {
            Require(ReadyForSummonColdScene(controller), "Summon scene checkpoint is not between live original turns.");
            var bodies = summonLives.Keys.Select(controller.TryGetUnitTransform).ToArray();
            Require(bodies.All(value => value), "An original summon body is missing before scene destruction.");
            var views = bodies.SelectMany(value => value.GetComponent<MyFightStatusAgent>().MyStatuses).ToArray();
            Require(views.Length >= bodies.Length, "Original summon lifespan views are absent before scene destruction.");
            return new SummonColdSceneSnapshot { Bodies = bodies, Views = views,
                Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint) };
        }

        public void BindSummonColdScene(OfficialBattleRuntimeController controller, SummonColdSceneSnapshot saved)
        {
            Require(saved.Bodies.All(value => !value) && saved.Views.All(value => !value),
                "Old summon bodies or original status views survived complete host destruction.");
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Summon scene restoration changed the disk checkpoint or replayed a receipt.");
            foreach (var pair in summonLives)
            {
                var unit = controller.Checkpoint.Units.Single(value => value.Id == pair.Key);
                var life = unit.Statuses.Single(value => value.ClassId == pair.Value.StatusId);
                var body = controller.TryGetUnitTransform(unit.Id);
                var agent = body ? body.GetComponent<MyFightStatusAgent>() : null;
                var view = agent ? agent.MyStatuses.SingleOrDefault(value => value
                    && value.GetComponent<MyEntity>().Id.EntityId == life.InstanceId) : null;
                Require(body && view && view.Context.Owner == body.gameObject
                    && !view.GetComponent<MyEntity>().myNewEntity
                    && life.StartRound == pair.Value.StartRound && life.CurDurationRound == pair.Value.Duration
                    && unit.SkillState.Round < pair.Value.ExpireRound && !unit.Ended && unit.Hp > 0,
                    "Restored summon lost its original owner, lifespan, identity or non-fresh view: " + unit.Id);
            }
            BindPresentation(controller);
        }

        public bool SummonColdSceneModelsVisible(OfficialBattleRuntimeController controller)
        {
            foreach (int id in summonLives.Keys)
            {
                var body = controller.TryGetUnitTransform(id);
                var renderer = body ? body.GetComponentInChildren<BattleNativeMonsterRenderer>() : null;
                if (!renderer || renderer.Animation == "generate_01") return false;
                bool visible = false;
                foreach (var meshRenderer in body.GetComponentsInChildren<MeshRenderer>())
                {
                    var mesh = meshRenderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (!mesh || mesh.vertexCount == 0) continue;
                    Require(mesh.vertices.All(v => FiniteAirCombo(v.x) && FiniteAirCombo(v.y) && FiniteAirCombo(v.z)),
                        "Restored original summon geometry contains a non-finite vertex: " + id);
                    visible |= meshRenderer.enabled && meshRenderer.isVisible && !meshRenderer.forceRenderingOff;
                }
                if (!visible) return false;
            }
            return true;
        }
    }
}
