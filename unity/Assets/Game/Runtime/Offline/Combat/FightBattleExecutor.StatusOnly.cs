using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // Confirmed offline None result: execute authored hit effects, without
        // entering damage/cure listeners, HP settlement or their hit counters.
        // The normal lifecycle records each accepted status application once.
        private void ExecuteStatusOnlyHit(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            FightBattleHitResult resolved, FightBattleDamageEvent damage, long skillCostHp,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damages,
            List<FightBattleMovementEvent> movements, ref float lastDelay,
            ref float statusDuration, ref float statusActionDuration,
            FightGridPoint hitOrigin, FightBattleSkillCastPresentation hitCast)
        {
            damage.IsDamageTriggerStatus = ApplyHitStatuses(actor, target, skill, hit, random,
                skillCostHp, 0, damage);
            damage.HasDamageTriggerStatus = true;
            var invocations = new List<FightBattleStatusSkillInvocation>();
            CollectHitTriggerSkill(actor, target, skill, hit, invocations, hitOrigin, hitCast, damage.Delay);
            ApplyMissingStatusComponentsOnHit(actor, target, skill, hit, resolved, 0, invocations);
            ApplyHitDisperse(actor, target, hit, random, damage);
            ApplyHitMove(actor, target, skill, hit, movements);
            RebuildBattleProperties();
            if (invocations.Count > 0)
            {
                ExecuteConfiguredStatusSkills(invocations, casts, damages, movements, ref lastDelay,
                    out float duration, out float actionDuration);
                statusDuration = Math.Max(statusDuration, duration);
                if (actionDuration >= 0f)
                    statusActionDuration = Math.Max(statusActionDuration, actionDuration);
            }
            CleanupGridItems();
            ApplyChildSkill(actor, target, skill, hit, casts, damages, movements, ref lastDelay);
        }
    }
}
