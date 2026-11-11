using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusLifeReductionState
    {
        public int ReduceRoundCount;
        public bool HasUnsupportedFilter;
        public List<FightGameplayTagClause> TargetTagQuery = new List<FightGameplayTagClause>();
        public List<string> ReducedStatusKeys = new List<string>();

        public FightBattleStatusLifeReductionState Copy()
        {
            var copy = (FightBattleStatusLifeReductionState)MemberwiseClone();
            copy.ReducedStatusKeys = new List<string>(ReducedStatusKeys);
            copy.TargetTagQuery = new List<FightGameplayTagClause>();
            foreach (var clause in TargetTagQuery)
                copy.TargetTagQuery.Add(new FightGameplayTagClause {Type=clause.Type, Tags=(long[])clause.Tags.Clone()});
            return copy;
        }
    }

    // Confirmed offline scheduling. Amount, replacement parameters and tag clauses come from
    // the original component; the client has no server implementation of this handler.
    public static class FightStatusLifeReduction
    {
        public static void OnApplied(FightBattleUnitState owner, FightBattleStatusState applied)
        {
            if (owner == null || owner.Ended || applied == null || !owner.Statuses.Contains(applied)) return;
            var statuses = new List<FightBattleStatusState>(owner.Statuses);
            foreach (var reducer in statuses)
            {
                if (reducer?.Components?.LifeReduction == null || reducer.ForbiddenType != 0
                    || !owner.Statuses.Contains(reducer)) continue;
                foreach (var target in statuses)
                    if (reducer == applied || target == applied) TryReduce(owner, reducer, target);
            }
        }

        internal static string StatusKey(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            if (status.Components.LifeReductionIdentity <= 0)
            {
                // Executor InstanceId values can be reused after removal. Keep a separate
                // persisted identity so a newly applied lifespan always receives its debit.
                foreach (var existing in owner.Statuses)
                    owner.LifeReductionStatusSerial = Math.Max(owner.LifeReductionStatusSerial,
                        existing?.Components?.LifeReductionIdentity ?? 0);
                status.Components.LifeReductionIdentity = checked(++owner.LifeReductionStatusSerial);
            }
            return status.Components.LifeReductionIdentity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void TryReduce(FightBattleUnitState owner, FightBattleStatusState reducer,
            FightBattleStatusState target)
        {
            var definition = reducer.Components.LifeReduction;
            if (target == null || target == reducer || target.DurationRound < 0
                || definition.ReduceRoundCount <= 0 || definition.HasUnsupportedFilter
                || !owner.Statuses.Contains(target) || definition.ReducedStatusKeys.Contains(StatusKey(owner, target))) return;
            foreach (var clause in definition.TargetTagQuery)
                if (target.Components?.GameplayTags == null || !clause.Matches(target.Components.GameplayTags)) return;
            // Save the receipt before End callbacks: nested applications cannot debit this pair twice.
            definition.ReducedStatusKeys.Add(StatusKey(owner, target));
            target.CurDurationRound = (int)Math.Max(int.MinValue,
                (long)target.CurDurationRound - definition.ReduceRoundCount);
            if (target.StartRound < 0) target.DurationPreparedBeforeStart = true;
            int round = owner.StatusRoundResolver == null ? owner.SkillState.Round : owner.StatusRoundResolver(target);
            if (FightStatusLifecycle.IsExpired(target, round)) FightStatusLifecycle.End(owner, target);
        }
    }
}
