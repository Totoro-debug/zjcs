using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // StatusStackCountComponentInfo specifies the layer threshold, trigger
        // quota and removal condition; effect application uses the shared rules.
        public static void ApplyStackCount(FightBattleUnitState owner,
            FightBattleStatusState status, FightOfficialCombatCatalog catalog,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random)
        {
            if (owner == null || status == null || catalog == null || !owner.Statuses.Contains(status)
                || !status.HasStackCountTrigger || !status.StackCountRulesSupported
                || status.ForbiddenType != 0 || status.StackCount < status.StackCountThreshold) return;
            if (status.StackCountMaxTriggers >= 0 && status.StackCountTriggered >= status.StackCountMaxTriggers)
            {
                if (status.StackCountRemoveOnMax) FightStatusLifecycle.End(owner, status);
                return;
            }
            // Reserve the quota before nested status-start callbacks.
            status.StackCountTriggered++;
            foreach (var trigger in status.StackCountStatuses)
                ApplyThresholdStatus(owner, status, trigger, catalog, units, random);
            if (status.StackCountRemoveOnMax && status.StackCountMaxTriggers >= 0
                && status.StackCountTriggered >= status.StackCountMaxTriggers)
                FightStatusLifecycle.End(owner, status);
        }
    }
}
