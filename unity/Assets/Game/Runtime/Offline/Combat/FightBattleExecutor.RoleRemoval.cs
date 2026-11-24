using System;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void ExecuteLivingRoleRemovalSkills(FightBattleUnitState removed)
        {
            // Lethal hits already dispatch role-death reactions at their hit time.
            // A lifespan expiry removes a living role through the same one-shot
            // removal boundary, so TryAtRemove listeners also receive that event.
            if (removed == null || !removed.Ended || removed.Hp <= 0 || movementStartContext == null) return;
            var invocations = FightStatusTriggerRuntime.CollectRoleDieSkills(removed, units, statusCatalog, random);
            if (invocations.Count == 0) return;
            foreach (var invocation in invocations)
                invocation.Delay = Math.Max(0f, movementStartContext.StatusStartTime);
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, movementStartContext.Casts,
                movementStartContext.DamageEvents, movementStartContext.Movements, ref delay,
                out float duration, out float actionDuration);
            movementStartContext.Duration = Math.Max(movementStartContext.Duration, Math.Max(delay, duration));
            movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration, actionDuration);
        }
    }
}
