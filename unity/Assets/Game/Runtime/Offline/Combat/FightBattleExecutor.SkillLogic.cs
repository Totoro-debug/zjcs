using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void ExecuteLogicChildSkills(FightBattleUnitState actor,
            FightBattleSkillDefinition parent, FightSkillLogicRuntime logic,
            FightBattleHitDefinition hit, FightBattleUnitState primary,
            FightBattleSkillCastPresentation parentCast, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            ref float lastDelay, ref float triggerDuration, ref float triggerActionDuration)
        {
            if (statusCatalog == null || logic == null) return;
            var requests = logic.CollectChildSkills(hit, primary, parentCast);
            ExecuteLogicChildRequests(actor, parent, requests, casts, damageEvents, movements,
                ref lastDelay, ref triggerDuration, ref triggerActionDuration);
        }

        private void ExecuteLogicChildRequests(FightBattleUnitState actor,
            FightBattleSkillDefinition parent, List<FightSkillLogicRuntime.ChildSkillRequest> requests,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay,
            ref float triggerDuration, ref float triggerActionDuration)
        {
            if (statusCatalog == null || requests == null) return;
            foreach (var request in requests)
            {
                if (actor.Hp <= 0 || actor.Ended) break;
                int rank = Math.Max(1, parent.Rank), level = Math.Max(1, parent.Level), subRank = Math.Max(1, parent.SubRank);
                var child = statusCatalog.CreateSkill(request.SkillId, -1, parent.ItemId, rank, level, subRank,
                    ToPropertyList(statusCatalog.CalculateSkillProps(request.SkillId, rank, level, subRank)));
                child.HasCustomCure = request.HasCustomCure; child.CustomCure = request.CustomCure;
                child.RootEntityId = parent.RootEntityId > 0 ? parent.RootEntityId : parent.EntityId;
                child.StatusOrigin = FightBattleStatusOrigin.Resolve(parent, actor.Id).Copy();
                InheritBlessingScales(child, parent.BlessPropertyScales);
                var anchor = request.Target ?? new FightBattleUnitState { X = request.Position.X, Y = request.Position.Y };
                var childHits = FightBattleHitCollector.CollectAll(child);
                var targets = new List<FightBattleUnitState>();
                if (request.Target != null) targets.Add(request.Target);
                else if (childHits.Count > 0)
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
                float offset = request.Delay;
                OffsetSkillEvents(casts, castStart, damageEvents, damageStart, movements, movementStart, offset);
                lastDelay = Math.Max(lastDelay, offset + delay);
                triggerDuration = Math.Max(triggerDuration, offset + Math.Max(ResolveDuration(child, delay), nestedDuration));
                float actionDuration = Math.Max(ResolveActionDuration(child, delay), nestedActionDuration);
                if (actionDuration >= 0f) triggerActionDuration = Math.Max(triggerActionDuration, offset + actionDuration);
            }
        }
    }
}
