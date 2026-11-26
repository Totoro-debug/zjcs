using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // FightStatusRoundCheckSkillComponent: check the completed personal round,
        // excluding automatic status children and pet support from active casts.
        private void ExecuteRoundCheckSkills(FightBattleUnitState actor, int ordinaryCastStart,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damages,
            List<FightBattleMovementEvent> movements, ref float duration, ref float actionDuration)
        {
            if (statusCatalog == null) return;
            bool released = false;
            for (int i = ordinaryCastStart; i < casts.Count; i++)
                if (casts[i].ActorId == actor.Id && casts[i].Slot >= 0 && !casts[i].IsPetSupport)
                { released = true; break; }
            var invocations = FightStatusTriggerRuntime.CollectRoundCheckSkills(actor, units, random, released);
            foreach (var invocation in invocations) invocation.Delay = Math.Max(invocation.Delay, duration);
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, casts, damages, movements, ref delay,
                out float end, out float actionEnd);
            duration = Math.Max(duration, end);
            if (actionEnd >= 0f) actionDuration = Math.Max(actionDuration, actionEnd);
        }
    }
}
