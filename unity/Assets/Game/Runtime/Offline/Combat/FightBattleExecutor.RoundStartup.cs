using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private FightBattleUnitState roundStartNotForbiddenListener;

        private void OnStatusForbiddenChanged(FightBattleUnitState owner,
            FightBattleStatusState status, bool wasForbidden)
        {
            if (statusCatalog != null)
                FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
            var listener = roundStartNotForbiddenListener;
            if (!wasForbidden || status.ForbiddenType != 0 || !status.RoundStartTryAtNotForbidden
                || listener == null || movementStartContext == null || statusCatalog == null
                || !owner.Statuses.Contains(status) || !status.RoundStartRulesSupported
                || FightStatusLifecycle.ResolveTargetUnitId(status, status.RoundStartListenTarget, units) != listener.Id
                || status.RoundStartNotForbiddenListenerId == listener.Id
                    && status.RoundStartNotForbiddenRound == listener.SkillState.Round) return;
            // The first complete release consumes this round's extra attempt, even if
            // probability/conditions fail. Record it before invoking nested effects.
            status.RoundStartNotForbiddenListenerId = listener.Id;
            status.RoundStartNotForbiddenRound = listener.SkillState.Round;
            ExecuteRoundStartStatus(owner, listener, status, false, status.RoundStartPhase);
            RebuildBattleProperties();
        }

        private void RestoreRoundStartDefinition(FightBattleStatusState status)
        {
            if (!status.HasRoundStartTrigger
                || status.RoundStartDefinitionResolved && status.RoundStartTargetRulesResolved
                    && status.RoundStartPropTargetRulesResolved && status.RoundStartMeetRulesResolved
                    && status.RoundStartRandomRulesResolved && status.RoundStartSourceRulesResolved
                    && status.RoundStartMergeRulesResolved
                    && status.RoundStartDelayRulesResolved
                    && status.RoundStartFilterRulesResolved
                    && status.RoundStartForbiddenRulesResolved
                    // Earlier checkpoints parsed merged-count definitions but intentionally blocked them.
                    && (!status.RoundStartMergeMaxCountOnStackRound || status.RoundStartRulesSupported)) return;
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties, blessPropertyScales: status.BlessPropertyScales);
            status.RoundStartRulesSupported = definition.RoundStartRulesSupported;
            status.RoundStartSourceRulesResolved = true;
            if (!status.RoundStartForbiddenRulesResolved)
            {
                status.RoundStartTryAtNotForbidden = definition.RoundStartTryAtNotForbidden;
                status.RoundStartForbiddenRulesResolved = true;
            }
            if (!status.RoundStartFilterRulesResolved)
            {
                status.RoundStartRequiresFriendWithoutStatuses = definition.RoundStartRequiresFriendWithoutStatuses;
                status.RoundStartFriendIncludeApplicator = definition.RoundStartFriendIncludeApplicator;
                status.RoundStartFriendExcludedStatuses = new List<int>(definition.RoundStartFriendExcludedStatuses);
                status.RoundStartFilterRulesResolved = true;
            }
            if (!status.RoundStartDelayRulesResolved)
            {
                status.RoundStartDelayRound = definition.RoundStartDelayRound;
                status.RoundStartDelayRoundType = definition.RoundStartDelayRoundType;
                // Original delayed definitions use the same applicator clock for lifetime
                // and listening. Their saved StartRound is the retained application/refresh time.
                if (status.RoundStartDelayRound > 0 && status.StartRound >= 0
                    && status.RoundStartListenTarget == status.RoundTarget)
                    status.RoundStartDelayStartRound = status.StartRound;
                status.RoundStartDelayRulesResolved = true;
            }
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
                && status.HasRoundStartTrigger && status.RoundStartRulesSupported && status.RoundStartTryAtStart
                && status.RoundStartDelayRound == 0;
        }

        private int RoundStartClockRound(FightBattleStatusState status)
        {
            int id = FightStatusLifecycle.ResolveTargetUnitId(status, status.RoundStartListenTarget, units);
            return unitById.TryGetValue(id, out var listener) ? listener.SkillState.Round : -1;
        }

        private void InitializeRoundStartDelay(FightBattleStatusState status)
        {
            if (status != null && status.HasRoundStartTrigger && status.RoundStartDelayRound > 0
                && status.RoundStartDelayRoundType == 1 && status.RoundStartDelayStartRound < 0)
                status.RoundStartDelayStartRound = RoundStartClockRound(status);
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
