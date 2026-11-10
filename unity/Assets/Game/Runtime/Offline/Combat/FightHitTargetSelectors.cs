using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public interface IFightHitTargetSelector
    {
        // Null means a random parent found no anchor; its dependent hit is skipped.
        FightHitTargetSelection Select(FightHitTargetSelectionContext context);
    }

    public sealed class FightHitTargetSelectionContext
    {
        public FightBattleUnitState Source, TargetingSource, Primary;
        public IReadOnlyList<FightBattleUnitState> Units;
        public FightBattleSkillDefinition Skill;
        public FightBattleHitDefinition Hit;
        public List<FightBattleUnitState> Targets;
        public FightBattleSkillCastPresentation Cast;
        public FightBattleGrid Grid;
        public FightRandomStream Random;
    }

    public sealed class FightHitTargetSelection
    {
        public readonly List<FightBattleUnitState> Targets;
        public readonly IReadOnlyList<FightHitOccurrence> Occurrences;
        public readonly IReadOnlyList<FightGridPoint> SelectedGridPositions;
        public readonly IReadOnlyList<FightGridPoint> EffectCenters;

        internal FightHitTargetSelection(List<FightBattleUnitState> targets,
            IReadOnlyList<FightHitOccurrence> occurrences, IReadOnlyList<FightGridPoint> selectedGridPositions,
            IReadOnlyList<FightGridPoint> effectCenters)
        {
            Targets = targets; Occurrences = occurrences;
            SelectedGridPositions = selectedGridPositions; EffectCenters = effectCenters;
        }
    }

    // One instance per cast. Random parent groups share exclusions across their
    // child hits; nested/next casts own another instance and cannot inherit them.
    public sealed class FightHitTargetSelectors
    {
        private static readonly IFightHitTargetSelector Direct = new DirectSelector();
        private readonly IFightHitTargetSelector randomParent = new RandomParentSelector();

        public IFightHitTargetSelector For(FightBattleHitDefinition hit)
        {
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            return hit.RandomTargetGroupId > 0 ? randomParent : Direct;
        }

        private sealed class DirectSelector : IFightHitTargetSelector
        {
            public FightHitTargetSelection Select(FightHitTargetSelectionContext context)
            {
                Require(context);
                var planned = Plan(context, context.Hit, context.Targets, context.Primary);
                var centers = new List<FightGridPoint>();
                if (context.Hit.Scope != null && context.Hit.Scope.Count > 0)
                    centers.Add(FightHitAnchorExtensions.ResolveCenter(context.Hit.Anchor,
                        context.Source, context.Primary, context.Cast));
                else foreach (var occurrence in planned)
                    centers.Add(FightHitAnchorExtensions.ResolveCenter(context.Hit.Anchor,
                        context.Source, occurrence.Target, context.Cast));
                return new FightHitTargetSelection(planned.Select(value => value.Target).ToList(), planned,
                    new List<FightGridPoint>(), centers);
            }
        }

        private sealed class RandomParentSelector : IFightHitTargetSelector
        {
            private sealed class RandomTargetGroupState
            {
                public readonly HashSet<int> TargetIds = new HashSet<int>();
                public readonly HashSet<FightGridPoint> EmptyPositions = new HashSet<FightGridPoint>();
                public readonly HashSet<FightGridPoint> GridPositions = new HashSet<FightGridPoint>();
            }

            private readonly Dictionary<int, RandomTargetGroupState> randomTargetGroups
                = new Dictionary<int, RandomTargetGroupState>();

            public FightHitTargetSelection Select(FightHitTargetSelectionContext context)
            {
                Require(context);
                var actor = context.Source;
                var targetingSource = context.TargetingSource ?? actor;
                var units = context.Units; var skill = context.Skill; var hit = context.Hit;
                var targets = context.Targets; var targetPosition = context.Primary;
                var hitCast = context.Cast; var random = context.Random;
                List<FightBattleUnitState> hitTargets;
                IReadOnlyList<FightHitOccurrence> plannedHits = null;
                if (!randomTargetGroups.TryGetValue(hit.RandomTargetGroupId,
                    out RandomTargetGroupState groupState))
                {
                    groupState = new RandomTargetGroupState();
                    randomTargetGroups.Add(hit.RandomTargetGroupId, groupState);
                }

                var selectedTargets = new List<FightBattleUnitState>();
                var selectedPositions = new List<FightGridPoint>();
                if (hit.RandomTargetScope != null && hit.RandomTargetScope.Count > 0)
                {
                    var scopeCenter = FightHitAnchorExtensions.ResolveCenter(hit.RandomTargetAnchor,
                        actor, targetPosition ?? (targets != null && targets.Count > 0 ? targets[0] : null), hitCast);
                    var scopeAnchor = new FightBattleUnitState { X = scopeCenter.X, Y = scopeCenter.Y };
                    List<FightBattleRandomScopeSelection> selections =
                        FightBattleTargeting.SelectRandomScopePositions(targetingSource, units, skill,
                            scopeAnchor, hit.RandomTargetScope, context.Grid,
                            hit.RandomCount, random, hit.RandomTargetAllowEmptyScope,
                            hit.RandomTargetNotAllowHitEmpty, hit.RandomTargetNotSameTarget,
                            groupState.TargetIds, hit.RandomTargetNotSameEmptyScope,
                            groupState.EmptyPositions, hit.RandomTargetNotSameGrid,
                            groupState.GridPositions,
                            hit.RandomTargetFilterOutOfGroundScope, hit.RandomTargetScopeTransform, hitCast);
                    foreach (FightBattleRandomScopeSelection selection in selections)
                    {
                        selectedPositions.Add(selection.Position);
                        groupState.GridPositions.Add(selection.Position);
                        if (selection.IsEmpty)
                            groupState.EmptyPositions.Add(selection.Position);
                        foreach (FightBattleUnitState selectedTarget in selection.Targets)
                        {
                            if (hit.RandomTargetNotSameTarget)
                                groupState.TargetIds.Add(selectedTarget.Id);
                            if (!hit.RandomTargetNotSameTarget
                                || !selectedTargets.Exists(target => target.Id == selectedTarget.Id))
                                selectedTargets.Add(selectedTarget);
                        }
                    }
                }
                else
                {
                    var randomCandidates = new List<FightBattleUnitState>();
                    foreach (FightBattleUnitState candidate in targets ??
                        new List<FightBattleUnitState>())
                        if ((!hit.RandomTargetNotSameGrid
                                || !groupState.GridPositions.Contains(
                                    new FightGridPoint(candidate.X, candidate.Y)))
                            && (!hit.RandomTargetNotSameTarget
                                || !groupState.TargetIds.Contains(candidate.Id)))
                            randomCandidates.Add(candidate);
                    selectedTargets = FightBattleTargeting.SelectRandomHitTargets(
                        randomCandidates, hit.RandomCount, random,
                        hit.RandomTargetNotSameTarget ? groupState.TargetIds : null);
                    foreach (FightBattleUnitState selectedTarget in selectedTargets)
                    {
                        selectedPositions.Add(new FightGridPoint(selectedTarget.X, selectedTarget.Y));
                        groupState.TargetIds.Add(selectedTarget.Id);
                        groupState.GridPositions.Add(new FightGridPoint(
                            selectedTarget.X, selectedTarget.Y));
                    }
                }
                if (selectedPositions.Count == 0) return null;

                hitTargets = new List<FightBattleUnitState>();
                if (hit.Scope != null && hit.Scope.Count > 0)
                {
                    var randomTargetEffect = new FightBattleHitDefinition
                    {
                        Anchor = 0,
                        TargetMode = hit.TargetMode,
                        Scope = hit.Scope,
                        ScopeTransform = hit.ScopeTransform,
                        ScopeEntries = hit.ScopeEntries, ActionType = hit.ActionType, IsSummon = hit.IsSummon
                    };
                    var randomOccurrences = new List<FightHitOccurrence>();
                    // The original client transforms Scope from the hit entity's grid.
                    // A selected body cell is that grid; replacing it with the entity centre
                    // shifts both the rendered area and the damage to different cells.
                    foreach (FightGridPoint selectedPosition in selectedPositions)
                        randomOccurrences.AddRange(Plan(context, randomTargetEffect,
                            selectedTargets, new FightBattleUnitState {
                                X = selectedPosition.X, Y = selectedPosition.Y }, true));
                    plannedHits = randomOccurrences;
                    foreach (var occurrence in randomOccurrences) hitTargets.Add(occurrence.Target);
                }
                else hitTargets.AddRange(selectedTargets);
                var effectCenters = new List<FightGridPoint>(selectedPositions);
                return new FightHitTargetSelection(hitTargets, plannedHits, selectedPositions, effectCenters);
            }
        }

        private static IReadOnlyList<FightHitOccurrence> Plan(FightHitTargetSelectionContext context,
            FightBattleHitDefinition hit, List<FightBattleUnitState> targets,
            FightBattleUnitState primary, bool selectedByRandomParent = false)
        {
            return FightBattleHitPatterns.For(hit).Plan(new FightHitPatternContext {
                Source = context.TargetingSource ?? context.Source, Units = context.Units,
                Skill = context.Skill, Hit = hit, Targets = targets, Primary = primary,
                Cast = context.Cast, Random = context.Random, SelectedByRandomParent = selectedByRandomParent
            });
        }

        private static void Require(FightHitTargetSelectionContext context)
        {
            if (context == null || context.Source == null || context.Units == null
                || context.Skill == null || context.Hit == null)
                throw new ArgumentException("Hit selection requires source, roster, skill and hit configuration.");
        }
    }
}
