using System;
namespace Zjcs.Combat.Official
{
    // Shared totals are derived from live statuses, so removal and checkpoint restore
    // cannot leave a separate pool counter out of sync.
    public static class FightStatusSharedStacks
    {
        public static int Total(FightBattleUnitState owner, int shareId)
        {
            long total = 0;
            foreach (var status in owner.Statuses)
                if (status != null && status.SharedStackId == shareId) total += Math.Max(0, status.StackCount);
            return (int)Math.Min(int.MaxValue, total);
        }

        internal static bool Allocate(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (status.SharedStackId <= 0) return true;
            int requested = Math.Max(0, status.StackCount);
            if (status.MaxStackedCount > 0) requested = Math.Min(requested, status.MaxStackedCount);
            int assigned = Math.Min(requested, Math.Max(0, status.SharedStackLimit - Total(owner, status.SharedStackId)));
            if (assigned == 0) return false;
            int before = status.StackCount;
            status.StackCount = assigned;
            FightStatusPropertyRuntime.OnStackChanged(status, before);
            return true;
        }

        internal static int Constrain(FightBattleUnitState owner, FightBattleStatusState status, int delta)
        {
            if (status.SharedStackId <= 0) return delta;
            if (owner == null || !owner.Statuses.Contains(status))
                throw new InvalidOperationException("Shared stack changes require the actual live status owner.");
            int total = Total(owner, status.SharedStackId);
            // FightStatusAgentComponentImplement.CanAddShareStack / ChangeShareStack.
            if (delta > 0 && total >= status.SharedStackLimit) return 0;
            int next = (int)Math.Min(status.SharedStackLimit, Math.Max(0L, total + (long)delta));
            return next - total;
        }
    }
}
