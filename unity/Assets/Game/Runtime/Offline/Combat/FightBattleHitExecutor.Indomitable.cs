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
                    if (status == null || status.ClassId != 25013 || !status.HasHpLimit
                        || status.ForbiddenType != 0 || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                        || status.HpLimitListenerType != 0 || status.OwnerId != target.Id
                        || status.HpLimitTriggeredCount >= status.HpLimitMaxCount) continue;
                    // Confirmed order: preserve one HP at settlement, then original 10990.
                    status.HpLimitTriggeredCount++;
                    result.FatalPrevention = status;
                    return settlement.ApplyWithLowerLimit(damageId, sourceEntity, targetEntity,
                        damage, status.HpLimitLowerLimit);
                }
            return settlement.Apply(damageId, sourceEntity, targetEntity, damage);
        }
    }
}
