namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private static bool IsPendingConfiguredStatusStart(FightBattleUnitState owner,
            FightBattleStatusState status)
        {
            var start = status?.Components?.Start;
            // Element harmony has its own original skill-element collector.
            return owner != null && !owner.Ended && status != null && status.ClassId != 25111
                && start != null && !start.Started
                && (start.ForbiddenStatusId > 0 || start.Statuses != null && start.Statuses.Count > 0);
        }

        private int StartConfiguredStatus(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (!IsPendingConfiguredStatusStart(owner, status) || movementStartContext == null) return 0;
            FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            ApplyMissingStatusStart(owner, status);
            return 1;
        }
    }
}
