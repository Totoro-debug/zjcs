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
        /// delay/anchor to the first node of each root. Shared by the executor and by area targeting.
        /// </summary>
        public static List<FightBattleHitDefinition> CollectAll(FightBattleSkillDefinition skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (skill.HitGraphRoots != null && skill.HitGraphRoots.Count > 0)
            {
                var collected = new List<FightBattleHitDefinition>();
                int nextRandomTargetGroupId = 1;
                foreach (FightBattleHitRoot root in skill.HitGraphRoots)
                {
                    List<FightBattleHitDefinition> rootHits = CollectRoot(skill.HitGraphEntities,
                        root.EntityId, ref nextRandomTargetGroupId);
                    if (rootHits.Count > 0)
                    {
                        rootHits[0].Delay = root.Delay;
                        rootHits[0].Anchor = root.Anchor;
                        rootHits[0].IgnoreAICount = root.IgnoreAICount;
                    }
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
                        ref nextRandomTargetGroupId));
                return collected;
            }
            if (skill.HitGraphRootEntityId > 0)
                return Collect(skill.HitGraphEntities, skill.HitGraphRootEntityId);
            return skill.Hits ?? new List<FightBattleHitDefinition>();
        }

        private static List<FightBattleHitDefinition> CollectRoot(
            IReadOnlyList<FightBattleHitEntity> entities, int rootEntityId,
            ref int nextRandomTargetGroupId)
        {
            var result = new List<FightBattleHitDefinition>();
            Collect(entities, rootEntityId, result, new HashSet<int>(), 0, 0, 1, null,
                ref nextRandomTargetGroupId);
            return result;
        }

        private static void Collect(IReadOnlyList<FightBattleHitEntity> entities, int entityId,
            List<FightBattleHitDefinition> result, HashSet<int> ancestors, int depth,
            int targetMode, int randomCount, RandomTargetContext randomTargetContext,
            ref int nextRandomTargetGroupId)
        {
            if (entityId <= 0 || depth > 12 || ancestors.Contains(entityId)) return;
            FightBattleHitEntity entity = Find(entities, entityId);
            if (entity == null) return;
            var branch = new HashSet<int>(ancestors) { entityId };

            if (entity.HasFixedDamage || entity.IsSummon)
                result.Add(new FightBattleHitDefinition
                {
                    EntityId = entity.EntityId,
                    DamageId = entity.DamageId,
                    HasFixedDamage = entity.HasFixedDamage,
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
                    SummonReferenceType = entity.SummonReferenceType,
                    SummonLevelType = entity.SummonLevelType,
                    SummonLifespanStatusId = entity.SummonLifespanStatusId,
                    SummonPools = entity.SummonPools == null
                        ? new List<FightBattleSummonPool>()
                        : new List<FightBattleSummonPool>(entity.SummonPools),
                    SummonStatusIds = entity.SummonStatusIds == null
                        ? new List<int>() : new List<int>(entity.SummonStatusIds),
                    SummonPositions = entity.SummonPositions == null
                        ? new List<FightGridPoint>() : new List<FightGridPoint>(entity.SummonPositions),
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
                    ActionType = entity.ActionType,
                    Delay = entity.Delay,
                    DelayDistFactor = entity.DelayDistFactor,
                    Anchor = entity.Anchor,
                    IgnoreAICount = entity.IgnoreAICount,
                    TrueDamage = entity.TrueDamage,
                    BreakResilience = entity.BreakResilience,
                    Scope = entity.Scope == null
                        ? new List<FightGridPoint>() : new List<FightGridPoint>(entity.Scope),
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
                        OffsetX = entity.HitMove.OffsetX,
                        OffsetY = entity.HitMove.OffsetY,
                        TowardsTargetPos = entity.HitMove.TowardsTargetPos,
                        Direction = entity.HitMove.Direction,
                        Distance = entity.HitMove.Distance,
                        BasePercent = entity.HitMove.BasePercent,
                        AffectedByProp = entity.HitMove.AffectedByProp,
                        CancelDiagonally = entity.HitMove.CancelDiagonally,
                        HasConditions = entity.HitMove.HasConditions
                    }
                });

            CollectChildren(entities, entity.ChainedHits, result, branch, depth + 1,
                targetMode, randomCount, randomTargetContext, ref nextRandomTargetGroupId);
            CollectChildren(entities, entity.CustomTargetHits, result, branch, depth + 1,
                1, 0, null, ref nextRandomTargetGroupId);
            RandomTargetContext childRandomTargetContext = entity.RandomTargetHits == null
                || entity.RandomTargetHits.Count == 0 ? null : new RandomTargetContext
                {
                    GroupId = nextRandomTargetGroupId++,
                    Anchor = entity.Anchor,
                    NotSameTarget = entity.NotSameTarget,
                    AllowEmptyScope = entity.AllowEmptyScope,
                    NotSameEmptyScope = entity.NotSameEmptyScope,
                    NotAllowHitEmpty = entity.NotAllowHitEmpty,
                    NotSameGrid = entity.NotSameGrid,
                    FilterOutOfGroundScope = entity.FilterOutOfGroundScope,
                    Scope = entity.Scope == null ? new List<FightGridPoint>()
                        : new List<FightGridPoint>(entity.Scope)
                };
            CollectChildren(entities, entity.RandomTargetHits, result, branch, depth + 1, 2,
                Math.Max(1, entity.MiniHitTargetCount), childRandomTargetContext,
                ref nextRandomTargetGroupId);
        }

        private static void CollectChildren(IReadOnlyList<FightBattleHitEntity> entities,
            List<int> children, List<FightBattleHitDefinition> result, HashSet<int> ancestors,
            int depth, int targetMode, int randomCount,
            RandomTargetContext randomTargetContext, ref int nextRandomTargetGroupId)
        {
            if (children == null) return;
            for (int i = 0; i < children.Count; i++)
                Collect(entities, children[i], result, ancestors, depth, targetMode, randomCount,
                    randomTargetContext, ref nextRandomTargetGroupId);
        }

        private static FightBattleHitEntity Find(IReadOnlyList<FightBattleHitEntity> entities, int id)
        {
            for (int i = 0; i < entities.Count; i++)
                if (entities[i] != null && entities[i].EntityId == id) return entities[i];
            return null;
        }
    }
}
