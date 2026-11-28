using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class BlockedCollisionContext
        {
            public FightBattleSkillDefinition Skill;
            public int TargetId;
        }

        private BlockedCollisionContext blockedCollision;

        private bool TryApplyBlockedKnockback(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition parent, FightBattleHitDefinition hit, FightBattleSkillMove move,
            List<FightBattleMovementEvent> movements, int stepX, int stepY, float hitDelay)
        {
            bool windBlade = hit.EntityId == 10641 && hit.DamageId == 10642 && move.MoveBehaviourId == 10643;
            bool unstableAura = hit.EntityId == 10851 && hit.DamageId == 10852 && move.MoveBehaviourId == 10853;
            if (move.Distance < 1
                || move.OffsetX != 0 || move.OffsetY != 0 || statusCatalog == null || checkpoint.Grid == null
                || movementStartContext == null
                || !statusCatalog.UsesStandardKnockbackCollision(move.MoveBehaviourId)) return false;
            bool diagonal = stepX != 0 && stepY != 0;
            var start = new FightGridPoint(target.X, target.Y);
            var path = FightKnockbackCollision.Trace(checkpoint.Grid, units, actor, target,
                stepX, stepY, move.Distance, out FightKnockbackCollision collision);
            if (collision == null)
            {
                if (!diagonal && move.Distance == 1) return false;
                int index = movements.Count;
                if (ApplyMovementPath(target, path, path.Count - 1, movements))
                    ApplyMovementAction(movements, index, move, parent, originDelay: hitDelay);
                return true;
            }
            var movementObserver = ImpactGate as IFightBattleBlockedMovementObserver;
            var impactObserver = ImpactGate as IFightBattleBlockedImpactObserver;
            bool observesContact = movementObserver != null && movementObserver.ObservesBlockedMovements && impactObserver != null;
            if (move.Distance == 1 && !observesContact && (!unstableAura || ImpactGate != null)) return diagonal;

            int movementIndex = movements.Count;
            FightBattleMovementEvent movement;
            if (path.Count > 1)
            {
                ApplyMovementPath(target, path, path.Count - 1, movements);
                movement = movements[movementIndex];
            }
            else
            {
                movement = new FightBattleMovementEvent { UnitId = target.Id,
                    HasDirection = true, Direction = target.Direction };
                movement.Path.Add(start); movement.Path.Add(start);
                movements.Add(movement);
            }
            var context = movementStartContext;
            void ResolveCollision()
            {
                foreach (FightBattleUnitState victim in collision.Victims)
                {
                    if (actor.Hp <= 0 || victim.Hp <= 0 || victim.Ended) continue;
                    ExecuteBlockedCollisionChild(actor, victim, parent, movement, impactObserver, context);
                }
            }
            var sourceCast = gridViewCast?.SkillEntityId == parent.EntityId ? gridViewCast : null;
            bool collisionDeferred = windBlade && sourceCast != null
                && ImpactGate is IFightBattleDeferredBlockedImpactObserver deferred
                && deferred.TryDeferBlockedContact(sourceCast, movement, ResolveCollision);
            ApplyMovementAction(movements, movementIndex, move, parent, originDelay: hitDelay,
                blockedPosition: collision.BlockedPosition);
            if (collisionDeferred) return true;
            if (observesContact) movementObserver.AwaitBlockedContact(movement);
            ResolveCollision();
            return true;
        }

        private void ExecuteBlockedCollisionChild(FightBattleUnitState actor, FightBattleUnitState victim,
            FightBattleSkillDefinition parent, FightBattleMovementEvent movement,
            IFightBattleBlockedImpactObserver impactObserver, MovementStartContext context)
        {
            int rank = Math.Max(1, parent.Rank), level = Math.Max(1, parent.Level), subRank = Math.Max(1, parent.SubRank);
            var skill = statusCatalog.CreateSkill(10100, -1, 0, rank, level, subRank,
                ToPropertyList(statusCatalog.CalculateSkillProps(10100, rank, level, subRank)));
            skill.StatusOrigin = FightBattleStatusOrigin.Resolve(parent, actor.Id).Copy();
            var targets = new List<FightBattleUnitState> { victim };
            var cast = new FightBattleSkillCastEvent { ActorId = actor.Id, Slot = -1,
                SkillEntityId = skill.EntityId, SkillItemId = skill.ItemId,
                Presentation = FightBattleSkillCastPresentation.Capture(actor, skill, targets, victim) };
            cast.TargetIds.Add(victim.Id);
            checkpoint.Statistics.RecordCast(actor.Id, skill.EntityId, skill.ItemId);
            context.Casts.Add(cast);
            impactObserver?.BindBlockedImpact(movement, cast, victim.Id);
            var previous = blockedCollision;
            blockedCollision = new BlockedCollisionContext { Skill = skill, TargetId = victim.Id };
            try
            {
                ExecuteSkill(actor, skill, targets, context.Casts, context.DamageEvents, context.Movements,
                    out float delay, victim, presentationCast: cast);
                CompleteCastPresentation(cast, skill, delay);
            }
            finally { blockedCollision = previous; }
        }

        private bool IsBlockedCollisionHit(FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            FightBattleUnitState target) => blockedCollision != null && ReferenceEquals(skill, blockedCollision.Skill)
            && target.Id == blockedCollision.TargetId && hit.EntityId == 10101 && hit.DamageId == 10006;

        private long CurrentAttack(FightBattleUnitState actor)
        {
            var properties = ToPropertyDictionary(actor.Properties);
            return properties.TryGetValue(formula.PropertyId("Attack"), out long value) ? value : 0L;
        }
    }
}
