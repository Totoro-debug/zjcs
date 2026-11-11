namespace Zjcs.Combat.Official
{
    public static partial class FightBattleTargeting
    {
        // User-confirmed offline behavior for the otherwise empty original 24008 handler.
        // Do not change Targetable: occupancy, friendly support and own actions remain independent.
        internal static bool IsHiddenFromEnemySelection(FightBattleUnitState actor,
            FightBattleUnitState target)
        {
            if (actor == null || target == null || actor.Side == target.Side
                || target.Statuses == null) return false;
            foreach (FightBattleStatusState status in target.Statuses)
                if (status != null && status.ClassId == 24008 && status.ActionType == 50
                    && status.ForbiddenType == 0 && status.StackCount > 0) return true;
            return false;
        }
    }
}
