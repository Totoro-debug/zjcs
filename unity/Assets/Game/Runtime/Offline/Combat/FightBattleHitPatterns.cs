using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // A pattern plans occurrences; the executor owns settlement and lifecycle.
    // Different skills select a pattern from their original hit configuration.
    public interface IFightHitPattern
    {
        IReadOnlyList<FightHitOccurrence> Plan(FightHitPatternContext context);
    }

    public sealed class FightHitPatternContext
    {
        public FightBattleUnitState Source;
        public IReadOnlyList<FightBattleUnitState> Units;
        public FightBattleSkillDefinition Skill;
        public FightBattleHitDefinition Hit;
        public List<FightBattleUnitState> Targets;
        public FightBattleUnitState Primary;
        public FightBattleSkillCastPresentation Cast;
        public FightRandomStream Random;
        // A random parent has already paid its configured target/anchor count.
        public bool SelectedByRandomParent;
    }

    public sealed class FightHitOccurrence
    {
        public readonly FightBattleUnitState Target;
        public readonly bool HasGridPosition;
        public readonly FightGridPoint GridPosition;
        public readonly float Delay;

        internal FightHitOccurrence(FightBattleUnitState target,
            bool hasGridPosition = false, FightGridPoint gridPosition = default, float delay = 0f)
        {
            Target = target; HasGridPosition = hasGridPosition;
            GridPosition = gridPosition; Delay = delay;
        }
    }

    public static class FightBattleHitPatterns
    {
        private static readonly IFightHitPattern Entity = new EntityPattern();
        private static readonly IFightHitPattern Area = new AreaPattern();

        public static IFightHitPattern For(FightBattleHitDefinition hit)
        {
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            return hit.Scope != null && hit.Scope.Count > 0 ? Area : Entity;
        }

        private sealed class EntityPattern : IFightHitPattern
        {
            public IReadOnlyList<FightHitOccurrence> Plan(FightHitPatternContext context)
            {
                Require(context);
                var targets = context.SelectedByRandomParent ? context.Targets
                    : FightBattleTargeting.SelectHitTargets(context.Targets,
                        context.Hit.TargetMode, context.Hit.RandomCount, context.Random);
                var result = new List<FightHitOccurrence>();
                if (targets != null) foreach (var target in targets)
                    result.Add(new FightHitOccurrence(target));
                return result;
            }
        }

        private sealed class AreaPattern : IFightHitPattern
        {
            public IReadOnlyList<FightHitOccurrence> Plan(FightHitPatternContext context)
            {
                Require(context);
                var hit = context.Hit;
                bool perCell = hit.ActionType == 4 && context.Skill.SkillType != 3
                    && !hit.IsSummon && hit.Scope.Count > 1;
                // Fixed multi-cell attacks have no implicit one-entity limit.
                // Explicit random-target counts remain entity selection constraints.
                var selector = new FightBattleHitDefinition { Anchor = hit.Anchor,
                    Scope = hit.Scope, ScopeTransform = hit.ScopeTransform,
                    TargetMode = perCell || context.SelectedByRandomParent ? 1 : hit.TargetMode };
                var candidates = FightBattleTargeting.SelectScopeTargets(context.Source,
                    context.Units, context.Skill, context.Primary, selector, context.Random, context.Cast);
                if (!context.SelectedByRandomParent && hit.TargetMode == 2)
                    candidates = FightBattleTargeting.SelectHitTargets(candidates, 2, hit.RandomCount, context.Random);
                var result = new List<FightHitOccurrence>();
                if (!perCell)
                {
                    foreach (var target in candidates) result.Add(new FightHitOccurrence(target));
                    return result;
                }
                var center = FightHitAnchorExtensions.ResolveCenter(hit.Anchor,
                    context.Source, context.Primary, context.Cast);
                var scope = FightBattleTargeting.TransformScope(hit.Scope,
                    hit.ScopeTransform, context.Source, context.Cast);
                for (int index = 0; index < scope.Count; index++)
                {
                    var cell = new FightGridPoint(center.X + scope[index].X, center.Y + scope[index].Y);
                    float delay = ScopeDelay(hit, index);
                    foreach (var target in candidates)
                        if (Occupies(target, cell)) result.Add(new FightHitOccurrence(target, true, cell, delay));
                }
                return result;
            }

            private static float ScopeDelay(FightBattleHitDefinition hit, int index)
            {
                if (hit.ScopeEntries == null || hit.ScopeEntries.Count == 0) return 0f;
                if (hit.ScopeEntries.Count != hit.Scope.Count || hit.ScopeEntries[index] == null
                    || !hit.ScopeEntries[index].Position.Equals(hit.Scope[index]))
                    throw new InvalidOperationException("Original hit scope entries do not match their positions.");
                return hit.ScopeEntries[index].Delay;
            }

            private static bool Occupies(FightBattleUnitState target, FightGridPoint cell)
            {
                if (target.BodyOffsets == null || target.BodyOffsets.Count == 0)
                    return target.X == cell.X && target.Y == cell.Y;
                foreach (var body in target.BodyOffsets)
                    if (target.X + body.X == cell.X && target.Y + body.Y == cell.Y) return true;
                return false;
            }
        }

        private static void Require(FightHitPatternContext context)
        {
            if (context == null || context.Source == null || context.Units == null
                || context.Skill == null || context.Hit == null)
                throw new ArgumentException("A hit pattern requires source, roster, skill and hit configuration.");
        }
    }
}
