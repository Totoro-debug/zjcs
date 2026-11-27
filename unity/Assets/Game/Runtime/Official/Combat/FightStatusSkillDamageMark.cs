using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal static class FightStatusSkillDamageMark
    {
        private const int BreakingBrandStatusId = 24052;

        internal static bool IsBreakingBrand(FightBattleStatusState status) =>
            status.ClassId == BreakingBrandStatusId;

        internal static bool IsTriggerHit(FightBattleSkillDefinition skill, FightBattleHitResult hit) =>
            IsActiveSkill(skill) && !hit.IsCure && !hit.Type.Dodge && !hit.Type.Blinding && !hit.Type.Immunity;

        internal static void FilterTargetProperties(FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightDamageFormula formula, Dictionary<int, long> targetProperties)
        {
            if (IsActiveSkill(skill) || target.Statuses == null) return;

            // Property rebuild includes the mark on its owner. Remove its contribution
            // from this hit's property snapshot when the cast cannot use the mark.
            int vulnerability = formula.PropertyId("StatusDmgVulnerablePer");
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (status == null || !IsBreakingBrand(status) || status.ForbiddenType != 0
                    || !status.HasPropertyComponent || status.ResolvedProperties == null) continue;
                foreach (FightPropertyValue property in status.ResolvedProperties)
                    if (property != null && property.Id == vulnerability
                        && targetProperties.TryGetValue(vulnerability, out long value))
                        targetProperties[vulnerability] = value - property.Value;
            }
        }

        private static bool IsActiveSkill(FightBattleSkillDefinition skill) =>
            skill != null && (skill.ActionType == 2 || skill.ActionType == 6);
    }
}
