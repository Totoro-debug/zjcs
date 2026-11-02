using System;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        public FightBattleExecutionResult Step(int sequence, int counter)
        {
            foreach (var unit in units)
                if (unit.StatusStarted != null || unit.StatusEnded != null)
                    throw new InvalidOperationException("A battle unit is already executing a step.");
            foreach (var unit in units)
            {
                unit.StatusStarted = OnStatusStarted;
                unit.StatusEnded = OnStatusEnded;
            }
            try { return StepCore(sequence, counter); }
            finally
            {
                // Include units summoned during this step. No delegate or executor is retained
                // by the checkpoint, including when a configuration fails during a nested cast.
                foreach (var unit in units)
                {
                    unit.StatusStarted = null;
                    unit.StatusEnded = null;
                }
                movementStartContext = null;
            }
        }

        private void OnStatusEnded(FightBattleUnitState owner, FightBattleStatusState ended)
        {
            if (movementStartContext == null || statusCatalog == null || owner == null
                || owner.Ended || ended == null || ended.ActionType != 70) return;
            foreach (var listener in new System.Collections.Generic.List<FightBattleStatusState>(owner.Statuses))
            {
                if (listener == null || !listener.HasHpLessThan
                    || !listener.HpLessThanCheckExistForbidden || !owner.Statuses.Contains(listener)) continue;
                FightStatusTriggerRuntime.Apply(owner, statusCatalog, false, random: random,
                    units: units, startingStatus: listener, executeHpSkills: ExecuteHpStartupSkills);
            }
        }

        private void OnStatusStarted(FightBattleUnitState owner, FightBattleStatusState status)
        {
            var context = movementStartContext;
            if (context == null || statusCatalog == null || status == null
                || owner == null || owner.Ended) return;

            // FightStatusComponentImplement.Start initializes props, registers the status,
            // initializes its round clock, then fires status start. Stacking/discarding an
            // existing entity does not enter this callback.
            FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            StartHpStatus(owner, status);
            if (!status.HasMoveNear || !status.MoveNearTryAtStart || !status.MoveNearRulesSupported
                || owner.Hp <= 0 || !owner.Statuses.Contains(status)) return;
            var invocations = FightStatusTriggerRuntime.CollectMoveNearSkills(owner, units,
                FightStatusTriggerRuntime.MoveNearEventStart, null, random, status);
            if (invocations.Count == 0) return;
            FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            foreach (var invocation in invocations)
                invocation.Delay += Math.Max(0f, context.StatusStartTime);
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, context.Casts, context.DamageEvents,
                context.Movements, ref delay, out float duration, out float actionDuration);
            context.Duration = Math.Max(context.Duration, Math.Max(delay, duration));
            context.ActionDuration = Math.Max(context.ActionDuration, actionDuration);
        }
    }
}
