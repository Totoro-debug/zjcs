using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void ExecuteDamageSharing(List<FightBattleStatusSkillInvocation> pending,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, float hitDelay, ref float lastDelay,
            ref float duration, ref float actionDuration)
        {
            foreach (var invocation in pending)
            {
                // Preserve the original receiver's already settled share if a guardian dies.
                if (!unitById.TryGetValue(invocation.TargetUnitId, out var guardian)
                    || !FightBattleTargeting.IsSelectable(guardian)) continue;
                invocation.CustomDamage = FightBattleDamageSharingRuntime.ResolveTransferredDamage(
                    invocation, units, formula);
                invocation.Delay = hitDelay;
                ExecuteConfiguredStatusSkills(new[] { invocation }, casts, damageEvents, movements,
                    ref lastDelay, out float nestedDuration, out float nestedActionDuration);
                duration = Math.Max(duration, nestedDuration);
                actionDuration = Math.Max(actionDuration, nestedActionDuration);
            }
        }
    }
}
