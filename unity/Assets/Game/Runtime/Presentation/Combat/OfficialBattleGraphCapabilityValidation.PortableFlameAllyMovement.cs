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
        private bool ValidatePortableFlameAllyMovement =>
            Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_ALLY_MOVEMENT") == "1";
        private bool flameAllyArmed, flameAllyMoved, flameAllyAdvanced, flameAllyArrived, flameAllyChildStarted;
        private int flameAllyInstance, flameAllyMoveRound;
        private float flameAllyChildDelay;
        private Vector3 flameAllyFrom, flameAllyTo, flameAllyEffectFrom;
        private MyPlayableFollowBone flameAllyFollow;
        private Transform flameAllyEffect;

        private void BindPortableFlameAllyMovement(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameAllyMovement) return;
            controller.StepCompleted += step => {
                if (step.Event.SkillCasts.Any(cast => cast.ActorId == flameReplacementCaster && cast.SkillEntityId == 11200))
                    SetPortableFlameAllyTurnDelay(controller, 100000f);
            };
        }

        private static void SetPortableFlameAllyTurnDelay(OfficialBattleRuntimeController controller, float seconds)
        {
            // Hold new turns while the diagnostic checks/rebinds native views;
            // the current original graphs and their presentation clocks keep running.
            typeof(OfficialBattleRuntimeController).GetField("remainingStepDelay", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, seconds);
        }

        private void ObservePortableFlameAllyMovementStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidatePortableFlameAllyMovement || !flameAllyArmed || flameAllyMoved) return;
            var step = checkpoint.Receipts.Last().Event;
            var movement = step.Movements.SingleOrDefault(value => value.UnitId == 1);
            if (movement == null) return;
            Require(step.ActorId == 1 && step.ActorRound == flameAllyMoveRound && movement.Path.Count == 2
                && movement.Path[0].X == movement.Path[1].X && movement.Path[1].Y == movement.Path[0].Y + 1
                && movement.MoveSkillEntityId == 15300 && movement.MoveBehaviourId == 15301
                && entityViews.OwnsMovement(movement), "The ally-held flame did not use the original ordinary approach.");
            var casts = step.SkillCasts.Where(value => value.SkillEntityId == 10860).ToArray();
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 10860).ToArray();
            Require(casts.Length == 1 && casts[0].ActorId == flameReplacementCaster && casts[0].Slot < 0
                && hits.Length == 1 && hits[0].SourceId == flameReplacementCaster && hits[0].TargetId == 2
                && hits[0].HitEntityId == 10861 && hits[0].DamageEntityId == 10862 && hits[0].Damage < 0
                && hits[0].BeforeHp - hits[0].AfterHp == -hits[0].Damage,
                "The moving ally must trigger one original hit owned by the other caster.");
            flameAllyChildDelay = movement.Delay + entityViews.MovementDuration(movement);
            Require(flameAllyChildDelay > 0f && Math.Abs(casts[0].Delay - flameAllyChildDelay) < .0001f,
                "The ally-held flame did not wait for actual path arrival.");
            flameAllyTo = entityViews.ToWorldPos(entityViews.GridToPos(
                new Zjcs.Official.World.NativeTerrain.Common.Pos2d(movement.Path[1].X, movement.Path[1].Y)));
            var holder = checkpoint.Units.Single(unit => unit.Id == 1);
            var status = holder.Statuses.Single(value => value.ClassId == 24210);
            Require(holder.SkillState.Slots.Single().ReleasedTimes == 1 && status.InstanceId == flameAllyInstance
                && status.CreatorId == flameReplacementCaster && status.SourceSkillRank == 3 && status.SourceSkillLevel == 30,
                "Movement replaced the ally's flame or inherited the holder's active skill cultivation.");
            holder.Skills.Clear(); holder.SkillState.Slots.Clear(); SetPortableFlameMoveDistance(holder, 0);
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(FightBattleCheckpointSerializer.Load(saved)).State;
            Require(FightBattleCheckpointSerializer.Save(restored) == saved,
                "Ally movement changed damage, origin or remaining lifetime during restore.");
            flameAllyMoved = true;
            Debug.Log("[Portable flame ally movement] Original 15300/15301 approach and one other-caster 10860/10861/10862 hit passed.");
        }

        private bool ObservePortableFlameAllyMovementFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameAllyMovement || !flameReplacementRestoreFrame || flameAllyArrived) return false;
            var body = controller.TryGetUnitTransform(1);
            if (!flameAllyArmed && !controller.IsPresentingStep)
            {
                var holder = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
                Require(holder.Skills.Count == 0 && holder.SkillState.Slots.Count == 0
                    && holder.SkillState.Round < flameHolders[1].ExpiryRound - 1,
                    "The ally-held flame needs one remaining personal turn for its original approach.");
                var approach = catalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                    catalog.CalculateSkillProps(10600, 2, 20, 1)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                approach.LimitedTimes = 1; holder.Skills.Add(approach);
                holder.SkillState.Slots.Add(catalog.CreateSkillSlot(approach, 0)); SetPortableFlameMoveDistance(holder, 1);
                flameAllyMoveRound = holder.SkillState.Round + 1;
                flameAllyInstance = holder.Statuses.Single(value => value.ClassId == 24210).InstanceId;
                flameAllyFollow = UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().First(follow =>
                    follow.myTarget == body.gameObject && follow.IsFollowPos && !follow.IsStatic
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0));
                flameAllyEffect = (Transform)typeof(MyPlayableFollowBone)
                    .GetField("mTransform", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flameAllyFollow);
                Require(flameAllyEffect && flameAllyFollow.myAttach.GetBone(flameAllyFollow),
                    "The ally's original dynamic fire-ring attachment was not initialized.");
                flameAllyFrom = body.position; flameAllyEffectFrom = flameAllyEffect.position;
                entityViews.CastStarted += cast => {
                    if (!flameAllyMoved || flameAllyChildStarted || cast.ActorId != flameReplacementCaster
                        || cast.SkillEntityId != 10860 || Math.Abs(cast.Delay - flameAllyChildDelay) > .0001f) return;
                    Require(body && Vector3.Distance(body.position, flameAllyTo) < .03f,
                        "The other caster's native fire child began before its ally arrived.");
                    flameAllyChildStarted = true;
                };
                flameAllyArmed = true; SetPortableFlameAllyTurnDelay(controller, 0f);
                CaptureName = "portable-flame-ally-before-move"; return true;
            }
            if (!flameAllyMoved) return false;
            Require(body && flameAllyFollow && flameAllyEffect && flameAllyFollow.myTarget == body.gameObject
                && PortableFlameViews().Single(view => view.Context.Owner == body.gameObject)
                    .GetComponent<MyEntity>().Id.EntityId == flameAllyInstance,
                "The moving ally lost its same original fire-ring instance or follow-bone target.");
            var action = body.root.GetComponentsInChildren<MyFightActionSkillMove>().FirstOrDefault(value =>
                value.myEntity.Entity?.ClassId == 15301 && value.myApplyEntity && value.myApplyEntity.transform == body);
            if (!flameAllyAdvanced && action && Vector3.Distance(body.position, flameAllyFrom) > .02f
                && Vector3.Distance(body.position, flameAllyTo) > .02f
                && Vector3.Distance(flameAllyEffect.position, flameAllyEffectFrom) > .02f)
            {
                flameAllyAdvanced = true; CaptureName = "portable-flame-ally-during-move"; return true;
            }
            if (flameAllyAdvanced && !action && Vector3.Distance(body.position, flameAllyTo) < .01f)
            {
                Require(Vector3.Distance(flameAllyEffect.position, flameAllyEffectFrom) > .02f
                    && flameAllyFollow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && flameAllyFollow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible),
                    "The same other-caster fire ring did not arrive visibly with its ally.");
                flameAllyArrived = true; CaptureName = "portable-flame-ally-after-move";
                Debug.Log("[Portable flame ally movement] Same native status, body interpolation and visible follow-bone arrival passed.");
                return true;
            }
            return false;
        }

        private void VerifyPortableFlameAllyMovement()
        {
            if (!ValidatePortableFlameAllyMovement) return;
            Require(flameAllyArmed && flameAllyMoved && flameAllyAdvanced && flameAllyArrived && flameAllyChildStarted,
                "The ally-held fire-ring movement and other-caster native child did not finish.");
            Debug.Log("[Portable flame ally movement] Other-caster native child began after the ally reached the original landing cell.");
        }
    }
}
