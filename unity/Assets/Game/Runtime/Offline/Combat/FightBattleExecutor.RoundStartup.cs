using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RestoreRoundStartDefinition(FightBattleStatusState status)
        {
            if (!status.HasRoundStartTrigger
                || status.RoundStartDefinitionResolved && status.RoundStartTargetRulesResolved
                    && status.RoundStartPropTargetRulesResolved && status.RoundStartMeetRulesResolved
                    && status.RoundStartRandomRulesResolved && status.RoundStartSourceRulesResolved
                    && status.RoundStartMergeRulesResolved) return;
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties, blessPropertyScales: status.BlessPropertyScales);
            status.RoundStartRulesSupported = definition.RoundStartRulesSupported;
            status.RoundStartSourceRulesResolved = true;
            if (!status.RoundStartMergeRulesResolved)
            {
                status.RoundStartMergeMaxCountOnStackRound = definition.RoundStartMergeMaxCountOnStackRound;
                // No previous duration-merge implementation recorded extra stack rounds.
                // Preserve actual calls; missing legacy stack-round history starts at one.
                status.RoundStartStackRoundCount = Math.Max(1, status.RoundStartStackRoundCount);
                status.RoundStartMergeRulesResolved = true;
            }
            if (!status.RoundStartRandomRulesResolved)
            {
                status.RoundStartRandomOneStatus = definition.RoundStartRandomOneStatus;
                status.RoundStartRandomRulesResolved = true;
            }
            if (!status.RoundStartMeetRulesResolved)
            {
                status.RoundStartResetMeetCountOnInvoke = definition.RoundStartResetMeetCountOnInvoke;
                // Older supported definitions only had MeetCount=1. A recorded
                // invocation already proves that this non-resetting threshold was met.
                if (status.RoundStartMeetCount == 1 && status.RoundStartTriggeredCount > 0
                    && !status.RoundStartResetMeetCountOnInvoke) status.RoundStartMeetProgress = 1;
                status.RoundStartMeetRulesResolved = true;
            }
            if (!status.RoundStartPropTargetRulesResolved)
            {
                for (int i = 0; i < status.RoundStartSkills.Count && i < definition.RoundStartSkills.Count; i++)
                {
                    var trigger = status.RoundStartSkills[i];
                    var original = definition.RoundStartSkills[i];
                    if (trigger.EntityId != original.EntityId || trigger.Source != original.Source
                        || trigger.Target != original.Target) continue;
                    trigger.PropTarget = original.PropTarget?.Copy();
                    trigger.TargetWithoutStatusIds = original.TargetWithoutStatusIds == null
                        ? null : (int[])original.TargetWithoutStatusIds.Clone();
                }
                status.RoundStartPropTargetRulesResolved = true;
            }
            if (!status.RoundStartDefinitionResolved)
                status.RoundStartRemoveWhenNotEnoughTimes = definition.RoundStartRemoveWhenNotEnoughTimes;
            status.RoundStartDefinitionResolved = true;
            if (!status.RoundStartTargetRulesResolved)
            {
                status.RoundStartPhase = definition.RoundStartPhase;
                status.RoundStartExcludedTargetStatuses = new List<int>(definition.RoundStartExcludedTargetStatuses);
                status.RoundStartTargetRulesResolved = true;
            }
        }

        private static bool IsPendingRoundStart(FightBattleUnitState owner, FightBattleStatusState status)
        {
            return owner != null && !owner.Ended && status != null && !status.RoundStartStartChecked
                && status.HasRoundStartTrigger && status.RoundStartRulesSupported && status.RoundStartTryAtStart;
        }

        private int StartRoundStatus(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (!IsPendingRoundStart(owner, status) || movementStartContext == null) return 0;
            status.RoundStartStartChecked = true;
            FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            int listenerId = FightStatusLifecycle.ResolveTargetUnitId(status, status.RoundStartListenTarget, units);
            if (!unitById.TryGetValue(listenerId, out var listener)) return 0;
            return ExecuteRoundStartStatus(owner, listener, status, true);
        }

        private void ExecuteRoundStartTriggers(FightBattleUnitState owner, FightBattleUnitState listener, int phase = 0)
        {
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                if (owner.Statuses.Contains(status)) ExecuteRoundStartStatus(owner, listener, status, false, phase);
        }

        private int ExecuteRoundStartStatus(FightBattleUnitState owner, FightBattleUnitState listener,
            FightBattleStatusState status, bool atStatusStart, int phase = 0)
        {
            if (status == null || !status.HasRoundStartTrigger || !status.RoundStartRulesSupported
                || !atStatusStart && status.RoundStartPhase != phase
                || !FightBattleTargeting.IsSelectable(owner) || !FightBattleTargeting.IsSelectable(listener)
                || FightStatusLifecycle.ResolveTargetUnitId(status, status.RoundStartListenTarget, units) != listener.Id)
                return 0;
            int before = status.RoundStartTriggeredCount;
            var invocations = FightStatusTriggerRuntime.CollectRoundStartInvocations(owner, listener,
                units, random, statusCatalog, status, atStatusStart, phase);
            ExecuteHpStartupSkills(invocations);
            // User-confirmed offline boundary: finish the final invocation before removing
            // its source. Unlimited triggers never exhaust this counter.
            if (status.RoundStartTriggeredCount > before && status.RoundStartRemoveWhenNotEnoughTimes
                && FightStatusLifecycle.RoundStartMaximumCount(status) >= 0
                && FightStatusLifecycle.RoundStartMaximumCount(status) - status.RoundStartTriggeredCount < status.RoundStartMeetCount
                && owner.Statuses.Contains(status)) FightStatusLifecycle.End(owner, status);
            return status.RoundStartTriggeredCount - before;
        }
    }
}
