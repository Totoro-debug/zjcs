using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void ExecuteBurningExplosions(FightBattleUnitState actor,
            FightBattleSkillDefinition parent, FightSkillLogicRuntime logic,
            FightBattleHitDefinition hit, FightBattleUnitState primary,
            FightBattleSkillCastPresentation parentCast, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            ref float lastDelay, ref float triggerDuration, ref float triggerActionDuration)
        {
            if (statusCatalog == null || logic == null) return;
            var positions = logic.CollectBurningExplosions(hit, primary, parentCast);
            foreach (var position in positions)
            {
                if (actor.Hp <= 0 || actor.Ended) break;
                int rank = Math.Max(1, parent.Rank), level = Math.Max(1, parent.Level), subRank = Math.Max(1, parent.SubRank);
                var child = statusCatalog.CreateSkill(13745, -1, parent.ItemId, rank, level, subRank,
                    ToPropertyList(statusCatalog.CalculateSkillProps(13745, rank, level, subRank)));
                child.RootEntityId = parent.RootEntityId > 0 ? parent.RootEntityId : parent.EntityId;
                child.StatusOrigin = FightBattleStatusOrigin.Resolve(parent, actor.Id).Copy();
                InheritBlessingScales(child, parent.BlessPropertyScales);
                var anchor = new FightBattleUnitState { X = position.X, Y = position.Y };
                var childHits = FightBattleHitCollector.CollectAll(child);
                var targets = new List<FightBattleUnitState>();
                if (childHits.Count > 0)
                    foreach (var occurrence in PlanHitPattern(actor, child, childHits[0], targets, anchor, null))
                        if (!targets.Exists(value => value.Id == occurrence.Target.Id)) targets.Add(occurrence.Target);
                int castStart = casts.Count, damageStart = damageEvents.Count, movementStart = movements.Count;
                var cast = new FightBattleSkillCastEvent
                {
                    ActorId = actor.Id, Slot = -1, SkillEntityId = child.EntityId, SkillItemId = child.ItemId,
                    Presentation = FightBattleSkillCastPresentation.Capture(actor, child, targets, anchor)
                };
                foreach (var target in targets) if (!cast.TargetIds.Contains(target.Id)) cast.TargetIds.Add(target.Id);
                checkpoint.Statistics.RecordCast(actor.Id, child.EntityId, child.ItemId);
                casts.Add(cast);
                ExecuteSkill(actor, child, targets, casts, damageEvents, movements,
                    out float delay, out float nestedDuration, out float nestedActionDuration,
                    anchor, presentationCast: cast);
                CompleteCastPresentation(cast, child, delay);
                float offset = Math.Max(0f, hit.Delay);
                OffsetSkillEvents(casts, castStart, damageEvents, damageStart, movements, movementStart, offset);
                lastDelay = Math.Max(lastDelay, offset + delay);
                triggerDuration = Math.Max(triggerDuration, offset + Math.Max(ResolveDuration(child, delay), nestedDuration));
                float actionDuration = Math.Max(ResolveActionDuration(child, delay), nestedActionDuration);
                if (actionDuration >= 0f) triggerActionDuration = Math.Max(triggerActionDuration, offset + actionDuration);
            }
        }
    }
}
