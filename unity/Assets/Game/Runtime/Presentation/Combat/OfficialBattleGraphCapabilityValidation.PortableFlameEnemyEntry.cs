using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidatePortableFlameEnemyEntry =>
            Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_ENEMY_ENTRY") == "1";
        private bool flameEntryArmed, flameEntryMoved, flameEntryAdvanced, flameEntryArrived,
            flameEntryOrdinaryStarted, flameEntryChildStarted;
        private int flameEntryRound, flameEntryInstance, flameEntryEnemyAiType;
        private float flameEntryDelay;
        private Vector3 flameEntryFrom, flameEntryTo, flameEntryHolderPosition;
        private MyFightStatus flameEntryView;
        private MyPlayableFollowBone flameEntryFollow;

        private void BindPortableFlameEnemyEntry(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameEnemyEntry) return;
            Require(ValidatePortableFlameLife && !ValidatePortableFlameMovement && !ValidatePortableFlameReplacement
                && !PortableFlameColdRequested && firstPromotionFacingIndex == 2,
                "The enemy-entry diagnostic requires its own original fire-ring lifecycle facing up.");
            controller.StepCompleted += step => {
                if (step.Event.SkillCasts.Any(cast => cast.ActorId == 1 && cast.SkillEntityId == 11200))
                    SetPortableFlameEntryTurnDelay(controller, 100000f);
            };
        }

        private static void SetPortableFlameEntryTurnDelay(OfficialBattleRuntimeController controller, float seconds)
        {
            // Only delay new turns while the fixture observes/rebinds current graphs.
            typeof(OfficialBattleRuntimeController).GetField("remainingStepDelay", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, seconds);
        }

        private void ObservePortableFlameEnemyEntryStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidatePortableFlameEnemyEntry || !flameEntryArmed || flameEntryMoved) return;
            var step = checkpoint.Receipts.Last().Event;
            var movement = step.Movements.SingleOrDefault(value => value.UnitId == 2);
            if (movement == null) return;
            Require(step.ActorId == 2 && step.ActorRound == flameEntryRound && movement.Path.Count == 2
                && movement.Path[0].X == movement.Path[1].X && movement.Path[1].Y == movement.Path[0].Y - 1
                && movement.MoveSkillEntityId == 19810 && movement.MoveBehaviourId == 19811
                && movement.OrdinarySkillPresentation != null && entityViews.OwnsMovement(movement),
                "The enemy did not enter through its original independent ordinary movement.");
            var casts = step.SkillCasts.Where(cast => cast.SkillEntityId == 10860).ToArray();
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 10860).ToArray();
            Require(casts.Length == 1 && casts[0].ActorId == 1 && casts[0].Slot < 0
                && hits.Length == 1 && hits[0].SourceId == 1 && hits[0].TargetId == 2
                && hits[0].HitEntityId == 10861 && hits[0].DamageEntityId == 10862
                && hits[0].Damage < 0 && hits[0].BeforeHp - hits[0].AfterHp == -hits[0].Damage,
                "Enemy entry must trigger one original hit owned by the stationary ring's caster.");
            flameEntryDelay = movement.Delay + entityViews.MovementDuration(movement);
            Require(flameEntryDelay > 0f && Math.Abs(casts[0].Delay - flameEntryDelay) < .0001f,
                "Enemy-entry fire damage did not wait for actual path arrival.");
            flameEntryTo = entityViews.ToWorldPos(entityViews.GridToPos(
                new Zjcs.Official.World.NativeTerrain.Common.Pos2d(movement.Path[1].X, movement.Path[1].Y)));
            var holder = checkpoint.Units.Single(unit => unit.Id == 1);
            var status = holder.Statuses.Single(value => value.ClassId == 24210);
            var enemy = checkpoint.Units.Single(unit => unit.Id == 2);
            Require(status.InstanceId == flameEntryInstance && status.CreatorId == 1
                && status.SourceSkillRank == 2 && status.SourceSkillLevel == 20
                && enemy.SkillState.Slots.Single().ReleasedTimes == 1,
                "Enemy entry replaced the stationary ring or lost its creator/cultivation.");
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear(); SetPortableFlameMoveDistance(enemy, 0);
            enemy.AiType = flameEntryEnemyAiType;
            foreach (var ally in checkpoint.Units.Where(unit => flameCandidateIds.Contains(unit.Id))) ally.Targetable = true;
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(FightBattleCheckpointSerializer.Load(saved)).State;
            Require(FightBattleCheckpointSerializer.Save(restored) == saved,
                "Enemy-entry damage, status identity or remaining lifetime changed during restore.");
            flameEntryMoved = true;
            Debug.Log("[Portable flame enemy entry] Original enemy 19810/19811 path and one stationary-caster 10860/10861/10862 hit passed.");
        }

        private bool ObservePortableFlameEnemyEntryFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameEnemyEntry || !flameRestoreFrame || flameEntryArrived) return false;
            var body = controller.TryGetUnitTransform(2);
            var holderBody = controller.TryGetUnitTransform(1);
            if (!flameEntryArmed && !controller.IsPresentingStep)
            {
                var holder = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
                var enemy = controller.Checkpoint.Units.Single(unit => unit.Id == 2);
                Require(holder.SkillState.Round == 1 && enemy.SkillState.Round == 0
                    && enemy.X == holder.X && enemy.Y == holder.Y + 2,
                    "Enemy entry must be armed before the original enemy's first turn.");
                flameEntryEnemyAiType = enemy.AiType;
                var ordinary = catalog.CreateRoleMovement(enemy, false);
                Require(ordinary.MoveSkillEntityId == 19810 && ordinary.MoveBehaviourId == 19811,
                    "The original 3331 role must retain its own configured ordinary movement subtree.");
                enemy.Skills.Clear(); enemy.SkillState.Slots.Clear(); enemy.AiType = -1; enemy.AiPreviousTargetId = 1;
                var approach = catalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                    catalog.CalculateSkillProps(10600, 2, 20, 1)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                approach.LimitedTimes = 1; enemy.Skills.Add(approach);
                enemy.SkillState.Slots.Add(catalog.CreateSkillSlot(approach, 0)); SetPortableFlameMoveDistance(enemy, 1);
                // Other friends already border the enemy. Exclude them only from
                // this diagnostic's target selection so the original approach selects unit 1.
                foreach (var ally in controller.Checkpoint.Units.Where(unit => flameCandidateIds.Contains(unit.Id)))
                { Require(ally.Targetable, "A fixture ally was already untargetable."); ally.Targetable = false; }
                flameEntryRound = enemy.SkillState.Round + 1;
                flameEntryInstance = holder.Statuses.Single(value => value.ClassId == 24210).InstanceId;
                flameEntryView = PortableFlameViews().Single(view => view.Context.Owner == holderBody.gameObject);
                flameEntryFollow = UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().First(follow =>
                    follow.myTarget == holderBody.gameObject && follow.IsFollowPos && !follow.IsStatic
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0));
                flameEntryFrom = body.position; flameEntryHolderPosition = holderBody.position;
                entityViews.CastStarted += cast => {
                    if (cast.ActorId == 2 && cast.SkillEntityId == 19810)
                    {
                        Require(UnityEngine.Object.FindObjectsOfType<MyFightSkill>().Any(view =>
                            view.Component?.Entity?.ClassId == 19810 && view.Source && view.Source.transform == body),
                            "The moving original enemy lacks its independent native ordinary skill.");
                        flameEntryOrdinaryStarted = true;
                    }
                    if (!flameEntryMoved || flameEntryChildStarted || cast.ActorId != 1 || cast.SkillEntityId != 10860
                        || Math.Abs(cast.Delay - flameEntryDelay) > .0001f) return;
                    Require(body && Vector3.Distance(body.position, flameEntryTo) < .03f,
                        "The native fire child began before the enemy reached the adjacent cell.");
                    flameEntryChildStarted = true;
                };
                flameEntryArmed = true; SetPortableFlameEntryTurnDelay(controller, 0f);
                CaptureName = "portable-flame-entry-before-move"; return true;
            }
            if (!flameEntryMoved) return false;
            Require(body && holderBody && Vector3.Distance(holderBody.position, flameEntryHolderPosition) < .01f
                && flameEntryView && flameEntryFollow && flameEntryFollow.myTarget == holderBody.gameObject
                && flameEntryView.Context.Owner == holderBody.gameObject
                && flameEntryView.GetComponent<MyEntity>().Id.EntityId == flameEntryInstance,
                "Enemy movement displaced or replaced the stationary original fire ring.");
            var action = body.root.GetComponentsInChildren<MyFightActionSkillMove>().FirstOrDefault(value =>
                value.myEntity.Entity?.ClassId == 19811 && value.myApplyEntity && value.myApplyEntity.transform == body);
            if (!flameEntryAdvanced && action && Vector3.Distance(body.position, flameEntryFrom) > .02f
                && Vector3.Distance(body.position, flameEntryTo) > .02f)
            { flameEntryAdvanced = true; CaptureName = "portable-flame-entry-during-move"; return true; }
            if (flameEntryAdvanced && !action && Vector3.Distance(body.position, flameEntryTo) < .01f)
            {
                Require(flameEntryFollow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && flameEntryFollow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible),
                    "The stationary original fire ring stopped rendering during enemy entry.");
                flameEntryArrived = true; CaptureName = "portable-flame-entry-after-move";
                Debug.Log("[Portable flame enemy entry] Native enemy body interpolation, unchanged holder and same visible status passed.");
                return true;
            }
            return false;
        }

        private void VerifyPortableFlameEnemyEntry()
        {
            if (!ValidatePortableFlameEnemyEntry) return;
            Require(flameEntryArmed && flameEntryMoved && flameEntryAdvanced && flameEntryArrived
                && flameEntryOrdinaryStarted && flameEntryChildStarted,
                "The original enemy-entry movement and native fire child did not finish.");
            Debug.Log("[Portable flame enemy entry] Native caster-owned child began after the enemy reached the adjacent original cell.");
        }
    }
}
