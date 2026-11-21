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
        public readonly int EffectCenterIndex;
        // -1 means the occurrence is not attributable to one authored scope cell.
        public readonly int ScopeIndex;

        internal FightHitOccurrence(FightBattleUnitState target,
            bool hasGridPosition = false, FightGridPoint gridPosition = default, float delay = 0f,
            int effectCenterIndex = 0, int scopeIndex = -1)
        {
            Target = target; HasGridPosition = hasGridPosition;
            GridPosition = gridPosition; Delay = delay; EffectCenterIndex = effectCenterIndex; ScopeIndex = scopeIndex;
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
                var candidates = context.Targets == null ? null : context.Targets.FindAll(target =>
                    !FightBattleTargeting.IsHiddenFromEnemySelection(context.Source, target));
                var targets = context.SelectedByRandomParent ? candidates
                    : FightBattleTargeting.SelectHitTargets(candidates,
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
                // Scope enumerates affected cells for damage, healing and status hits alike.
                // Explicit random-target counts are applied to the candidates below.
                var selector = new FightBattleHitDefinition { Anchor = hit.Anchor,
                    Scope = hit.Scope, ScopeTransform = hit.ScopeTransform,
                    TargetMode = 1 };
                var candidates = FightBattleTargeting.SelectScopeTargets(context.Source,
                    context.Units, context.Skill, context.Primary, selector, context.Random, context.Cast,
                    hit.ActionType == 62 ? context.Targets : null);
                if (!context.SelectedByRandomParent && hit.TargetMode == 2)
                    candidates = FightBattleTargeting.SelectHitTargets(candidates, 2, hit.RandomCount, context.Random);
                var result = new List<FightHitOccurrence>();
                if (!perCell)
                {
                    // A single authored cell still delays its damage/status occurrence.
                    // Keeping entity-level selection here must not drop HitScopCfg.Delay.
                    if (hit.Scope.Count == 1 || hit.IsSummon)
                    {
                        float delay = hit.Scope.Count == 1 ? ScopeDelay(hit, 0) : 0f;
                        foreach (var target in candidates)
                            result.Add(new FightHitOccurrence(target, delay: delay,
                                scopeIndex: hit.Scope.Count == 1 ? 0 : -1));
                        return result;
                    }
                    var entityCenter = FightHitAnchorExtensions.ResolveCenter(hit.Anchor,
                        context.Source, context.Primary, context.Cast);
                    var entityScope = FightBattleTargeting.TransformScope(hit.Scope,
                        hit.ScopeTransform, context.Source, context.Cast);
                    bool allIdentified = true;
                    foreach (var target in candidates)
                    {
                        int found = -1;
                        FightGridPoint foundCell = default;
                        for (int index = 0; index < entityScope.Count; index++)
                        {
                            var cell = new FightGridPoint(entityCenter.X + entityScope[index].X,
                                entityCenter.Y + entityScope[index].Y);
                            if (!Occupies(target, cell)) continue;
                            if (found >= 0) { found = -1; break; }
                            found = index; foundCell = cell;
                        }
                        if (found < 0)
                        {
                            allIdentified = false;
                            result.Add(new FightHitOccurrence(target));
                        }
                        else result.Add(new FightHitOccurrence(target, true, foundCell,
                            ScopeDelay(hit, found), scopeIndex: found));
                    }
                    // Scope entries already use the original hit's playback order. Preserve
                    // candidate order for ties and do not guess where an ambiguous body lands.
                    if (allIdentified)
                        for (int index = 1; index < result.Count; index++)
                        {
                            var occurrence = result[index]; int position = index;
                            while (position > 0 && result[position - 1].ScopeIndex > occurrence.ScopeIndex)
                            { result[position] = result[position - 1]; position--; }
                            result[position] = occurrence;
                        }
                    return result;
                }
                var center = FightHitAnchorExtensions.ResolveCenter(hit.Anchor,
                    context.Source, context.Primary, context.Cast);
                var scope = FightBattleTargeting.TransformScope(hit.Scope,
                    hit.ScopeTransform, context.Source, context.Cast);
                var settled = FightSkillHitPolicy.OncePerTarget(context.Skill, hit)
                    ? new HashSet<int>() : null;
                for (int index = 0; index < scope.Count; index++)
                {
                    var cell = new FightGridPoint(center.X + scope[index].X, center.Y + scope[index].Y);
                    float delay = ScopeDelay(hit, index);
                    foreach (var target in candidates)
                        if (Occupies(target, cell) && (settled == null || settled.Add(target.Id)))
                            result.Add(new FightHitOccurrence(target, true, cell, delay, scopeIndex: index));
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
