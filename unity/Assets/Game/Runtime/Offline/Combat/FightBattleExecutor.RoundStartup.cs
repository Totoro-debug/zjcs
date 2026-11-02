using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RestoreRoundStartDefinition(FightBattleStatusState status)
        {
            if (!status.HasRoundStartTrigger || status.RoundStartDefinitionResolved) return;
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties, blessPropertyScales: status.BlessPropertyScales);
            status.RoundStartRulesSupported = definition.RoundStartRulesSupported;
            status.RoundStartRemoveWhenNotEnoughTimes = definition.RoundStartRemoveWhenNotEnoughTimes;
            status.RoundStartDefinitionResolved = true;
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

        private void ExecuteRoundStartTriggers(FightBattleUnitState owner, FightBattleUnitState listener)
        {
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                if (owner.Statuses.Contains(status)) ExecuteRoundStartStatus(owner, listener, status, false);
        }

        private int ExecuteRoundStartStatus(FightBattleUnitState owner, FightBattleUnitState listener,
            FightBattleStatusState status, bool atStatusStart)
        {
            if (status == null || !status.HasRoundStartTrigger || !status.RoundStartRulesSupported
                || !FightBattleTargeting.IsSelectable(owner) || !FightBattleTargeting.IsSelectable(listener)
                || FightStatusLifecycle.ResolveTargetUnitId(status, status.RoundStartListenTarget, units) != listener.Id)
                return 0;
            int before = status.RoundStartTriggeredCount;
            var invocations = FightStatusTriggerRuntime.CollectRoundStartInvocations(owner, listener,
                units, random, statusCatalog, status, atStatusStart);
            ExecuteHpStartupSkills(invocations);
            // User-confirmed offline boundary: finish the final invocation before removing
            // its source. Unlimited triggers never exhaust this counter.
            if (status.RoundStartRemoveWhenNotEnoughTimes && status.RoundStartMaxCount >= 0
                && status.RoundStartMaxCount - status.RoundStartTriggeredCount < status.RoundStartMeetCount
                && owner.Statuses.Contains(status)) FightStatusLifecycle.End(owner, status);
            return status.RoundStartTriggeredCount - before;
        }
    }
}
