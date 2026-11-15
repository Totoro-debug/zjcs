using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidatePortableFlameMovement =>
            Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_MOVEMENT") == "1";
        private bool flameMoveArmed, flameMoveSeen, flameMoveAdvanced, flameMoveArrived, flameMoveChildAfterArrival;
        private Vector3 flameMoveFrom, flameMoveTo, flameMoveEffectFrom;
        private MyPlayableFollowBone flameMoveFollow;
        private Transform flameMoveEffect;
        private int flameMoveInstance;

        private void ObservePortableFlameMovementStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidatePortableFlameMovement || !flameMoveArmed || flameMoveSeen) return;
            var step = checkpoint.Receipts.Last().Event;
            var movement = step.Movements.SingleOrDefault(value => value.UnitId == 1);
            if (movement == null) return;
            Require(step.ActorId == 1 && step.ActorRound == 2 && movement.Path.Count == 2
                && movement.Path[0].X == movement.Path[1].X && movement.Path[1].Y == movement.Path[0].Y + 1
                && movement.MoveSkillEntityId == 15300 && movement.MoveBehaviourId == 15301
                && movement.OrdinarySkillPresentation != null && entityViews.OwnsMovement(movement),
                "The moving fire ring did not use the original independent 15300/15301 ordinary action.");
            var casts = step.SkillCasts.Where(value => value.SkillEntityId == 10860).ToArray();
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 10860).ToArray();
            Require(casts.Length == 1 && casts[0].ActorId == 1 && casts[0].Slot < 0 && hits.Length == 1
                && hits[0].SourceId == 1 && hits[0].TargetId == 2 && hits[0].HitEntityId == 10861
                && hits[0].DamageEntityId == 10862 && hits[0].Damage < 0
                && hits[0].BeforeHp - hits[0].AfterHp == -hits[0].Damage,
                "The moving original ring lost its single creator-owned proximity damage.");
            Require(Math.Abs(casts[0].Delay - movement.Delay - entityViews.MovementDuration(movement)) < .0001f
                && hits[0].Delay >= casts[0].Delay && casts[0].Delay > 0f,
                "The original fire-ring child must wait for the confirmed movement arrival boundary.");
            flameMoveTo = entityViews.ToWorldPos(entityViews.GridToPos(
                new Zjcs.Official.World.NativeTerrain.Common.Pos2d(movement.Path[1].X, movement.Path[1].Y)));
            var player = checkpoint.Units.Single(value => value.Id == 1);
            Require(player.SkillState.Slots.Single().ReleasedTimes == 1
                && player.Statuses.Single(value => value.ClassId == 24210).InstanceId == flameMoveInstance,
                "Ordinary movement recast or replaced the original fire ring.");
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            SetPortableFlameMoveDistance(player, 0);
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                "Moving fire-ring damage or remaining duration did not round-trip.");
            flameMoveSeen = true;
            Debug.Log("[Portable flame movement] Original ordinary path and single creator-owned 10860/10861/10862 damage passed; child delay="
                + casts[0].Delay + "; movement action duration=" + movement.OrdinarySkillPresentation.ActionDuration + ".");
        }

        private void SetPortableFlameMoveDistance(FightBattleUnitState player, int distance)
        {
            foreach (var properties in new[] { player.Properties, player.BaseProperties })
            {
                if (properties == null) continue;
                var value = properties.FirstOrDefault(item => item.Id == catalog.PropertyType("MoveDist"));
                if (value == null) properties.Add(new FightPropertyValue { Id = catalog.PropertyType("MoveDist"), Value = distance });
                else value.Value = distance;
            }
            player.MoveDist = distance;
        }

        private bool ObservePortableFlameMovementFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameMovement || !flameRestoreFrame || flameMoveArrived) return false;
            var body = controller.TryGetUnitTransform(1);
            if (!flameMoveArmed && !controller.IsPresentingStep)
            {
                var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
                Require(player.SkillState.Round == 1 && player.Skills.Count == 0 && player.SkillState.Slots.Count == 0,
                    "The fire-ring approach must be armed after the original cast and before the second personal round.");
                var approach = catalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                    catalog.CalculateSkillProps(10600, 2, 20, 1)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                approach.LimitedTimes = 1;
                player.Skills.Add(approach); player.SkillState.Slots.Add(catalog.CreateSkillSlot(approach, 0));
                SetPortableFlameMoveDistance(player, 1);
                flameMoveInstance = player.Statuses.Single(value => value.ClassId == 24210).InstanceId;
                flameMoveFollow = UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().First(follow =>
                    follow.myTarget == body.gameObject && follow.IsFollowPos && !follow.IsStatic
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0));
                flameMoveEffect = (Transform)typeof(MyPlayableFollowBone)
                    .GetField("mTransform", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flameMoveFollow);
                Require(flameMoveEffect && flameMoveFollow.myAttach.GetBone(flameMoveFollow),
                    "The original dynamic fire-ring attachment was not initialized.");
                flameMoveFrom = body.position; flameMoveEffectFrom = flameMoveEffect.position;
                entityViews.CastStarted += cast => {
                    if (!flameMoveSeen || flameMoveChildAfterArrival || cast.ActorId != 1 || cast.SkillEntityId != 10860) return;
                    Require(body && Vector3.Distance(body.position, flameMoveTo) < .03f,
                        "The native fire-ring child started before the moving holder arrived.");
                    flameMoveChildAfterArrival = true;
                };
                flameMoveArmed = true; CaptureName = "portable-flame-before-move"; return true;
            }
            if (!flameMoveSeen) return false;
            Require(body && flameMoveFollow && flameMoveEffect && flameMoveFollow.myTarget == body.gameObject
                && PortableFlameViews().Single(view => view.Context.Owner == body.gameObject)
                    .GetComponent<MyEntity>().Id.EntityId == flameMoveInstance,
                "The moving native fire ring lost its original holder, instance or attachment.");
            var action = body.root.GetComponentsInChildren<MyFightActionSkillMove>().FirstOrDefault(value =>
                value.myEntity.Entity?.ClassId == 15301 && value.myApplyEntity
                && value.myApplyEntity.transform == body);
            float travelled = Vector3.Distance(body.position, flameMoveFrom);
            if (!flameMoveAdvanced && action && travelled > .02f
                && Vector3.Distance(body.position, flameMoveTo) > .02f
                && Vector3.Distance(flameMoveEffect.position, flameMoveEffectFrom) > .02f)
            {
                flameMoveAdvanced = true; CaptureName = "portable-flame-during-move"; return true;
            }
            if (flameMoveAdvanced && !action && Vector3.Distance(body.position, flameMoveTo) < .01f)
            {
                Require(Vector3.Distance(flameMoveEffect.position, flameMoveEffectFrom) > .02f
                    && flameMoveFollow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && flameMoveFollow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible),
                    "The original fire-ring particles did not arrive visibly with their holder.");
                flameMoveArrived = true; CaptureName = "portable-flame-after-move";
                Debug.Log("[Portable flame movement] Original 15300/15301 body interpolation, same live 24210 attachment and visible arrival passed.");
                return true;
            }
            return false;
        }

        private void VerifyPortableFlameMovement()
        {
            if (!ValidatePortableFlameMovement) return;
            Require(flameMoveArmed && flameMoveSeen && flameMoveAdvanced && flameMoveArrived && flameMoveChildAfterArrival,
                "The original fire-ring movement, body interpolation or visible arrival was not verified.");
            Debug.Log("[Portable flame movement] Native proximity child began after the holder reached the original landing cell.");
        }
    }
}
