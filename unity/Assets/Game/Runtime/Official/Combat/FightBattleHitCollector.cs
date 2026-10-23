using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightBattleHitCollector
    {
        public static List<FightBattleHitDefinition> Collect(IReadOnlyList<FightBattleHitEntity> entities,
            int rootEntityId)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var result = new List<FightBattleHitDefinition>();
            Collect(entities, rootEntityId, result, new HashSet<int>(), 0, 0, 1);
            return result;
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
                foreach (FightBattleHitRoot root in skill.HitGraphRoots)
                {
                    List<FightBattleHitDefinition> rootHits = Collect(skill.HitGraphEntities, root.EntityId);
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
                foreach (int rootEntityId in skill.HitGraphRootEntityIds)
                    collected.AddRange(Collect(skill.HitGraphEntities, rootEntityId));
                return collected;
            }
            if (skill.HitGraphRootEntityId > 0)
                return Collect(skill.HitGraphEntities, skill.HitGraphRootEntityId);
            return skill.Hits ?? new List<FightBattleHitDefinition>();
        }

        private static void Collect(IReadOnlyList<FightBattleHitEntity> entities, int entityId,
            List<FightBattleHitDefinition> result, HashSet<int> ancestors, int depth,
            int targetMode, int randomCount)
        {
            if (entityId <= 0 || depth > 12 || ancestors.Contains(entityId)) return;
            FightBattleHitEntity entity = Find(entities, entityId);
            if (entity == null) return;
            var branch = new HashSet<int>(ancestors) { entityId };

            if (entity.HasFixedDamage)
                result.Add(new FightBattleHitDefinition
                {
                    EntityId = entity.EntityId,
                    DamageId = entity.DamageId,
                    DamageType = entity.DamageType,
                    BasicProperty = entity.BasicProperty,
                    DamageProperty = entity.DamageProperty,
                    FixedDamageProperty = entity.FixedDamageProperty,
                    DamageParam = entity.DamageParam,
                    TargetMode = targetMode,
                    RandomCount = randomCount,
                    ActionType = entity.ActionType,
                    Delay = entity.Delay,
                    DelayDistFactor = entity.DelayDistFactor,
                    Anchor = entity.Anchor,
                    IgnoreAICount = entity.IgnoreAICount,
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
                targetMode, randomCount);
            CollectChildren(entities, entity.CustomTargetHits, result, branch, depth + 1, 1, 0);
            CollectChildren(entities, entity.RandomTargetHits, result, branch, depth + 1, 2,
                Math.Max(1, entity.MiniHitTargetCount));
        }

        private static void CollectChildren(IReadOnlyList<FightBattleHitEntity> entities,
            List<int> children, List<FightBattleHitDefinition> result, HashSet<int> ancestors,
            int depth, int targetMode, int randomCount)
        {
            if (children == null) return;
            for (int i = 0; i < children.Count; i++)
                Collect(entities, children[i], result, ancestors, depth, targetMode, randomCount);
        }

        private static FightBattleHitEntity Find(IReadOnlyList<FightBattleHitEntity> entities, int id)
        {
            for (int i = 0; i < entities.Count; i++)
                if (entities[i] != null && entities[i].EntityId == id) return entities[i];
            return null;
        }
    }
}
