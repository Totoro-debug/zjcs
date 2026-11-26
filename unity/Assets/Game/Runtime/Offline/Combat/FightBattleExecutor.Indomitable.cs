using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void ExecuteIndomitableRecovery(FightBattleUnitState owner, FightBattleHitResult hit,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, float hitDelay, ref float delay,
            ref float duration, ref float actionDuration)
        {
            var source = hit.FatalPrevention;
            if (source == null || statusCatalog == null) return;
            // Damage has settled to the configured lower HP limit. Rebuild the
            // holder's live properties before any configured HP-limit skill runs.
            FightStatusTriggerRuntime.RecalculateCurrentHpStackSources(owner, statusCatalog, random);
            RebuildBattleProperties();
            var invocations = FightStatusTriggerRuntime.CollectHpLimitSkills(owner, source, units, random);
            foreach (var invocation in invocations) invocation.Delay = Math.Max(0f, hitDelay);
            ExecuteConfiguredStatusSkills(invocations, casts, damageEvents, movements, ref delay,
                out float recoveryDuration, out float recoveryActionDuration);
            duration = Math.Max(duration, recoveryDuration);
            actionDuration = Math.Max(actionDuration, recoveryActionDuration);
        }
    }

    public static partial class FightStatusTriggerRuntime
    {
        internal static List<FightBattleStatusSkillInvocation> CollectHpLimitSkills(
            FightBattleUnitState owner, FightBattleStatusState source,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            foreach (var trigger in source.HpLimitSkills)
                AppendThresholdSkill(owner, source, trigger, random, result, null, units);
            return result;
        }
    }
}
