using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private FightBattleUnitState petEnergyHpOwner;
        private FightBattleUnitState petEnergyHpCaster;

        private void SynchronizePetEnergyHp(FightBattleUnitState unit)
        {
            if (unit == null || petEnergyHpOwner == null || petEnergyHpCaster == null) return;
            if (ReferenceEquals(unit, petEnergyHpCaster))
            {
                petEnergyHpOwner.Hp = Math.Max(0L,
                    Math.Min(petEnergyHpOwner.MaxHp, petEnergyHpCaster.Hp));
                if (petEnergyHpOwner.Turn != null) petEnergyHpOwner.Turn.Hp = petEnergyHpOwner.Hp;
            }
            else if (ReferenceEquals(unit, petEnergyHpOwner))
            {
                petEnergyHpCaster.Hp = Math.Max(0L,
                    Math.Min(petEnergyHpCaster.MaxHp, petEnergyHpOwner.Hp));
                if (petEnergyHpCaster.Turn != null) petEnergyHpCaster.Turn.Hp = petEnergyHpCaster.Hp;
            }
        }

        private void RestoreHpChangeStackDefinition(FightBattleStatusState status)
        {
            if (!status.HasHpLessThan || status.HpLessThanChangeStackResolved) return;
            // Older checkpoints contain the listener and its counters but not ChangeStackCfg.
            // Restore only that missing definition, preserving the saved stack/count boundary.
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank,
                status.SourceProperties, replacementProperties: status.ReplacementProperties,
                blessPropertyScales: status.BlessPropertyScales);
            status.HpLessThanChangeStackRate = definition.HpLessThanChangeStackRate;
            status.HpLessThanChangeStackUnit = definition.HpLessThanChangeStackUnit;
            status.HpLessThanChangeStackResolved = true;
        }

        private static bool IsPendingHpStart(FightBattleUnitState owner, FightBattleStatusState status)
        {
            return owner != null && !owner.Ended && status != null && !status.HpStartChecked
                && (status.HasHpLessThan && status.HpLessThanTryAtStart
                    || status.HpTriggerPer > 0f && status.HpTriggerAtStart || status.HasHpLimit);
        }

        private bool HasPendingStatusStarts()
        {
            if (statusCatalog == null) return false;
            foreach (var owner in units)
                foreach (var status in owner.Statuses)
                    if (IsPendingHpStart(owner, status) || IsPendingRoundStart(owner, status)
                        || IsPendingStackCountStart(owner, status)) return true;
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
                random, null, units, configured, status, ExecuteHpStartupSkills);
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

        private void ApplyHpChangeTriggers(FightBattleUnitState changed, FightBattleSkillDefinition sourceSkill)
        {
            SynchronizePetEnergyHp(changed);
            if (statusCatalog == null) return;
            long hpIncreaseDelta = changed != null && changed.HpUpdateCaptured
                ? changed.HpIncreaseForLastUpdate : -1L;
            if (changed != null) changed.HpUpdateCaptured = false;
            var simple = new List<FightBattleStatusStateTrigger>();
            var configured = new List<FightBattleStatusSkillInvocation>();
            FightStatusTriggerRuntime.Apply(changed, statusCatalog, false, simple,
                random, sourceSkill, units, configured, executeHpSkills: ExecuteHpStartupSkills,
                hpIncreaseDelta: hpIncreaseDelta);
            foreach (var trigger in simple)
                configured.Add(new FightBattleStatusSkillInvocation
                {
                    Trigger = trigger, SourceUnitId = changed.Id, TargetUnitId = changed.Id,
                    TargetPositionUnitId = changed.Id, SelectTargets = true
                });
            ExecuteHpStartupSkills(configured);
            RebuildBattleProperties();
        }

        private void OnHpUpdated(FightBattleUnitState changed)
        {
            ApplyHpChangeTriggers(changed, null);
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

        private FightBattleExecutionResult ExecutePendingStatusStarts(int sequence, int counter)
        {
            if (!HasPendingStatusStarts()) return null;
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
                            if (owner.Statuses.Contains(statuses[index])
                                && (IsPendingHpStart(owner, statuses[index]) || IsPendingRoundStart(owner, statuses[index])
                                    || IsPendingStackCountStart(owner, statuses[index])))
                            {
                                if (firstOwner == null) firstOwner = owner;
                                changed += StartHpStatus(owner, statuses[index]);
                                changed += StartRoundStatus(owner, statuses[index]);
                                if (IsPendingStackCountStart(owner, statuses[index]))
                                    OnStatusStackChanged(statuses[index]);
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
                while (!checkpoint.Finished && HasPendingStatusStarts());
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
