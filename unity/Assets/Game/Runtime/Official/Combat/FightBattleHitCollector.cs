using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightBattleHitCollector
    {
        private sealed class RandomTargetContext
        {
            public int GroupId;
            public int Anchor;
            public bool NotSameTarget;
            public bool AllowEmptyScope;
            public bool NotSameEmptyScope;
            public bool NotAllowHitEmpty;
            public bool NotSameGrid;
            public bool FilterOutOfGroundScope;
            public List<FightGridPoint> Scope;
            public FightBattleHitScopeTransform ScopeTransform;
        }

        public static List<FightBattleHitDefinition> Collect(IReadOnlyList<FightBattleHitEntity> entities,
            int rootEntityId)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            int nextRandomTargetGroupId = 1;
            return CollectRoot(entities, rootEntityId, ref nextRandomTargetGroupId);
        }

        /// <summary>
        /// Collects every hit of a skill in execution order, applying the configured root-reference
        /// delay/anchor to that occurrence of each root. Shared by the executor and by area targeting.
        /// </summary>
        public static List<FightBattleHitDefinition> CollectAll(FightBattleSkillDefinition skill,
            bool includeRootViews = false)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (skill.HitGraphRoots != null && skill.HitGraphRoots.Count > 0)
            {
                var collected = new List<FightBattleHitDefinition>();
                int nextRandomTargetGroupId = 1;
                foreach (FightBattleHitRoot root in skill.HitGraphRoots)
                {
                    List<FightBattleHitDefinition> rootHits = CollectRoot(skill.HitGraphEntities,
                        root.EntityId, ref nextRandomTargetGroupId, new FightBattleHitLink
                        { EntityId = root.EntityId, Delay = root.Delay, Anchor = root.Anchor,
                            IgnoreAICount = root.IgnoreAICount }, includeRootViews);
                    collected.AddRange(rootHits);
                }
                return collected;
            }
            if (skill.HitGraphRootEntityIds != null && skill.HitGraphRootEntityIds.Count > 0)
            {
                var collected = new List<FightBattleHitDefinition>();
                int nextRandomTargetGroupId = 1;
                foreach (int rootEntityId in skill.HitGraphRootEntityIds)
                    collected.AddRange(CollectRoot(skill.HitGraphEntities, rootEntityId,
                        ref nextRandomTargetGroupId, includeRootViews: includeRootViews));
                return collected;
            }
            if (skill.HitGraphRootEntityId > 0)
            {
                int nextRandomTargetGroupId = 1;
                return CollectRoot(skill.HitGraphEntities, skill.HitGraphRootEntityId,
                    ref nextRandomTargetGroupId, includeRootViews: includeRootViews);
            }
            return skill.Hits ?? new List<FightBattleHitDefinition>();
        }

        private static List<FightBattleHitDefinition> CollectRoot(
            IReadOnlyList<FightBattleHitEntity> entities, int rootEntityId,
            ref int nextRandomTargetGroupId, FightBattleHitLink incoming = null,
            bool includeRootViews = false)
        {
            var result = new List<FightBattleHitDefinition>();
            Collect(entities, rootEntityId, result, new HashSet<int>(), 0, 0, 1, null,
                ref nextRandomTargetGroupId, incoming, includeRootView: includeRootViews);
            return result;
        }

        private static void Collect(IReadOnlyList<FightBattleHitEntity> entities, int entityId,
            List<FightBattleHitDefinition> result, HashSet<int> ancestors, int depth,
            int targetMode, int randomCount, RandomTargetContext randomTargetContext,
            ref int nextRandomTargetGroupId, FightBattleHitLink incoming = null, float parentDelay = 0f,
            bool includeRootView = false)
        {
            if (entityId <= 0 || depth > 12 || ancestors.Contains(entityId)) return;
            FightBattleHitEntity entity = Find(entities, entityId);
            if (entity == null) return;
            var branch = new HashSet<int>(ancestors) { entityId };
            float occurrenceDelay = parentDelay + (incoming == null ? entity.Delay : incoming.Delay);

            bool viewOnly = includeRootView && entity.HitComponentType == "FightHitRandomTargetComponent"
                && entity.Scope != null && entity.Scope.Count > 0
                && !entity.HasFixedDamage && !entity.IsSummon && entity.TriggerSkill == null;
            FightBattleHitDefinition definition = null;
            if (entity.HasFixedDamage || entity.IsSummon || entity.TriggerSkill != null || viewOnly)
            {
                definition = new FightBattleHitDefinition
                {
                    ChainConfiguration = entity.IsChained ? entity : null,
                    IsViewOnly = viewOnly,
                    EntityId = entity.EntityId,
                    DamageId = entity.DamageId,
                    HasFixedDamage = entity.HasFixedDamage,
                    AirCombo = entity.AirCombo,
                    DamageType = entity.DamageType,
                    BasicProperty = entity.BasicProperty,
                    DamageProperty = entity.DamageProperty,
                    FixedDamageProperty = entity.FixedDamageProperty,
                    DamageParam = entity.DamageParam,
                    IgnoreShield = entity.IgnoreShield,
                    IsSummon = entity.IsSummon,
                    SummonIsRandom = entity.SummonIsRandom,
                    SummonImmediateRound = entity.SummonImmediateRound,
                    SummonInheritProps = entity.SummonInheritProps,
                    SummonConfigType = entity.SummonConfigType,
                    SummonReferenceType = entity.SummonReferenceType,
                    SummonGridItemClassId = entity.SummonGridItemClassId,
                    GridItemFindAdjacentIfBlocked = entity.GridItemFindAdjacentIfBlocked,
                    GridSummonCells = entity.GridSummonCells == null
                        ? new List<FightBattleGridSummonCell>()
                        : new List<FightBattleGridSummonCell>(entity.GridSummonCells),
                    SummonLevelType = entity.SummonLevelType,
                    SummonLifespanStatusId = entity.SummonLifespanStatusId,
                    SummonPools = entity.SummonPools == null
                        ? new List<FightBattleSummonPool>()
                        : new List<FightBattleSummonPool>(entity.SummonPools),
                    SummonStatusIds = entity.SummonStatusIds == null
                        ? new List<int>() : new List<int>(entity.SummonStatusIds),
                    SummonPositions = entity.SummonPositions == null
                        ? new List<FightGridPoint>() : new List<FightGridPoint>(entity.SummonPositions),
                    TriggerSkill = entity.TriggerSkill?.Copy(),
                    ExtApplyDamageTargetHpLessThan = entity.ExtApplyDamageTargetHpLessThan,
                    ExtApplyDamageScale = entity.ExtApplyDamageScale,
                    TargetMode = targetMode,
                    RandomCount = randomCount,
                    RandomTargetGroupId = randomTargetContext == null ? 0
                        : randomTargetContext.GroupId,
                    RandomTargetAnchor = randomTargetContext == null ? 0
                        : randomTargetContext.Anchor,
                    RandomTargetNotSameTarget = randomTargetContext != null
                        && randomTargetContext.NotSameTarget,
                    RandomTargetAllowEmptyScope = randomTargetContext != null
                        && randomTargetContext.AllowEmptyScope,
                    RandomTargetNotSameEmptyScope = randomTargetContext != null
                        && randomTargetContext.NotSameEmptyScope,
                    RandomTargetNotAllowHitEmpty = randomTargetContext != null
                        && randomTargetContext.NotAllowHitEmpty,
                    RandomTargetNotSameGrid = randomTargetContext != null
                        && randomTargetContext.NotSameGrid,
                    RandomTargetFilterOutOfGroundScope = randomTargetContext == null
                        || randomTargetContext.FilterOutOfGroundScope,
                    RandomTargetScope = randomTargetContext == null
                        || randomTargetContext.Scope == null
                        ? new List<FightGridPoint>()
                        : new List<FightGridPoint>(randomTargetContext.Scope),
                    RandomTargetScopeTransform = randomTargetContext?.ScopeTransform,
                    ActionType = entity.ActionType,
                    Delay = occurrenceDelay,
                    DelayDistFactor = entity.DelayDistFactor,
                    Trajectory = entity.Trajectory,
                    Anchor = incoming == null ? entity.Anchor : incoming.Anchor,
                    IgnoreAICount = incoming == null ? entity.IgnoreAICount : incoming.IgnoreAICount,
                    TrueDamage = entity.TrueDamage,
                    UseDamageFactor = entity.UseDamageFactor,
                    BreakResilience = entity.BreakResilience,
                    Scope = entity.ScopeEntries != null && entity.ScopeEntries.Count > 0
                        ? entity.ScopeEntries.ConvertAll(entry => entry.Position)
                        : entity.Scope == null ? new List<FightGridPoint>() : new List<FightGridPoint>(entity.Scope),
                    ScopeTransform = entity.ScopeTransform,
                    ScopeEntries = entity.ScopeEntries == null
                        ? new List<FightBattleHitScopeEntry>() : new List<FightBattleHitScopeEntry>(entity.ScopeEntries),
                    AllowEmptyScope = entity.AllowEmptyScope,
                    AppliedStatuses = entity.AppliedStatuses == null
                        ? new List<FightBattleStatusApplication>()
                        : new List<FightBattleStatusApplication>(entity.AppliedStatuses),
                    StatusListRandomOne = entity.StatusListRandomOne,
                    HemophagiaProperty = entity.HemophagiaProperty,
                    ChildSkillId = entity.ChildSkillId,
                    ChildSkillRate = entity.ChildSkillRate,
                    ChildSkillLessThanHpPer = entity.ChildSkillLessThanHpPer,
                    DisperseChance = entity.DisperseChance,
                    DisperseAffectedByProp = entity.DisperseAffectedByProp,
                    DisperseStatusTypes = entity.DisperseStatusTypes == null
                        ? new List<int>() : new List<int>(entity.DisperseStatusTypes),
                    DisperseCount = entity.DisperseCount,
                    DisperseRandom = entity.DisperseRandom,
                    HitMove = entity.HitMove == null ? null : new FightBattleSkillMove
                    {
                        MoveBehaviourId = entity.HitMove.MoveBehaviourId,
                        Delay = entity.HitMove.Delay,
                        MoveDuration = entity.HitMove.MoveDuration,
                        MoveSpeed = entity.HitMove.MoveSpeed,
                        OffsetX = entity.HitMove.OffsetX,
                        OffsetY = entity.HitMove.OffsetY,
                        TowardsTargetPos = entity.HitMove.TowardsTargetPos,
                        Direction = entity.HitMove.Direction,
                        Distance = entity.HitMove.Distance,
                        BasePercent = entity.HitMove.BasePercent,
                        AffectedByProp = entity.HitMove.AffectedByProp,
                        CancelDiagonally = entity.HitMove.CancelDiagonally,
                        HasConditions = entity.HitMove.HasConditions,
                        Conditions = CopyMoveConditions(entity.HitMove.Conditions)
                    }
                };
                result.Add(definition);
            }

            CollectChildren(entities, entity.ChainedHits, entity.ChainedHitLinks, result, branch, depth + 1, occurrenceDelay,
                targetMode, randomCount, randomTargetContext, ref nextRandomTargetGroupId,
                entity.IsChained ? definition : null);
            CollectChildren(entities, entity.CustomTargetHits, entity.CustomTargetHitLinks, result, branch, depth + 1, occurrenceDelay,
                1, 0, null, ref nextRandomTargetGroupId);
            bool hasRandomChildren = entity.RandomTargetHitLinks != null && entity.RandomTargetHitLinks.Count > 0
                || entity.RandomTargetHits != null && entity.RandomTargetHits.Count > 0;
            RandomTargetContext childRandomTargetContext = !hasRandomChildren ? null : new RandomTargetContext
                {
                    GroupId = nextRandomTargetGroupId++,
                    Anchor = incoming == null ? entity.Anchor : incoming.Anchor,
                    NotSameTarget = entity.NotSameTarget,
                    AllowEmptyScope = entity.AllowEmptyScope,
                    NotSameEmptyScope = entity.NotSameEmptyScope,
                    NotAllowHitEmpty = entity.NotAllowHitEmpty,
                    NotSameGrid = entity.NotSameGrid,
                    FilterOutOfGroundScope = entity.FilterOutOfGroundScope,
                    Scope = entity.Scope == null ? new List<FightGridPoint>()
                        : new List<FightGridPoint>(entity.Scope),
                    ScopeTransform = entity.ScopeTransform
                };
            CollectChildren(entities, entity.RandomTargetHits, entity.RandomTargetHitLinks, result, branch, depth + 1, occurrenceDelay, 2,
                Math.Max(1, entity.MiniHitTargetCount), childRandomTargetContext,
                ref nextRandomTargetGroupId);
        }

        private static List<FightBattleSkillMoveCondition> CopyMoveConditions(
            List<FightBattleSkillMoveCondition> conditions)
        {
            var result = new List<FightBattleSkillMoveCondition>();
            if (conditions == null) return result;
            foreach (FightBattleSkillMoveCondition condition in conditions)
                result.Add(condition == null ? null : new FightBattleSkillMoveCondition
                {
                    Type = condition.Type,
                    Distance = condition.Distance
                });
            return result;
        }

        private static void CollectChildren(IReadOnlyList<FightBattleHitEntity> entities,
            List<int> children, List<FightBattleHitLink> links, List<FightBattleHitDefinition> result, HashSet<int> ancestors,
            int depth, float parentDelay, int targetMode, int randomCount,
            RandomTargetContext randomTargetContext, ref int nextRandomTargetGroupId,
            FightBattleHitDefinition previousChainHit = null)
        {
            // HitCfg belongs to an edge, not to its shared entity class. Never mutate
            // the class while expanding repeated references. Parent configured delays accumulate
            // by the confirmed offline rule. ID-only legacy graphs retain their old timing.
            if (links != null && links.Count > 0)
            {
                foreach (FightBattleHitLink link in links)
                {
                    if (link == null) throw new InvalidOperationException("Null original HitCfg reference.");
                    int first = result.Count;
                    Collect(entities, link.EntityId, result, ancestors, depth, targetMode, randomCount,
                        randomTargetContext, ref nextRandomTargetGroupId, link, parentDelay);
                    BindChainParent(result, first, previousChainHit, link.EntityId, link.Delay);
                }
                return;
            }
            if (children == null) return;
            for (int i = 0; i < children.Count; i++)
            {
                int first = result.Count;
                Collect(entities, children[i], result, ancestors, depth, targetMode, randomCount,
                    randomTargetContext, ref nextRandomTargetGroupId);
                BindChainParent(result, first, previousChainHit, children[i],
                    first < result.Count ? result[first].Delay : 0f);
            }
        }

        private static void BindChainParent(List<FightBattleHitDefinition> result, int first,
            FightBattleHitDefinition parent, int childId, float delay)
        {
            if (parent == null || first >= result.Count || result[first].EntityId != childId) return;
            var child = result[first];
            if (child.ChainConfiguration == null)
                throw new InvalidOperationException("A chained edge requires its original chained child configuration.");
            child.PreviousChainHit = parent;
            child.ChainLinkDelay = delay;
        }

        private static FightBattleHitEntity Find(IReadOnlyList<FightBattleHitEntity> entities, int id)
        {
            for (int i = 0; i < entities.Count; i++)
                if (entities[i] != null && entities[i].EntityId == id) return entities[i];
            return null;
        }
    }
}
