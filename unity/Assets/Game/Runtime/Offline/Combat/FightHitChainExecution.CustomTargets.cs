using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightHitChainExecution
    {
        private readonly Dictionary<FightBattleHitDefinition, List<FightBattleHitDefinition>> customChildren
            = new Dictionary<FightBattleHitDefinition, List<FightBattleHitDefinition>>();

        private FightHitTargetSelection SelectCustom(Step step, FightHitTargetSelectionContext context,
            FightBattleHitTiming.Flight flight)
        {
            // These three missing server resolvers place one summon within the
            // caster's authored four-cell scope, near the ordinary AI target.
            if (step.Hit.EntityId != 12063 && step.Hit.EntityId != 13303 && step.Hit.EntityId != 13673)
                throw new NotSupportedException("Unconfirmed custom target placement: " + step.Hit.EntityId);
            var center = new FightGridPoint(context.Source.X, context.Source.Y);
            var offsets = FightBattleTargeting.TransformScope(step.Hit.Scope,
                step.Hit.ScopeTransform, context.Source, step.Cast);
            customChildren.TryGetValue(step.Original, out var children);
            FightBattleUnitState target = null;
            foreach (var candidate in context.Targets)
                if (FightBattleTargeting.IsEligibleSkillTarget(context.TargetingSource ?? context.Source,
                    candidate, context.Skill)) { target = candidate; break; }
            if (target != null && children != null && offsets != null)
            {
                if (context.CanPlaceSummon == null)
                    throw new InvalidOperationException("Custom summon placement requires the battle occupancy check.");
                int best = -1, bestTarget = int.MaxValue, bestSource = int.MaxValue;
                for (int index = 0; index < offsets.Count; index++)
                {
                    var cell = center + offsets[index];
                    if (!CanPlaceChildren(children, context, cell, 0)) continue;
                    int targetDistance = BodyDistance(cell, target);
                    int sourceDistance = BodyDistance(cell, context.Source);
                    if (targetDistance > bestTarget || targetDistance == bestTarget && sourceDistance >= bestSource) continue;
                    best = index; bestTarget = targetDistance; bestSource = sourceDistance;
                }
                bool fallback = best < 0;
                var selected = fallback ? center : center + offsets[best];
                // Only the caster is ignored on the confirmed last-resort cell;
                // other occupants and all map/body checks still apply.
                if (!fallback || CanPlaceChildren(children, context, selected, context.Source.Id))
                {
                    float scopeDelay = !fallback && step.Hit.ScopeEntries != null && best < step.Hit.ScopeEntries.Count
                        ? step.Hit.ScopeEntries[best].Delay : 0f;
                    float trigger = FightBattleHitTiming.EffectDelay(step.Hit, step.Cast, center, flight) + scopeDelay;
                    foreach (var child in children)
                        pending.Add(new Step { Original = child, Hit = child.AtDelay(trigger + child.CustomLinkDelay),
                            Cast = step.Cast.WithHitOrigin(center), CustomTarget = target, CustomCell = selected,
                            SummonOnSourceFallback = fallback, Order = ++serial });
                }
            }
            return new FightHitTargetSelection(new List<FightBattleUnitState>(),
                new List<FightHitOccurrence>(), new List<FightGridPoint>(), new List<FightGridPoint> { center });
        }

        private static bool CanPlaceChildren(List<FightBattleHitDefinition> children,
            FightHitTargetSelectionContext context, FightGridPoint cell, int ignoredUnitId)
        {
            foreach (var child in children)
                if (!context.CanPlaceSummon(child, cell, ignoredUnitId)) return false;
            return true;
        }

        private static int BodyDistance(FightGridPoint cell, FightBattleUnitState unit)
        {
            int distance = Math.Abs(cell.X - unit.X) + Math.Abs(cell.Y - unit.Y);
            if (unit.BodyOffsets != null)
                foreach (var offset in unit.BodyOffsets)
                    distance = Math.Min(distance, Math.Abs(cell.X - unit.X - offset.X) + Math.Abs(cell.Y - unit.Y - offset.Y));
            return distance;
        }
    }
}
