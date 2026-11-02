using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private static bool IsPendingHpStart(FightBattleUnitState owner, FightBattleStatusState status)
        {
            return owner != null && !owner.Ended && status != null && !status.HpStartChecked
                && (status.HasHpLessThan && status.HpLessThanTryAtStart
                    || status.HpTriggerPer > 0f && status.HpTriggerAtStart || status.HasHpLimit);
        }

        private bool HasPendingHpStarts()
        {
            if (statusCatalog == null) return false;
            foreach (var owner in units)
                foreach (var status in owner.Statuses)
                    if (IsPendingHpStart(owner, status)) return true;
            return false;
        }

        private int StartHpStatus(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (!IsPendingHpStart(owner, status) || movementStartContext == null) return 0;
            status.HpStartChecked = true;
            FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            // The just-registered status's property changes precede its simulated HP change.
            FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            var simple = new List<FightBattleStatusStateTrigger>();
            var configured = new List<FightBattleStatusSkillInvocation>();
            int changed = FightStatusTriggerRuntime.Apply(owner, statusCatalog, true, simple,
                random, null, units, configured, status);
            foreach (var trigger in simple)
                configured.Add(new FightBattleStatusSkillInvocation
                {
                    Trigger = trigger, SourceUnitId = owner.Id, TargetUnitId = owner.Id,
                    TargetPositionUnitId = owner.Id, SelectTargets = true,
                    AllowDeadSource = status.HpLessThanInvokeOnDie && owner.Hp <= 0,
                    AllowDeadTarget = status.HpLessThanInvokeOnDie && owner.Hp <= 0
                });
            ExecuteHpStartupSkills(configured);
            return changed;
        }

        private void ExecuteHpStartupSkills(List<FightBattleStatusSkillInvocation> invocations)
        {
            if (invocations.Count == 0) return;
            var context = movementStartContext;
            FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            foreach (var invocation in invocations)
                invocation.Delay += Math.Max(0f, context.StatusStartTime);
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, context.Casts, context.DamageEvents,
                context.Movements, ref delay, out float duration, out float actionDuration);
            context.Duration = Math.Max(context.Duration, Math.Max(delay, duration));
            context.ActionDuration = Math.Max(context.ActionDuration, actionDuration);
        }

        private FightBattleExecutionResult ExecutePendingHpStarts(int sequence, int counter)
        {
            if (!HasPendingHpStarts()) return null;
            var before = new Dictionary<int, long>();
            foreach (var unit in units) before[unit.Id] = unit.Hp;
            var context = new MovementStartContext
            {
                Casts = new List<FightBattleSkillCastEvent>(), DamageEvents = new List<FightBattleDamageEvent>(),
                Movements = new List<FightBattleMovementEvent>()
            };
            movementStartContext = context;
            currentStepSpawnedUnitIds = new List<int>();
            currentStepStatusCustomActions = new List<FightBattleStatusCustomActionEvent>();
            currentStepDispersedUnitIds = new List<int>();
            FightBattleUnitState firstOwner = null;
            int changed = 0;
            try
            {
                do
                {
                    foreach (var owner in new List<FightBattleUnitState>(units))
                    {
                        var statuses = new List<FightBattleStatusState>(owner.Statuses);
                        // Preserve the original HP collector's reverse status order.
                        for (int index = statuses.Count - 1; index >= 0; index--)
                            if (owner.Statuses.Contains(statuses[index]) && IsPendingHpStart(owner, statuses[index]))
                            {
                                if (firstOwner == null) firstOwner = owner;
                                changed += StartHpStatus(owner, statuses[index]);
                            }
                    }
                    RebuildBattleProperties();
                    float delay = 0f;
                    ExecuteAccumulatedDamageEnds(context.Casts, context.DamageEvents, context.Movements,
                        ref delay, ref context.Duration, ref context.ActionDuration);
                    CollectStatusCustomActions(currentStepStatusCustomActions);
                    RefreshTurnUnits();
                    SpawnNextWaveIfCleared();
                    UpdateOutcome(Math.Max(1, turns.GlobalRound()));
                }
                while (!checkpoint.Finished && HasPendingHpStarts());
                bool hpChanged = false;
                foreach (var unit in units)
                    if (!before.TryGetValue(unit.Id, out long hp) || hp != unit.Hp) hpChanged = true;
                if (changed == 0 && context.Casts.Count == 0 && context.DamageEvents.Count == 0
                    && context.Movements.Count == 0 && currentStepStatusCustomActions.Count == 0 && !hpChanged)
                    return null;

                var targets = new List<FightBattleUnitState>();
                var targetIds = new HashSet<int>();
                foreach (var cast in context.Casts)
                    foreach (int id in cast.TargetIds)
                        if (targetIds.Add(id) && unitById.TryGetValue(id, out var target)) targets.Add(target);
                var step = CreateEvent(firstOwner ?? units[0], null, targets, before,
                    Math.Max(1, turns.GlobalRound()), context.Movements, context.Casts, context.DamageEvents,
                    currentStepSpawnedUnitIds, currentStepStatusCustomActions, context.Duration, context.ActionDuration);
                checkpoint.Counter = counter;
                checkpoint.RandomSeed = random.Seed;
                checkpoint.Receipts.Add(new FightBattleStepReceipt { Sequence = sequence, Counter = counter, Event = step });
                FightBattleCheckpointRules.Validate(checkpoint, true);
                return new FightBattleExecutionResult { Checkpoint = checkpoint, Event = step };
            }
            finally
            {
                movementStartContext = null;
                currentStepSpawnedUnitIds = null;
                currentStepStatusCustomActions = null;
                currentStepDispersedUnitIds = null;
            }
        }
    }
}
