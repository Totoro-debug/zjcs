using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Hit-result components which run between the official hit type roll and damage
    /// settlement. The component data is stored on each status instance so counters and
    /// replacement values survive checkpoint restore.
    /// </summary>
    public static class FightBattleHitComponentRuntime
    {
        public static FightDamageTypeData ApplyDamageGetType(
            FightBattleUnitState target, FightBattleSkillDefinition skill, int elementType,
            FightDamageTypeData current, FightRandomStream random)
        {
            if (target?.Statuses == null) return current;
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(target.Statuses))
            {
                FightBattleStatusDamageGetTypeComponentState component =
                    status?.Components?.DamageGetType;
                if (!Eligible(status, skill, component) || !TryResolveType(component, elementType,
                    out int damageType, out bool value) || !Roll(component.Rate, random)) continue;

                SetDamageType(ref current, damageType, value);
                component.TriggeredCount++;
            }
            return current;
        }

        public static bool ApplyDamageReduceAbsolute(
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            ref long damage, ref long absoluteDamage, FightRandomStream random)
        {
            if (target?.Statuses == null || damage == 0 && absoluteDamage == 0) return false;
            bool triggered = false;
            var remove = new List<FightBattleStatusState>();
            double amount = Math.Abs((double)(absoluteDamage != 0 ? absoluteDamage : damage));
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(target.Statuses))
            {
                FightBattleStatusDamageReduceAbsoluteComponentState component =
                    status?.Components?.DamageReduceAbsolute;
                if (!Eligible(status, skill, component) || component.MaxCount >= 0
                    && component.TriggeredCount >= component.MaxCount) continue;
                if (component.DamageGreaterMaxHpPer >= 0f
                    && amount <= target.MaxHp * (double)component.DamageGreaterMaxHpPer) continue;
                if (!Roll(component.Rate, random)) continue;

                component.TriggeredCount++;
                damage = 0;
                absoluteDamage = 0;
                amount = 0;
                triggered = true;
                target.StatusRefreshed?.Invoke(target, status);
                if (component.AutoRemove && component.MaxCount >= 0
                    && component.TriggeredCount >= component.MaxCount) remove.Add(status);
            }
            foreach (FightBattleStatusState status in remove)
                FightStatusLifecycle.End(target, status);
            return triggered;
        }

        private static bool Eligible(FightBattleStatusState status,
            FightBattleSkillDefinition skill,
            FightBattleStatusDamageGetTypeComponentState component)
        {
            if (status == null || component == null || status.ForbiddenType != 0) return false;
            if (component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount) return false;
            if (component.IsCheckSkillTrigger && !FightSkillTriggerPolicy.AllowsStatus(skill, status))
                return false;
            if (component.FilterSkillIds != null && component.FilterSkillIds.Count > 0
                && (skill == null || !component.FilterSkillIds.Contains(skill.EntityId))) return false;
            return true;
        }

        private static bool Eligible(FightBattleStatusState status,
            FightBattleSkillDefinition skill,
            FightBattleStatusDamageReduceAbsoluteComponentState component)
        {
            if (status == null || component == null || status.ForbiddenType != 0) return false;
            if (component.IsCheckSkillTrigger && !FightSkillTriggerPolicy.AllowsStatus(skill, status))
                return false;
            return true;
        }

        private static bool TryResolveType(FightBattleStatusDamageGetTypeComponentState component,
            int elementType, out int damageType, out bool value)
        {
            if (component.Elements != null)
                foreach (FightBattleStatusDamageElementRule rule in component.Elements)
                    if (rule != null && rule.ElementType == elementType && rule.DamageType != 0)
                    {
                        damageType = rule.DamageType;
                        value = rule.Value;
                        return true;
                    }
            damageType = component.DamageType;
            value = component.Value;
            return damageType != 0;
        }

        private static void SetDamageType(ref FightDamageTypeData result, int damageType, bool value)
        {
            switch (damageType)
            {
                case 1: result.Critical = value; break;
                case 2: result.Block = value; break;
                case 3: result.Dodge = value; break;
                case 4: result.Blinding = value; break;
                case 5: result.Immunity = value; break;
            }
        }

        private static bool Roll(float rate, FightRandomStream random)
        {
            if (rate >= 1f) return true;
            if (rate <= 0f) return false;
            return random == null || random.Next() < rate;
        }
    }
}
