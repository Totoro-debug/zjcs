using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // Owns one cast's traversal, shared branch history and hit origins. The
    // executor still settles each selected hit through its existing damage path.
    internal sealed partial class FightHitChainExecution
    {
        internal sealed class History
        {
            internal readonly HashSet<int> Visited = new HashSet<int>();
        }

        internal sealed class Step
        {
            internal FightBattleHitDefinition Original, Hit;
            internal FightBattleSkillCastPresentation Cast;
            internal History History;
            internal FightBattleUnitState CustomTarget;
            internal FightGridPoint CustomCell;
            internal int PreviousTargetId, PickedTargetId, Order;
        }

        private readonly FightHitTargetSelectors selectors = new FightHitTargetSelectors();
        private readonly Dictionary<FightBattleHitDefinition, List<FightBattleHitDefinition>> children
            = new Dictionary<FightBattleHitDefinition, List<FightBattleHitDefinition>>();
        private readonly List<Step> pending = new List<Step>();
        private readonly FightBattleSkillCastPresentation cast;
        private readonly bool ordered;
        private int serial;

        internal FightHitChainExecution(IReadOnlyList<FightBattleHitDefinition> hits,
            FightBattleSkillCastPresentation cast, bool enableChains)
        {
            this.cast = cast ?? throw new ArgumentNullException(nameof(cast));
            if (hits == null) throw new ArgumentNullException(nameof(hits));
            foreach (var hit in hits)
                if (enableChains && (hit.ChainConfiguration != null || hit.IsCustomTarget)) ordered = true;
            foreach (var hit in hits)
            {
                if (ordered && hit.PreviousCustomHit != null)
                {
                    if (!customChildren.TryGetValue(hit.PreviousCustomHit, out var list))
                        customChildren.Add(hit.PreviousCustomHit, list = new List<FightBattleHitDefinition>());
                    list.Add(hit);
                    continue;
                }
                if (ordered && hit.PreviousChainHit != null)
                {
                    if (!children.TryGetValue(hit.PreviousChainHit, out var list))
                        children.Add(hit.PreviousChainHit, list = new List<FightBattleHitDefinition>());
                    list.Add(hit);
                    continue;
                }
                var origin = new FightGridPoint(cast.HasEntityPosition ? cast.EntityX : cast.SourceX,
                    cast.HasEntityPosition ? cast.EntityY : cast.SourceY);
                pending.Add(new Step { Original = hit, Hit = hit,
                    Cast = ordered && hit.ChainConfiguration != null ? cast.WithHitOrigin(origin) : cast,
                    Order = ++serial });
            }
        }

        internal bool TryTake(out Step step)
        {
            if (pending.Count == 0) { step = null; return false; }
            int next = 0;
            if (ordered)
                for (int index = 1; index < pending.Count; index++)
                    if (pending[index].Hit.Delay < pending[next].Hit.Delay
                        || pending[index].Hit.Delay == pending[next].Hit.Delay
                            && pending[index].Order < pending[next].Order) next = index;
            step = pending[next]; pending.RemoveAt(next);
            return true;
        }

        internal FightHitTargetSelection Select(Step step, FightHitTargetSelectionContext context,
            FightBattleHitTiming.Flight flight)
        {
            context.Hit = step.Hit;
            context.Cast = step.Cast;
            if (step.CustomTarget != null)
            {
                context.Primary = new FightBattleUnitState { X = step.CustomCell.X, Y = step.CustomCell.Y };
                context.Targets = new List<FightBattleUnitState> { step.CustomTarget };
            }
            if (step.Hit.IsCustomTarget) return SelectCustom(step, context, flight);
            var selection = step.History == null
                ? selectors.For(step.Hit).Select(context) : SelectNext(step, context);
            if (selection == null || !ordered || step.Hit.ChainConfiguration == null) return selection;
            selection = PreserveSingleScopeDelay(step.Hit, context, selection);
            ScheduleChildren(step, context, selection, flight);
            return selection;
        }

        private FightHitTargetSelection SelectNext(Step step, FightHitTargetSelectionContext context)
        {
            var info = step.Hit.ChainConfiguration;
            var origin = new FightGridPoint(step.Cast.SourceX, step.Cast.SourceY);
            var range = new HashSet<FightGridPoint>();
            if (context.Skill.Range != null)
                foreach (var offset in context.Skill.Range)
                    range.Add(new FightGridPoint(origin.X + offset.X, origin.Y + offset.Y));
            FightBattleUnitState picked = null;
            FightGridPoint cell = default;
            long bestDistance = long.MaxValue;
            bool bestUnvisited = false;
            foreach (var unit in context.Units)
            {
                if (!FightBattleTargeting.IsEligibleSkillTarget(context.TargetingSource ?? context.Source,
                    unit, context.Skill) || !info.Repeat && step.History.Visited.Contains(unit.Id)
                    || !info.Back && unit.Id == step.PreviousTargetId) continue;
                bool unvisited = !step.History.Visited.Contains(unit.Id);
                var cells = new List<FightGridPoint> { new FightGridPoint(unit.X, unit.Y) };
                if (unit.BodyOffsets != null)
                    foreach (var offset in unit.BodyOffsets)
                        cells.Add(new FightGridPoint(unit.X + offset.X, unit.Y + offset.Y));
                foreach (var candidate in cells)
                {
                    if (!range.Contains(candidate)) continue;
                    long dx = (long)candidate.X - origin.X, dy = (long)candidate.Y - origin.Y;
                    long distance = dx * dx + dy * dy;
                    bool preferNew = info.PreferUnvisited && unvisited != bestUnvisited;
                    if (picked != null && (preferNew ? !unvisited
                        : distance > bestDistance || distance == bestDistance && unit.Id >= picked.Id)) continue;
                    picked = unit; cell = candidate; bestDistance = distance; bestUnvisited = unvisited;
                }
            }
            if (picked == null) return null;
            step.PickedTargetId = picked.Id;
            // A body cell is the hit entity's origin, independently of the role's centre.
            var occurrences = FightBattleHitPatterns.For(step.Hit).Plan(new FightHitPatternContext {
                Source = context.TargetingSource ?? context.Source, Units = context.Units,
                Skill = context.Skill, Hit = step.Hit, Targets = new List<FightBattleUnitState> { picked },
                Primary = new FightBattleUnitState { X = cell.X, Y = cell.Y }, Cast = step.Cast,
                Random = context.Random, SelectedByRandomParent = true
            });
            var targets = new List<FightBattleUnitState>();
            foreach (var occurrence in occurrences) targets.Add(occurrence.Target);
            return new FightHitTargetSelection(targets, occurrences,
                new List<FightGridPoint> { cell }, new List<FightGridPoint> { cell });
        }

        private static FightHitTargetSelection PreserveSingleScopeDelay(FightBattleHitDefinition hit,
            FightHitTargetSelectionContext context, FightHitTargetSelection selection)
        {
            if (hit.Scope == null || hit.Scope.Count != 1 || hit.ScopeEntries == null
                || hit.ScopeEntries.Count != 1) return selection;
            var offset = FightBattleTargeting.TransformScope(hit.Scope, hit.ScopeTransform,
                context.Source, context.Cast)[0];
            var occurrences = new List<FightHitOccurrence>();
            for (int index = 0; index < selection.Targets.Count; index++)
            {
                var previous = selection.Occurrences == null ? null : selection.Occurrences[index];
                int centerIndex = previous?.EffectCenterIndex ?? 0;
                var center = selection.EffectCenters[centerIndex];
                occurrences.Add(new FightHitOccurrence(selection.Targets[index], true,
                    new FightGridPoint(center.X + offset.X, center.Y + offset.Y),
                    hit.ScopeEntries[0].Delay, centerIndex));
            }
            return new FightHitTargetSelection(selection.Targets, occurrences,
                selection.SelectedGridPositions, selection.EffectCenters);
        }

        private void ScheduleChildren(Step step, FightHitTargetSelectionContext context,
            FightHitTargetSelection selection, FightBattleHitTiming.Flight flight)
        {
            children.TryGetValue(step.Original, out var next);
            for (int centerIndex = 0; centerIndex < selection.EffectCenters.Count; centerIndex++)
            {
                var history = step.History ?? new History();
                bool hitAny = false;
                float scopeDelay = 0f;
                foreach (var occurrence in selection.Occurrences)
                    if (occurrence.EffectCenterIndex == centerIndex)
                    {
                        hitAny = true;
                        history.Visited.Add(occurrence.Target.Id);
                        scopeDelay = Math.Max(scopeDelay, occurrence.Delay);
                    }
                if (!hitAny) continue;
                if (next == null) continue;
                var center = selection.EffectCenters[centerIndex];
                int previousTarget = step.PickedTargetId;
                if (previousTarget == 0)
                    foreach (var target in selection.Targets)
                        if (FightBattleTargeting.OccupiesPosition(target, center)
                            && (previousTarget == 0 || target.Id < previousTarget)) previousTarget = target.Id;
                float trigger = FightBattleHitTiming.EffectDelay(step.Hit, step.Cast, center, flight) + scopeDelay;
                foreach (var child in next)
                    pending.Add(new Step { Original = child, Hit = child.AtDelay(trigger + child.ChainLinkDelay),
                        Cast = cast.WithHitOrigin(center), History = history, PreviousTargetId = previousTarget,
                        Order = ++serial });
            }
        }
    }
}
