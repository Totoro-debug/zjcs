using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusDamageReducePerInfo
    {
        public float Rate = 1f;
        public bool Block;
        public bool Crit;
        public bool Damage = true;
        public bool Cure;
        public int RangeCondition;
        public int SkillTargetType = 4;
        public int ConditionCount = 1;
        public int MaxCount = -1;
        public bool AutoDestroy;
        public bool IsMutexByBetter;
        public bool ScalePropByStatusTypeCount;
        public bool Supported = true;
        public int ConditionProgress;
        public int TriggeredCount;
        public float ActiveSkillScale = 1f;
        public float NotActiveSkillScale = 1f;
        public FightBattleStatusTypeCountFilter StatusType = new FightBattleStatusTypeCountFilter();

        public FightBattleStatusDamageReducePerInfo Clone()
        {
            return new FightBattleStatusDamageReducePerInfo
            {
                Rate = Rate,
                Block = Block,
                Crit = Crit,
                Damage = Damage,
                Cure = Cure,
                RangeCondition = RangeCondition,
                SkillTargetType = SkillTargetType,
                ConditionCount = ConditionCount,
                MaxCount = MaxCount,
                AutoDestroy = AutoDestroy,
                IsMutexByBetter = IsMutexByBetter,
                ScalePropByStatusTypeCount = ScalePropByStatusTypeCount,
                Supported = Supported,
                ConditionProgress = ConditionProgress,
                TriggeredCount = TriggeredCount,
                ActiveSkillScale = ActiveSkillScale,
                NotActiveSkillScale = NotActiveSkillScale,
                StatusType = StatusType == null ? null : StatusType.Clone()
            };
        }
    }

    [Serializable]
    public sealed class FightBattleStatusTypeCountFilter
    {
        public bool IsCondition = true;
        public int Target;
        public List<int> AnyStatusTypes = new List<int>();
        public List<int> AnyActionTypes = new List<int>();
        public int CountType = 1;
        public int MaxStatusCount = -1;
        public float Unit = 1f;
        public bool IsAdditionCount;
        public bool IsOpenZeroCheckWhenCalcStatus;
        public bool IsCalculateStack;

        public FightBattleStatusTypeCountFilter Clone()
        {
            return new FightBattleStatusTypeCountFilter
            {
                IsCondition = IsCondition,
                Target = Target,
                AnyStatusTypes = AnyStatusTypes == null ? new List<int>() : new List<int>(AnyStatusTypes),
                AnyActionTypes = AnyActionTypes == null ? new List<int>() : new List<int>(AnyActionTypes),
                CountType = CountType,
                MaxStatusCount = MaxStatusCount,
                Unit = Unit,
                IsAdditionCount = IsAdditionCount,
                IsOpenZeroCheckWhenCalcStatus = IsOpenZeroCheckWhenCalcStatus,
                IsCalculateStack = IsCalculateStack
            };
        }
    }

    public static class FightBattleStatusDamageReducePerRuntime
    {
        public static double Resolve(FightBattleUnitState source, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightDamageTypeData damageType, bool isCure,
            FightRandomStream random)
        {
            if (target == null || target.Statuses == null) return 0.0;
            var remove = new List<FightBattleStatusState>();
            double additive = 0.0;
            double bestMutex = 0.0;
            bool hasMutex = false;
            foreach (FightBattleStatusState status in target.Statuses)
            {
                FightBattleStatusDamageReducePerInfo info = status == null ? null : status.DamageReducePer;
                if (info == null || !info.Supported || !Eligible(info, source, target, skill,
                    damageType, isCure) || info.MaxCount >= 0 && info.TriggeredCount >= info.MaxCount)
                    continue;

                double reduction;
                if (!TryGetReduction(status, info, source, target, random, out reduction)) continue;
                reduction *= skill != null && skill.SkillType == 0
                    ? info.ActiveSkillScale : info.NotActiveSkillScale;
                if (info.IsMutexByBetter)
                {
                    if (!hasMutex || reduction > bestMutex) bestMutex = reduction;
                    hasMutex = true;
                }
                else additive += reduction;

                if (info.AutoDestroy && info.MaxCount >= 0
                    && info.TriggeredCount >= info.MaxCount)
                    remove.Add(status);
            }
            foreach (FightBattleStatusState status in remove) target.Statuses.Remove(status);
            return additive + (hasMutex ? bestMutex : 0.0);
        }

        private static bool Eligible(FightBattleStatusDamageReducePerInfo info,
            FightBattleUnitState source, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightDamageTypeData damageType, bool isCure)
        {
            if (isCure ? !info.Cure : !info.Damage) return false;
            if (!isCure && (damageType.Blinding || damageType.Dodge || damageType.Immunity))
                return false;
            if (info.Block && !damageType.Block || info.Crit && !damageType.Critical) return false;
            if (skill == null || info.SkillTargetType != 4
                && skill.TargetType != info.SkillTargetType) return false;
            int distance = source == null ? 0
                : Math.Abs(source.X - target.X) + Math.Abs(source.Y - target.Y);
            if (info.RangeCondition == 1 && distance > 1) return false;
            if (info.RangeCondition == 2 && distance <= 1) return false;
            return true;
        }

        private static bool TryGetReduction(FightBattleStatusState ownerStatus,
            FightBattleStatusDamageReducePerInfo info,
            FightBattleUnitState source, FightBattleUnitState target, FightRandomStream random,
            out double reduction)
        {
            reduction = 0.0;
            float rate = Math.Max(0f, Math.Min(1f, info.Rate));
            if (rate < 1f && (random == null || random.Next() >= rate)) return false;
            if (++info.ConditionProgress < Math.Max(1, info.ConditionCount)) return false;
            info.ConditionProgress = 0;
            info.TriggeredCount++;

            FightBattleStatusTypeCountFilter filter = info.StatusType;
            int count = 1;
            if (info.ScalePropByStatusTypeCount)
            {
                FightBattleUnitState filterUnit = FilterTarget(filter == null ? 0 : filter.Target,
                    ownerStatus, source, target);
                if (filterUnit == null) return true;
                count = CountStatuses(filter, filterUnit.Statuses);
                if (filter != null && filter.IsOpenZeroCheckWhenCalcStatus && count == 0)
                    return true;
            }
            if (info.ScalePropByStatusTypeCount && filter != null)
                reduction = filter.IsAdditionCount
                    ? 1.0 + filter.Unit * count : filter.Unit * count;
            else reduction = filter == null ? 1.0 : filter.Unit;
            return true;
        }

        private static FightBattleUnitState FilterTarget(int targetType,
            FightBattleStatusState ownerStatus, FightBattleUnitState source,
            FightBattleUnitState target)
        {
            switch (targetType)
            {
                case 0: return null;
                case 1: return source;
                case 2: return target;
                case 3:
                    if (ownerStatus == null) return null;
                    if (source != null && ownerStatus.CreatorId == source.Id) return source;
                    if (target != null && ownerStatus.CreatorId == target.Id) return target;
                    return null;
                case 5: return target;
                case 6: return target;
                default: return null;
            }
        }

        private static int CountStatuses(FightBattleStatusTypeCountFilter filter,
            List<FightBattleStatusState> statuses)
        {
            if (filter == null || !filter.IsCondition) return 1;
            if (filter.AnyActionTypes != null && filter.AnyActionTypes.Count > 0
                && statuses == null) return 0;
            var entityIds = filter.CountType == 2 ? new HashSet<int>() : null;
            int count = 0;
            if (statuses != null)
            {
                foreach (FightBattleStatusState status in statuses)
                {
                    if (status == null || !Matches(filter.AnyStatusTypes, status.StatusType)
                        || !Matches(filter.AnyActionTypes, status.ActionType)) continue;
                    if (filter.CountType == 0) continue;
                    if (filter.CountType == 2)
                    {
                        entityIds.Add(status.ClassId);
                        count = entityIds.Count;
                    }
                    else count += filter.IsCalculateStack ? Math.Max(1, status.StackCount) : 1;
                }
            }
            if (filter.MaxStatusCount >= 0) count = Math.Min(count, filter.MaxStatusCount);
            return count;
        }

        private static bool Matches(List<int> values, int value)
        {
            return values == null || values.Count == 0 || values.Contains(value);
        }
    }
}
