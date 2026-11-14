using System;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        public FightBattleExecutionResult Step(int sequence, int counter)
        {
            foreach (var unit in StatusOwners())
                if (unit.StatusStarted != null || unit.StatusEnded != null || unit.StatusRefreshed != null || unit.StatusForbiddenChanged != null
                    || unit.HpUpdated != null || unit.HpIncreased != null || unit.StatusRoundResolver != null
                    || unit.RoundStartClockResolver != null || unit.StatusApplicationRejected != null)
                    throw new InvalidOperationException("A battle unit is already executing a step.");
            bool mutexChanged = false;
            foreach (var unit in StatusOwners())
            {
                foreach (var status in unit.Statuses) RestoreStackCountDefinition(status);
                mutexChanged |= SynchronizeDeathForbidden(unit);
                mutexChanged |= statusMutex.Synchronize(unit);
            }
            if (mutexChanged && statusCatalog != null)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            foreach (var unit in StatusOwners())
            {
                unit.StatusStarted = OnStatusStarted;
                unit.StatusApplicationRejected = template => RejectStatusApplication(unit, template);
                unit.StatusEnded = OnStatusEnded;
                unit.StatusRefreshed = OnStatusViewRefreshed;
                unit.HpUpdated = OnHpUpdated;
                unit.HpIncreased = OnHpIncreased;
                unit.StatusForbiddenChanged = OnStatusForbiddenChanged;
                unit.StatusRoundResolver = StatusClockRound;
                unit.RoundStartClockResolver = RoundStartClockRound;
                foreach (var status in unit.Statuses) status.StackChanged = OnStatusStackChanged;
            }
            try
            {
                BeginGridViewEvents();
                BeginStatusViewEvents();
                BeginUnitEndEvents();
                BeginUnitLifeEvents();
                BeginStatusSecondsHits();
                CleanupGridItems();
                var result = StepCore(sequence, counter);
                if (!result.Replayed)
                {
                    CompleteStatusViewEvents(result.Event);
                    CompleteUnitEndEvents(result.Event);
                    CompleteUnitLifeEvents(result.Event);
                    CompleteStatusSecondsHits();
                }
                return result;
            }
            finally
            {
                ClearGridViewEvents();
                ClearStatusViewEvents();
                unitEndEvents = null;
                ClearUnitLifeEvents();
                ClearStatusSecondsHits();
                // Include units summoned during this step. No delegate or executor is retained
                // by the checkpoint, including when a configuration fails during a nested cast.
                foreach (var unit in StatusOwners(true))
                {
                    unit.StatusStarted = null;
                    unit.StatusApplicationRejected = null;
                    unit.StatusEnded = null;
                    unit.StatusRefreshed = null;
                    unit.HpUpdated = null;
                    unit.HpIncreased = null;
                    unit.StatusForbiddenChanged = null;
                    unit.StatusRoundResolver = null;
                    unit.RoundStartClockResolver = null;
                    foreach (var status in unit.Statuses) status.StackChanged = null;
                }
                movementStartContext = null;
                roundStartNotForbiddenListener = null;
            }
        }

        private void OnStatusEnded(FightBattleUnitState owner, FightBattleStatusState ended)
        {
            RecordUnitEnd(owner, ended);
            RecordGridStatus(owner, ended, FightBattleGridViewEventKind.StatusEnd);
            RecordStatusView(owner, ended, FightBattleStatusViewEventKind.End);
            if (ended != null) ended.StackChanged = null;
            statusMutex.Remove(owner, ended);
            if (statusCatalog != null && ended != null && ended.HasPropertyComponent)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            if (movementStartContext != null && statusCatalog != null && owner != null && ended != null)
            {
                var logicInvocations = FightStatusLogicRuntime.Apply(owner, ended,
                    FightStatusLogicEvent.StatusEnd, owner, owner, null, null, units,
                    statusCatalog, random);
                if (logicInvocations.Count > 0)
                {
                    float logicDelay = 0f;
                    ExecuteConfiguredStatusSkills(logicInvocations, movementStartContext.Casts,
                        movementStartContext.DamageEvents, movementStartContext.Movements,
                        ref logicDelay, out float logicDuration, out float logicActionDuration);
                    movementStartContext.Duration = Math.Max(movementStartContext.Duration,
                        Math.Max(logicDelay, logicDuration));
                    movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration,
                        logicActionDuration);
                }
            }
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
            if (status != null) status.StackChanged = OnStatusStackChanged;
            InitializeStatusDeathForbidden(owner, status);
            statusMutex.Add(owner, status);
            if (statusCatalog != null && status != null && status.HasPropertyComponent)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            RecordGridStatus(owner, status, FightBattleGridViewEventKind.StatusStart);
            RecordStatusView(owner, status, FightBattleStatusViewEventKind.Start);
            var context = movementStartContext;
            if (context == null || statusCatalog == null || status == null
                || owner == null || owner.Ended) return;

            // FightStatusComponentImplement.Start initializes props, registers the status,
            // initializes its round clock, then fires status start. Stacking/discarding an
            // existing entity does not enter this callback.
            FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            InitializeRoundStartDelay(status);
            var logicInvocations = new System.Collections.Generic.List<FightBattleStatusSkillInvocation>();
            if (!status.LogicSelfInitialized)
            {
                logicInvocations.AddRange(FightStatusLogicRuntime.Apply(owner, status,
                    FightStatusLogicEvent.StatusSelfInit, owner, owner, null, null, units,
                    statusCatalog, random));
                status.LogicSelfInitialized = true;
            }
            logicInvocations.AddRange(FightStatusLogicRuntime.Apply(owner, status,
                FightStatusLogicEvent.StatusStart, owner, owner, null, null, units,
                statusCatalog, random));
            if (logicInvocations.Count > 0)
            {
                float logicDelay = Math.Max(0f, context.StatusStartTime);
                ExecuteConfiguredStatusSkills(logicInvocations, context.Casts, context.DamageEvents,
                    context.Movements, ref logicDelay, out float logicDuration,
                    out float logicActionDuration);
                context.Duration = Math.Max(context.Duration, Math.Max(logicDelay, logicDuration));
                context.ActionDuration = Math.Max(context.ActionDuration, logicActionDuration);
            }
            status.LogicStartInitialized = true;
            FightStatusLifeReduction.OnApplied(owner, status);
            if (!owner.Statuses.Contains(status) || owner.Ended) return;
            ApplyMissingStatusStart(owner, status);
            StartMoveScope(owner, status);
            StartHpStatus(owner, status);
            StartRoundStatus(owner, status);
            OnStatusStackChanged(status);
            if (!status.HasMoveNear || !status.MoveNearTryAtStart || !status.MoveNearRulesSupported
                || owner.Hp <= 0 && !IsGridSkillSource(owner) || !owner.Statuses.Contains(status)) return;
            var invocations = FightStatusTriggerRuntime.CollectMoveNearSkills(owner, StatusOwners(),
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

        private void OnStatusStackChanged(FightBattleStatusState status)
        {
            if (status != null && unitById.TryGetValue(status.OwnerId, out var statusOwner))
            {
                RecordGridStatus(statusOwner, status, FightBattleGridViewEventKind.StatusUpdate);
                RecordStatusView(statusOwner, status, FightBattleStatusViewEventKind.Update);
            }
            if (status == null || !status.HasStackCountTrigger
                || !unitById.TryGetValue(status.OwnerId, out var owner)) return;
            status.StackCountStartChecked = true;
            FightStatusTriggerRuntime.ApplyStackCount(owner, status, statusCatalog, units, random,
                movementStartContext == null ? null : (Action<System.Collections.Generic.List<FightBattleStatusSkillInvocation>>)ExecuteHpStartupSkills);
            ApplyMissingStatusStackChange(status);
        }
    }
}
