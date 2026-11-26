using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleHitExecutor
    {
        private FightDamageEvent SettleDamage(int damageId, FightEntityState sourceEntity,
            FightEntityState targetEntity, FightBattleUnitState target, FightBattleSkillDefinition skill,
            long damage, int stageType, FightBattleHitResult result)
        {
            // Unlimited-HP test monsters do not receive a lethal HP change.
            if (damageTest != null && damageTest.UnlimitedHp(stageType, target))
                return damageTest.Settle(settlement, stageType, damageId, sourceEntity, targetEntity, target, damage);
            if (damage < 0 && targetEntity.Hp > 0 && damage <= -targetEntity.Hp)
                foreach (var status in new List<FightBattleStatusState>(target.Statuses))
                {
                    // Listener type 0 is the original target's fatal-hit HP limit.
                    // The component is shared by several skills; its owning status
                    // entity must not be baked into hit settlement.
                    if (status == null || !target.Statuses.Contains(status) || !status.HasHpLimit
                        || status.ForbiddenType != 0 || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                        || status.HpLimitListenerType != 0 || status.OwnerId != target.Id
                        || status.HpLimitMaxCount >= 0
                            && status.HpLimitTriggeredCount >= status.HpLimitMaxCount) continue;
                    // Clamp first, then execute the status's configured HP-limit
                    // skills from the owning battle executor.
                    status.HpLimitTriggeredCount++;
                    result.FatalPrevention = status;
                    FightDamageEvent prevented = settlement.ApplyWithLowerLimit(damageId, sourceEntity, targetEntity,
                        damage, status.HpLimitLowerLimit);
                    if (status.HpLimitAutoRemove) FightStatusLifecycle.End(target, status);
                    return prevented;
                }
            return settlement.Apply(damageId, sourceEntity, targetEntity, damage);
        }
    }
}
