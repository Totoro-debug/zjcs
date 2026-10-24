using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightBattleTargeting
    {
        private const int ConfusionActionType = 54;
        private const int RidiculeActionType = 58;

        /// <summary>
        /// A unit can be chosen, occupied and hit only while it is alive, targetable and actually
        /// present in the battle. Units of later waves stay <see cref="FightBattleUnitState.Ended"/>
        /// until <c>FightBattleExecutor</c> spawns their wave, so they must not leak into targeting,
        /// hit collection or movement occupancy.
        /// </summary>
        public static bool IsSelectable(FightBattleUnitState unit)
        {
            return unit != null && unit.Hp > 0 && unit.Targetable && !unit.Ended;
        }

        public static List<FightBattleUnitState> Select(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, int targetType, int ridiculeTargetId = 0,
            bool confused = false, bool releaseWhileConfused = false,
            FightBattleSkillDefinition skill = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (units == null) throw new ArgumentNullException(nameof(units));
            var candidates = new List<FightBattleUnitState>();
            if (confused && !releaseWhileConfused
                && (skill == null || !skill.ReleaseWhileConfusion)) return candidates;
            bool confusionAllowed = confused && releaseWhileConfused
                || skill != null && skill.ReleaseWhileConfusion
                    && HasActionStatus(actor, ConfusionActionType);
            if (ridiculeTargetId <= 0 && skill != null && skill.ReleaseWhileRidicule)
                ridiculeTargetId = RidiculeSourceId(actor);
            foreach (FightBattleUnitState unit in units)
            {
                if (!IsSelectable(unit) || HasIgnoredStatus(unit, skill)) continue;
                bool isRidiculeSource = targetType == 1 && ridiculeTargetId > 0
                    && unit.Id == ridiculeTargetId;
                if (isRidiculeSource || MatchesTargetType(actor, unit, targetType, confusionAllowed))
                    candidates.Add(unit);
            }

            candidates.Sort((left, right) => CompareCandidates(actor, left, right, skill, units));
            PrioritizeRidiculeSource(candidates, targetType, ridiculeTargetId);
            if (targetType == 1 || targetType == 2 || targetType == 3 || targetType == 8 || targetType == 9)
                if (candidates.Count > 1) candidates.RemoveRange(1, candidates.Count - 1);
            return candidates;
        }

        public static FightBattleSkillDefinition FindReadySkill(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, int round, bool driving = true,
            ISet<int> excludedSlots = null, int globalRound = 0)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (units == null) throw new ArgumentNullException(nameof(units));
            if (actor.SkillState == null || actor.Skills == null) return null;
            foreach (FightBattleSkillDefinition skill in actor.Skills)
                if (skill != null) skill.UseLastAiPriority = false;
            var state = new FightSkillState(actor.SkillState);
            state.BeginRound(round);
            foreach (FightSkillSlotState slotState in actor.SkillState.Slots)
            {
                if (slotState == null || excludedSlots != null && excludedSlots.Contains(slotState.Slot)
                    || !state.CanRelease(slotState.Slot)
                    || state.LeftCooldown(slotState.Slot) > 0) continue;
                FightBattleSkillDefinition skill = FindSkill(actor.Skills, slotState.Slot);
                if (skill == null || !skill.RangeVerified) continue;
                if (!CanReleaseUnderControl(actor, skill)) continue;
                if (!ConditionAllows(actor, units, skill, round, globalRound)) continue;
                if (skill.MustHaveValidCost && skill.CostType != 0
                    && (skill.CostType == 2
                        ? FightBattleShieldRuntime.PersistentShieldTotal(actor) <= 0
                        : skill.SkillCost <= 0)) continue;
                List<FightBattleUnitState> targets = Select(actor, units, skill.TargetType,
                    skill: skill);
                if (targets.Count == 0 && skill.TargetType != 5
                    && !HasCasterAreaTargets(actor, units, skill)) continue;
                if (skill.SkillType == 3)
                {
                    bool needsHealing = false;
                    foreach (FightBattleUnitState target in targets)
                        if (target.Hp < target.MaxHp) { needsHealing = true; break; }
                    if (!needsHealing) continue;
                }
                return skill;
            }
            return null;
        }

        private static bool ConditionAllows(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            int actorRound, int globalRound)
        {
            if (!skill.NeedConditionStatusMeetToRelease) return true;
            bool hasCondition = false;
            bool active = false;
            if (actor.Statuses != null)
                foreach (FightBattleStatusState status in actor.Statuses)
                {
                    if (status == null || status.ConditionalSkills == null) continue;
                    foreach (FightBattleStatusSkillCondition condition in status.ConditionalSkills)
                    {
                        if (condition == null || condition.SkillEntityId != skill.EntityId) continue;
                        hasCondition = true;
                        if (!status.SkillConditionRulesSupported) continue;
                        bool met = EvaluateConditions(status, actor, units, actorRound, globalRound);
                        if (met ? condition.IsActive : condition.IsNoMeetSetNegation
                            && !condition.IsActive) active = true;
                    }
                }
            if (hasCondition) return active;
            return SkillSummonPoolAvailable(actor.Id, skill.HitGraphEntities, units);
        }

        private static bool SkillSummonPoolAvailable(int ownerId,
            IReadOnlyList<FightBattleHitEntity> hitEntities,
            IReadOnlyList<FightBattleUnitState> units)
        {
            if (hitEntities == null) return false;
            var capacities = new Dictionary<int, int>();
            foreach (FightBattleHitEntity hit in hitEntities)
            {
                if (hit == null || !hit.IsSummon || hit.SummonPools == null) continue;
                foreach (FightBattleSummonPool pool in hit.SummonPools)
                {
                    if (pool == null || pool.MonsterId <= 0 || pool.MaxSummonCount <= 0) continue;
                    capacities.TryGetValue(pool.MonsterId, out int current);
                    capacities[pool.MonsterId] = checked(current + pool.MaxSummonCount);
                }
            }
            foreach (KeyValuePair<int, int> capacity in capacities)
                if (CountSummons(ownerId, capacity.Key, units) < capacity.Value) return true;
            return false;
        }

        private static bool EvaluateConditions(FightBattleStatusState status,
            FightBattleUnitState actor, IReadOnlyList<FightBattleUnitState> units,
            int actorRound, int globalRound)
        {
            if (status.SkillConditionRules == null || status.SkillConditionRules.Count == 0)
                return true;
            foreach (FightBattleSkillConditionRule rule in status.SkillConditionRules)
            {
                if (rule == null) return false;
                bool met;
                switch (rule.ConditionType)
                {
                    case 0:
                        if (rule.HpPerValue < 0f) continue;
                        met = Compare(actor.Hp / (double)Math.Max(1L, actor.MaxHp),
                            rule.HpPerValue, rule.ContrastiveRelation);
                        break;
                    case 1:
                        if (rule.Round < 0) continue;
                        int targetId = ResolveRoundTargetId(status, rule.RoundTargetType,
                            actor, units);
                        FightBattleUnitState roundTarget = FindUnit(units, targetId) ?? actor;
                        int currentRound = roundTarget.Id == actor.Id ? actorRound
                            : roundTarget.SkillState == null ? 0 : roundTarget.SkillState.Round;
                        met = Compare(currentRound, rule.Round, rule.ContrastiveRelation);
                        break;
                    case 3:
                        if (rule.Count < 0) continue;
                        met = Compare(CountSummons(status.OwnerId, rule.SummonId, units),
                            rule.Count, rule.ContrastiveRelation);
                        break;
                    case 4:
                        met = SummonPoolAvailable(status, units);
                        break;
                    default:
                        return false;
                }
                if (!met) return false;
            }
            return true;
        }

        private static bool SummonPoolAvailable(FightBattleStatusState status,
            IReadOnlyList<FightBattleUnitState> units)
        {
            if (status.SummonConditionPools == null || status.SummonConditionPools.Count == 0)
                return false;
            int totalCount = 0;
            int totalLimit = 0;
            bool hasAvailablePool = false;
            foreach (FightBattleSummonPool pool in status.SummonConditionPools)
            {
                if (pool == null || pool.MonsterId <= 0 || pool.MaxSummonCount <= 0) continue;
                int count = CountSummons(status.OwnerId, pool.MonsterId, units);
                totalCount += count;
                totalLimit += pool.MaxSummonCount;
                if (count < pool.MaxSummonCount) hasAvailablePool = true;
            }
            return hasAvailablePool && totalCount < totalLimit;
        }

        private static int CountSummons(int ownerId, int monsterId,
            IReadOnlyList<FightBattleUnitState> units)
        {
            int count = 0;
            foreach (FightBattleUnitState unit in units)
                if (IsSelectable(unit) && unit.MasterId == ownerId
                    && (monsterId <= 0 || unit.MonsterId == monsterId)) count++;
            return count;
        }

        private static int ResolveRoundTargetId(FightBattleStatusState status, int targetType,
            FightBattleUnitState actor, IReadOnlyList<FightBattleUnitState> units)
        {
            int targetId;
            switch (targetType)
            {
                case 1: targetId = status.CreatorId; break;
                case 2: targetId = status.OwnerId; break;
                case 3: targetId = status.CreatorId; break;
                case 4: targetId = status.OwnerId; break;
                default: targetId = status.OwnerId; break;
            }
            if (targetType == 3 || targetType == 4)
            {
                FightBattleUnitState master = FindUnit(units, targetId);
                if (master != null && master.MasterId > 0) targetId = master.MasterId;
            }
            return targetId > 0 ? targetId : actor.Id;
        }

        private static FightBattleUnitState FindUnit(IReadOnlyList<FightBattleUnitState> units,
            int id)
        {
            if (units == null || id <= 0) return null;
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.Id == id) return unit;
            return null;
        }

        private static bool Compare(double left, double right, int relation)
        {
            switch (relation)
            {
                case 0: return Math.Abs(left - right) < 0.000001;
                case 1: return left >= right;
                case 2: return left > right;
                case 3: return left <= right;
                case 4: return left < right;
                default: return false;
            }
        }

        public static List<FightBattleUnitState> SelectInSkillRange(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            int ridiculeTargetId = 0, bool confused = false, bool releaseWhileConfused = false)
        {
            List<FightBattleUnitState> candidates = Select(actor, units, skill.TargetType,
                ridiculeTargetId, confused, releaseWhileConfused, skill);
            if (skill.Range == null || skill.Range.Count == 0) return candidates;

            var inRange = new List<FightBattleUnitState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                FightBattleUnitState target = candidates[i];
                int dx = target.X - actor.X;
                int dy = target.Y - actor.Y;
                for (int rangeIndex = 0; rangeIndex < skill.Range.Count; rangeIndex++)
                    if (skill.Range[rangeIndex].X == dx && skill.Range[rangeIndex].Y == dy)
                    {
                        inRange.Add(target);
                        break;
                    }
            }
            return inRange;
        }

        /// <summary>
        /// Official hit Scope: the damage area is the configured <c>HitScopCfg.Pos</c> offsets around
        /// the hit anchor (target position for <c>OnTarget</c>, the caster position for
        /// <c>OnSourceCastPos</c> variants). Only units matching the skill's hit-target side are
        /// considered. Random hits keep the full area for their own random selection.
        /// </summary>
        public static List<FightBattleUnitState> SelectScopeTargets(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            FightBattleUnitState primary, FightBattleHitDefinition hit)
        {
            var result = new List<FightBattleUnitState>();
            if (actor == null || units == null || hit == null || hit.Scope == null
                || hit.Scope.Count == 0) return result;
            int centerX = hit.Anchor == 0 && primary != null ? primary.X : actor.X;
            int centerY = hit.Anchor == 0 && primary != null ? primary.Y : actor.Y;
            foreach (FightBattleUnitState unit in ScopeCandidates(actor, units,
                skill == null ? 1 : skill.TargetType, skill))
            {
                int dx = unit.X - centerX;
                int dy = unit.Y - centerY;
                bool inScope = false;
                for (int i = 0; i < hit.Scope.Count; i++)
                    if (hit.Scope[i].X == dx && hit.Scope[i].Y == dy) { inScope = true; break; }
                if (inScope) result.Add(unit);
            }
            result.Sort((left, right) => CompareCandidates(actor, left, right, skill, units));
            PrioritizeRidiculeSource(result, skill == null ? 1 : skill.TargetType,
                skill != null && skill.ReleaseWhileRidicule ? RidiculeSourceId(actor) : 0);
            if (hit.TargetMode != 1 && hit.TargetMode != 2 && result.Count > 1)
                result.RemoveRange(1, result.Count - 1);
            return result;
        }

        /// <summary>
        /// Point-target skills (<c>SkillTargetType=Point</c>) detonate their hit Scope at the chosen
        /// grid point. When no enemy stands inside the configured Range, official AI may still cast
        /// such a skill centred on the caster (for example 16100 with Range (0,0)); this checks whether
        /// doing so would hit at least one valid target.
        /// </summary>
        public static bool HasCasterAreaTargets(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill)
        {
            if (actor == null || skill == null || skill.SkillTargetType != 0) return false;
            List<FightBattleHitDefinition> hits = FightBattleHitCollector.CollectAll(skill);
            foreach (FightBattleHitDefinition hit in hits)
                if (hit != null && hit.Scope != null && hit.Scope.Count > 0
                    && SelectScopeTargets(actor, units, skill, null, hit).Count > 0) return true;
            return false;
        }

        private static List<FightBattleUnitState> ScopeCandidates(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, int targetType,
            FightBattleSkillDefinition skill = null)
        {
            var result = new List<FightBattleUnitState>();
            foreach (FightBattleUnitState unit in units)
            {
                if (!IsSelectable(unit) || HasIgnoredStatus(unit, skill)) continue;
                bool isRidiculeSource = targetType == 1 && skill != null
                    && skill.ReleaseWhileRidicule && unit.Id == RidiculeSourceId(actor);
                if (isRidiculeSource || MatchesTargetType(actor, unit, targetType,
                    skill != null && skill.ReleaseWhileConfusion
                        && HasActionStatus(actor, ConfusionActionType))) result.Add(unit);
            }
            PrioritizeRidiculeSource(result, targetType,
                skill != null && skill.ReleaseWhileRidicule ? RidiculeSourceId(actor) : 0);
            return result;
        }

        private static bool MatchesTargetType(FightBattleUnitState actor,
            FightBattleUnitState unit, int targetType, bool ignoreFaction)
        {
            bool sameSide = unit.Side == actor.Side;
            switch (targetType)
            {
                case 0: return unit.Id == actor.Id;
                case 1: return ignoreFaction || !sameSide;
                case 2: return ignoreFaction || sameSide;
                case 3: return unit.Id != actor.Id && (ignoreFaction || sameSide);
                case 8: return unit.Id != actor.Id && unit.CharacterType == 1
                    && (ignoreFaction || sameSide);
                case 9: return (unit.CharacterType == 1 || unit.CharacterType == 4)
                    && (ignoreFaction || sameSide);
                case 4: return true;
                case 6: return unit.Id == actor.MasterId;
                case 7: return unit.MasterId == actor.Id;
                default: return false;
            }
        }

        private static bool CanReleaseUnderControl(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
        {
            if (actor == null || skill == null || actor.Statuses == null) return true;
            foreach (FightBattleStatusState status in actor.Statuses)
            {
                if (status == null) continue;
                if (status.ActionType == RidiculeActionType && !skill.ReleaseWhileRidicule)
                    return false;
                if (status.ActionType == ConfusionActionType && !skill.ReleaseWhileConfusion)
                    return false;
            }
            return true;
        }

        private static bool HasActionStatus(FightBattleUnitState unit, int actionType)
        {
            if (unit == null || unit.Statuses == null) return false;
            foreach (FightBattleStatusState status in unit.Statuses)
                if (status != null && status.ActionType == actionType) return true;
            return false;
        }

        private static int RidiculeSourceId(FightBattleUnitState unit)
        {
            if (unit == null || unit.Statuses == null) return 0;
            FightBattleStatusState latest = null;
            foreach (FightBattleStatusState status in unit.Statuses)
                if (status != null && status.ActionType == RidiculeActionType
                    && status.CreatorId > 0 && (latest == null
                        || status.InstanceId > latest.InstanceId)) latest = status;
            return latest == null ? 0 : latest.CreatorId;
        }

        private static void PrioritizeRidiculeSource(List<FightBattleUnitState> candidates,
            int targetType, int targetId)
        {
            if (targetType != 1 || targetId <= 0 || candidates == null) return;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Id != targetId) continue;
                if (i == 0) return;
                FightBattleUnitState target = candidates[i];
                candidates.RemoveAt(i);
                candidates.Insert(0, target);
                return;
            }
        }

        public static List<FightBattleUnitState> SelectHitTargets(IReadOnlyList<FightBattleUnitState> candidates,
            int targetMode, int randomCount, FightRandomStream random)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var available = new List<FightBattleUnitState>();
            foreach (FightBattleUnitState candidate in candidates)
                if (IsSelectable(candidate)) available.Add(candidate);
            if (available.Count == 0) return available;
            if (targetMode == 1) return available;
            if (targetMode != 2) return new List<FightBattleUnitState> { available[0] };
            if (random == null) throw new ArgumentNullException(nameof(random));

            int count = Math.Min(Math.Max(1, randomCount), available.Count);
            var selected = new List<FightBattleUnitState>(count);
            for (int i = 0; i < count; i++)
            {
                int index = Math.Min((int)(random.Next() * available.Count), available.Count - 1);
                selected.Add(available[index]);
                available.RemoveAt(index);
            }
            return selected;
        }

        /// <summary>
        /// Official <c>FightSkillComponentInfo.Range</c> is ordered from near to far and targeting
        /// depends on that order to prefer closer targets. An explicit AI priority property still
        /// wins; in-range candidates are preferred over out-of-range ones so movement planning can
        /// use them, and all other ties fall back to the existing comparison.
        /// </summary>
        private static int CompareCandidates(FightBattleUnitState actor,
            FightBattleUnitState left, FightBattleUnitState right, FightBattleSkillDefinition skill,
            IReadOnlyList<FightBattleUnitState> units)
        {
            IReadOnlyList<int> priorities = PriorityTypes(skill);
            for (int i = 0; i < priorities.Count; i++)
            {
                int comparison = ComparePriority(priorities[i], actor, left, right, units, skill);
                if (comparison != 0) return comparison;
            }
            if (skill == null || skill.PriorityProperty > 0)
                return Compare(left, right, skill);
            if (skill.Range != null && skill.Range.Count > 0)
            {
                int leftIndex = RangeIndex(actor, left, skill.Range);
                int rightIndex = RangeIndex(actor, right, skill.Range);
                if (leftIndex >= 0 && rightIndex >= 0)
                {
                    if (leftIndex != rightIndex) return leftIndex.CompareTo(rightIndex);
                }
                else if (leftIndex >= 0) return -1;
                else if (rightIndex >= 0) return 1;
            }
            return Compare(left, right, skill);
        }

        private static IReadOnlyList<int> PriorityTypes(FightBattleSkillDefinition skill)
        {
            if (skill == null) return Array.Empty<int>();
            if (skill.UseLastAiPriority && skill.LastAiPriorityTypes != null
                && skill.LastAiPriorityTypes.Count > 0) return skill.LastAiPriorityTypes;
            return skill.AiPriorityTypes == null ? Array.Empty<int>() : skill.AiPriorityTypes;
        }

        private static int ComparePriority(int type, FightBattleUnitState actor,
            FightBattleUnitState left, FightBattleUnitState right,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill)
        {
            switch (type)
            {
                case 0: // ShorterMoveDist
                case 8: // LeastMoveNearTarget
                    return MoveDistanceToRange(actor, left, skill).CompareTo(
                        MoveDistanceToRange(actor, right, skill));
                case 1: // MoreHitCount
                    return HitCoverageCount(actor, right, units, skill).CompareTo(
                        HitCoverageCount(actor, left, units, skill));
                case 2: // LowerTargetHp
                    int leftFiltered = Contains(skill.HpLowerCharacterTypeFilter, left.CharacterType) ? 1 : 0;
                    int rightFiltered = Contains(skill.HpLowerCharacterTypeFilter, right.CharacterType) ? 1 : 0;
                    if (leftFiltered != rightFiltered) return leftFiltered.CompareTo(rightFiltered);
                    return left.Hp.CompareTo(right.Hp);
                case 4: // ShorterTargetDist
                    return Distance(actor, left).CompareTo(Distance(actor, right));
                case 5: // ShorterMovedTargetDist
                    return Math.Max(0, Distance(actor, left) - Math.Max(0, actor.MoveDist)).CompareTo(
                        Math.Max(0, Distance(actor, right) - Math.Max(0, actor.MoveDist)));
                case 7: // MoreHitStatusCount
                    return HitStatusCount(actor, right, units, skill).CompareTo(
                        HitStatusCount(actor, left, units, skill));
                case 10: // BiggerBodyRange
                    return BodyRange(right).CompareTo(BodyRange(left));
                case 11: // PriorGameCharacterType
                    return PreferredCharacterType(right, skill).CompareTo(
                        PreferredCharacterType(left, skill));
                case 14: // PriorCloserTeammate
                case 21: // ShorterFriendDistToTargetPos
                    return NearestFriendDistance(left, units, actor).CompareTo(
                        NearestFriendDistance(right, units, actor));
                case 15: // PriorPropCompare
                    return ComparePriorityProperty(left, right, skill);
                case 18: // PriorGameCharacterTypeGroups
                    return CharacterTypeGroup(left, skill).CompareTo(CharacterTypeGroup(right, skill));
                default:
                    return 0;
            }
        }

        private static int MoveDistanceToRange(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill)
        {
            if (skill == null || skill.Range == null || skill.Range.Count == 0) return 0;
            int best = Int32.MaxValue;
            foreach (FightGridPoint offset in skill.Range)
            {
                int destinationX = target.X - offset.X;
                int destinationY = target.Y - offset.Y;
                best = Math.Min(best, Math.Abs(destinationX - actor.X) + Math.Abs(destinationY - actor.Y));
            }
            return best;
        }

        private static int HitCoverageCount(FightBattleUnitState actor,
            FightBattleUnitState primary, IReadOnlyList<FightBattleUnitState> units,
            FightBattleSkillDefinition skill)
        {
            int count = 0;
            if (skill == null || skill.HitGraphEntities == null || units == null) return count;
            foreach (FightBattleHitEntity hit in skill.HitGraphEntities)
            {
                if (hit == null || hit.Scope == null || hit.Scope.Count == 0) continue;
                int centerX = hit.Anchor == 0 ? primary.X : actor.X;
                int centerY = hit.Anchor == 0 ? primary.Y : actor.Y;
                foreach (FightBattleUnitState unit in ScopeCandidates(actor, units,
                    skill.TargetType, skill))
                {
                    if (InScope(unit, centerX, centerY, hit.Scope)) count++;
                }
            }
            return count;
        }

        private static int HitStatusCount(FightBattleUnitState actor,
            FightBattleUnitState primary, IReadOnlyList<FightBattleUnitState> units,
            FightBattleSkillDefinition skill)
        {
            int count = 0;
            if (skill == null || skill.HitGraphEntities == null || units == null) return count;
            foreach (FightBattleHitEntity hit in skill.HitGraphEntities)
            {
                if (hit == null || hit.Scope == null || hit.Scope.Count == 0
                    || hit.AppliedStatuses == null || hit.AppliedStatuses.Count == 0) continue;
                int centerX = hit.Anchor == 0 ? primary.X : actor.X;
                int centerY = hit.Anchor == 0 ? primary.Y : actor.Y;
                foreach (FightBattleUnitState unit in ScopeCandidates(actor, units,
                    skill.TargetType, skill))
                {
                    if (!InScope(unit, centerX, centerY, hit.Scope)) continue;
                    foreach (FightBattleStatusApplication application in hit.AppliedStatuses)
                        if (application != null && application.StatusId > 0
                            && !HasStatus(unit, application.StatusId)) count++;
                }
            }
            return count;
        }

        private static bool InScope(FightBattleUnitState unit, int centerX, int centerY,
            IReadOnlyList<FightGridPoint> scope)
        {
            int dx = unit.X - centerX;
            int dy = unit.Y - centerY;
            if (unit.BodyOffsets != null)
                foreach (FightGridPoint bodyOffset in unit.BodyOffsets)
                    foreach (FightGridPoint point in scope)
                        if (point.X == dx + bodyOffset.X && point.Y == dy + bodyOffset.Y) return true;
            foreach (FightGridPoint point in scope)
                if (point.X == dx && point.Y == dy) return true;
            return false;
        }

        private static int ComparePriorityProperty(FightBattleUnitState left,
            FightBattleUnitState right, FightBattleSkillDefinition skill)
        {
            if (skill == null || skill.PriorityProperty <= 0) return 0;
            long leftProperty = Property(left, skill.PriorityProperty);
            long rightProperty = Property(right, skill.PriorityProperty);
            if (leftProperty == rightProperty) return 0;
            return skill.PriorityFindMax
                ? rightProperty.CompareTo(leftProperty)
                : leftProperty.CompareTo(rightProperty);
        }

        private static bool PreferredCharacterType(FightBattleUnitState unit,
            FightBattleSkillDefinition skill)
        {
            return skill != null && skill.PriorityCharacterType > 0
                && unit.CharacterType == skill.PriorityCharacterType;
        }

        private static int CharacterTypeGroup(FightBattleUnitState unit,
            FightBattleSkillDefinition skill)
        {
            if (skill == null || skill.PriorityCharacterTypeGroups == null) return Int32.MaxValue;
            for (int i = 0; i < skill.PriorityCharacterTypeGroups.Count; i++)
                if (Contains(skill.PriorityCharacterTypeGroups[i], unit.CharacterType)) return i;
            return Int32.MaxValue;
        }

        private static int NearestFriendDistance(FightBattleUnitState target,
            IReadOnlyList<FightBattleUnitState> units, FightBattleUnitState actor)
        {
            int best = Int32.MaxValue;
            if (units == null) return best;
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null || unit.Id == actor.Id || unit.Side != actor.Side || !IsSelectable(unit)) continue;
                best = Math.Min(best, Math.Abs(target.X - unit.X) + Math.Abs(target.Y - unit.Y));
            }
            return best;
        }

        private static int Distance(FightBattleUnitState left, FightBattleUnitState right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private static int BodyRange(FightBattleUnitState unit)
        {
            return unit.BodyOffsets == null ? 0 : unit.BodyOffsets.Count;
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
                if (values[i] == value) return true;
            return false;
        }

        private static bool HasIgnoredStatus(FightBattleUnitState unit,
            FightBattleSkillDefinition skill)
        {
            if (unit == null || skill == null || skill.IgnoreStatusIds == null
                || skill.IgnoreStatusIds.Count == 0 || unit.Statuses == null) return false;
            foreach (FightBattleStatusState status in unit.Statuses)
                if (status != null && Contains(skill.IgnoreStatusIds, status.ClassId)) return true;
            return false;
        }

        private static bool HasStatus(FightBattleUnitState unit, int statusId)
        {
            if (unit == null || unit.Statuses == null) return false;
            foreach (FightBattleStatusState status in unit.Statuses)
                if (status != null && status.ClassId == statusId) return true;
            return false;
        }

        private static int RangeIndex(FightBattleUnitState actor, FightBattleUnitState target,
            IReadOnlyList<FightGridPoint> range)
        {
            int dx = target.X - actor.X;
            int dy = target.Y - actor.Y;
            for (int i = 0; i < range.Count; i++)
                if (range[i].X == dx && range[i].Y == dy) return i;
            return -1;
        }

        private static int Compare(FightBattleUnitState left, FightBattleUnitState right,
            FightBattleSkillDefinition skill)
        {
            bool hasPriority = skill != null && skill.PriorityProperty > 0;
            if (hasPriority)
            {
                long leftProperty = Property(left, skill.PriorityProperty);
                long rightProperty = Property(right, skill.PriorityProperty);
                if (leftProperty != rightProperty)
                    return skill.PriorityFindMax
                        ? rightProperty.CompareTo(leftProperty)
                        : leftProperty.CompareTo(rightProperty);
                return left.Id.CompareTo(right.Id);
            }
            double leftHp = left.Hp / (double)Math.Max(1L, left.MaxHp);
            double rightHp = right.Hp / (double)Math.Max(1L, right.MaxHp);
            int hp = leftHp.CompareTo(rightHp);
            return hp != 0 ? hp : left.Id.CompareTo(right.Id);
        }

        private static long Property(FightBattleUnitState target, int propertyId)
        {
            if (target.Properties == null) return 0;
            for (int i = 0; i < target.Properties.Count; i++)
                if (target.Properties[i] != null && target.Properties[i].Id == propertyId)
                    return target.Properties[i].Value;
            return 0;
        }

        private static FightBattleSkillDefinition FindSkill(List<FightBattleSkillDefinition> skills, int slot)
        {
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null && skills[i].Slot == slot) return skills[i];
            return null;
        }
    }
}
