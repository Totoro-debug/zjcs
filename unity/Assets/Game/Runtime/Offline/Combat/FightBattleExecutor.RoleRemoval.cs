using System;
using System.Collections.Generic;

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
            float delay = 0f, duration = 0f, actionDuration = -1f;
            ExecuteRoleDieSkills(removed, movementStartContext.StatusStartTime, movementStartContext.Casts,
                movementStartContext.DamageEvents, movementStartContext.Movements,
                ref delay, ref duration, ref actionDuration);
            movementStartContext.Duration = Math.Max(movementStartContext.Duration, Math.Max(delay, duration));
            movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration, actionDuration);
        }

        private void ExecuteRoleDieSkills(FightBattleUnitState removed, float time,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float delay, ref float duration, ref float actionDuration)
        {
            var invocations = FightStatusTriggerRuntime.CollectRoleDieSkills(removed, units, statusCatalog, random);
            if (invocations.Count == 0) return;
            foreach (var invocation in invocations) invocation.Delay = Math.Max(0f, time);
            ExecuteConfiguredStatusSkills(invocations, casts, damageEvents, movements, ref delay,
                out float nestedDuration, out float nestedActionDuration);
            duration = Math.Max(duration, nestedDuration);
            actionDuration = Math.Max(actionDuration, nestedActionDuration);
        }

        private void ExecuteRoleDeathSkills(FightBattleUnitState dead, float time,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float delay, ref float duration, ref float actionDuration)
        {
            ExecuteRoleDieSkills(dead, time, casts, damageEvents, movements,
                ref delay, ref duration, ref actionDuration);
            ExecuteRevivalSkills(casts, damageEvents, movements, ref delay, ref duration, ref actionDuration);
            SynchronizeDeathForbidden(dead);
        }
    }
}
