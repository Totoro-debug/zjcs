using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Base status lifecycle from <c>FightStatusComponentInfo</c>: round duration, stacking and the
    /// official same-status merge rules. Status component effects (props, shields, triggers) are not
    /// part of this type; it only owns the shared lifetime state.
    /// </summary>
    public static class FightStatusLifecycle
    {
        public const int TargetNone = 0;
        public const int TargetCreator = 1;
        public const int TargetApplicator = 2;
        public const int TargetCreatorMaster = 3;
        public const int TargetApplicatorMaster = 4;

        public const int TimingStart = 0;
        public const int TimingEnd = 1;

        public const int BehaviourParallel = 0;
        public const int BehaviourImmediate = 1;
        public const int BehaviourDiscard = 2;

        /// <summary>Initializes the official round bookkeeping when the status is first applied.</summary>
        public static void Initialize(FightBattleStatusState status, int round)
        {
            if (status == null || status.StartRound >= 0) return;
            status.StartRound = round;
            status.CurDurationRound = status.DurationRound;
        }

        /// <summary>
        /// Remaining rounds, matching <c>FightStatusController</c>: <c>Max(0, CurDurationRound -
        /// (round - StartRound))</c>; negative configured duration means infinite (-1).
        /// </summary>
        public static int LeftRound(FightBattleStatusState status, int round)
        {
            if (status == null) return 0;
            if (status.DurationRound < 0) return -1;
            int start = status.StartRound < 0 ? round : status.StartRound;
            return Math.Max(0, status.CurDurationRound - (round - start));
        }

        public static bool IsExpired(FightBattleStatusState status, int round)
        {
            return status != null && status.DurationRound >= 0 && LeftRound(status, round) <= 0;
        }

        /// <summary>
        /// Official stacking extension (<c>UpdateStatusRound</c>): the remaining time is added to the
        /// configured duration and capped by <c>MaxAddDurationRound</c>.
        /// </summary>
        public static void ExtendDurationOnStack(FightBattleStatusState status, int round)
        {
            if (status == null || status.DurationRound < 0 || !status.IsAddDurationRound) return;
            int start = status.StartRound < 0 ? round : status.StartRound;
            int remaining = status.CurDurationRound - (round - start);
            status.CurDurationRound = Math.Min(status.DurationRound + remaining,
                status.MaxAddDurationRound > 0 ? status.MaxAddDurationRound : status.DurationRound + remaining);
            status.StartRound = round;
        }

        public static void ResetDurationOnStack(FightBattleStatusState status, int round)
        {
            if (status == null) return;
            status.StartRound = round;
            status.CurDurationRound = status.DurationRound;
        }

        /// <summary>Adds stacks up to <see cref="FightBattleStatusState.MaxStackedCount"/>.</summary>
        public static int AddStack(FightBattleStatusState status, int delta)
        {
            if (status == null || !status.IsOpenStack || delta == 0) return 0;
            int previous = status.StackCount;
            long next = previous + (long)delta;
            if (status.MaxStackedCount > 0) next = Math.Min(next, status.MaxStackedCount);
            if (next < 0) next = 0;
            status.StackCount = (int)next;
            return status.StackCount - previous;
        }

        public static int ResolveTargetUnitId(FightBattleStatusState status,
            IReadOnlyList<FightBattleUnitState> units)
        {
            if (status == null) return 0;
            switch (status.RoundTarget)
            {
                case TargetCreator:
                    return status.CreatorId;
                case TargetApplicator:
                    return status.OwnerId;
                case TargetCreatorMaster:
                    return MasterOf(status.CreatorId, units);
                case TargetApplicatorMaster:
                    return MasterOf(status.OwnerId, units);
                default:
                    return 0;
            }
        }

        private static int MasterOf(int unitId, IReadOnlyList<FightBattleUnitState> units)
        {
            if (units == null || unitId <= 0) return 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].Id == unitId) return units[i].MasterId;
            return 0;
        }

        /// <summary>
        /// Applies a status template to a unit with the official same-status behaviour:
        /// <c>Parallel</c> keeps both, <c>Immediate</c> replaces the old instance, <c>Discard</c>
        /// keeps the old instance. Open stacking adds stacks to the existing instance instead.
        /// </summary>
        public static FightBattleStatusState Apply(FightBattleUnitState unit,
            FightBattleStatusState template, int round, int instanceId)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (unit.Statuses == null) unit.Statuses = new List<FightBattleStatusState>();
            FightBattleStatusState existing = null;
            foreach (FightBattleStatusState candidate in unit.Statuses)
                if (candidate != null && candidate.ClassId == template.ClassId) { existing = candidate; break; }
            if (existing != null)
            {
                if (existing.IsOpenStack)
                {
                    AddStack(existing, template.StackCount > 0 ? template.StackCount : 1);
                    if (existing.IsAddDurationRound) ExtendDurationOnStack(existing, round);
                    else if (existing.IsResetDurationWhenStack) ResetDurationOnStack(existing, round);
                    return existing;
                }
                if (existing.ActionBehOfSameStatus == BehaviourDiscard) return existing;
                if (existing.ActionBehOfSameStatus == BehaviourImmediate) unit.Statuses.Remove(existing);
            }
            var applied = new FightBattleStatusState
            {
                InstanceId = instanceId,
                ClassId = template.ClassId,
                StatusType = template.StatusType,
                DurationRound = template.DurationRound,
                CurDurationRound = template.DurationRound,
                StartRound = round,
                RoundTarget = template.RoundTarget,
                RoundUpdateTiming = template.RoundUpdateTiming,
                RemoveAtRoundTargetDestroy = template.RemoveAtRoundTargetDestroy,
                ActionBehOfSameStatus = template.ActionBehOfSameStatus,
                CanDispersed = template.CanDispersed,
                IsAddDurationRound = template.IsAddDurationRound,
                MaxAddDurationRound = template.MaxAddDurationRound,
                IsResetDurationWhenStack = template.IsResetDurationWhenStack,
                ScalePropWhenChangeStacked = template.ScalePropWhenChangeStacked,
                ApplyIgnoreResilience = template.ApplyIgnoreResilience,
                ForbiddenOnApplicatorDie = template.ForbiddenOnApplicatorDie,
                FollowRoundUpdateTimingOnGlobalRound = template.FollowRoundUpdateTimingOnGlobalRound,
                IsFixedProp = template.IsFixedProp,
                StackCount = template.StackCount,
                AddStackedCount = template.AddStackedCount,
                AddStackedRandomMinCount = template.AddStackedRandomMinCount,
                AddStackedRandomMaxCount = template.AddStackedRandomMaxCount,
                MaxStackedCount = template.MaxStackedCount,
                IsOpenStack = template.IsOpenStack,
                OwnerId = unit.Id,
                CreatorId = template.CreatorId,
                RootSkillId = template.RootSkillId,
                ApplyingSkillItemId = template.ApplyingSkillItemId,
                FalloffTarget = template.FalloffTarget,
                DamageId = template.DamageId,
                RootSkillIds = template.RootSkillIds == null ? Array.Empty<int>() : (int[])template.RootSkillIds.Clone(),
                SkillEntityClassId = template.SkillEntityClassId,
                IsCheckSkillTrigger = template.IsCheckSkillTrigger,
                RemoveOnSourceSkillStart = template.RemoveOnSourceSkillStart,
                NumOfStart = template.NumOfStart,
                MaxFalloffCount = template.MaxFalloffCount,
                FalloffPercent = template.FalloffPercent,
                Rate = template.Rate,
                Scope = template.Scope
            };
            unit.Statuses.Add(applied);
            return applied;
        }

        /// <summary>
        /// Expires statuses whose official round target is the acting unit and whose update timing
        /// matches. Returns the number of removed statuses.
        /// </summary>
        public static int TickTurn(FightBattleUnitState actor, IReadOnlyList<FightBattleUnitState> units,
            int round, int timing)
        {
            if (actor == null || actor.Statuses == null) return 0;
            int removed = 0;
            for (int i = actor.Statuses.Count - 1; i >= 0; i--)
            {
                FightBattleStatusState status = actor.Statuses[i];
                if (status == null) continue;
                if (status.StartRound < 0) Initialize(status, round);
                if (status.RoundUpdateTiming != timing) continue;
                if (ResolveTargetUnitId(status, units) != actor.Id) continue;
                if (!IsExpired(status, round)) continue;
                actor.Statuses.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// Removes statuses configured with <c>RemoveAtRoundTargetDestroy</c> when their round target
        /// no longer exists or is dead. Returns the number of removed statuses.
        /// </summary>
        public static int RemoveDestroyedTargets(IReadOnlyList<FightBattleUnitState> units)
        {
            if (units == null) return 0;
            int removed = 0;
            for (int u = 0; u < units.Count; u++)
            {
                FightBattleUnitState unit = units[u];
                if (unit == null || unit.Statuses == null) continue;
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    FightBattleStatusState status = unit.Statuses[i];
                    if (status == null || !status.RemoveAtRoundTargetDestroy
                        || status.RoundTarget == TargetNone) continue;
                    int targetId = ResolveTargetUnitId(status, units);
                    FightBattleUnitState target = null;
                    for (int t = 0; t < units.Count; t++)
                        if (units[t] != null && units[t].Id == targetId) { target = units[t]; break; }
                    if (target != null && target.Hp > 0) continue;
                    unit.Statuses.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }
    }
}
