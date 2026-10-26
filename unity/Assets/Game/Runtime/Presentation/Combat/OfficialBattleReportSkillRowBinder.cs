using System;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleReportSkillRowBinder
    {
        public static WorldBattleReportSkillRowData Values(FightBattleReportSkill skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            return new WorldBattleReportSkillRowData
            {
                Count = skill.Casts,
                AvgDamageCount = skill.HasDamageHitCounts ? skill.AvgDamageCount : (float?)null,
                AvgDamageCountPerTarget = skill.HasDamageHitCounts ? skill.AvgDamageCountPerTarget : (float?)null,
                RealDamage = -skill.Damage,
                RealDamagePer = skill.DamagePer,
                RealCure = skill.Heal,
                RealCurePer = skill.HealPer,
                RealCureMaxHpPer = skill.HasMaxHpPercentages ? skill.HealMaxHpPer : (float?)null,
                RealShield = skill.HasShieldGrantCounts ? skill.ShieldGranted : (long?)null,
                RealShieldPer = skill.HasShieldGrantCounts ? skill.ShieldPer : (float?)null,
                RealShieldMaxHpPer = skill.HasShieldGrantCounts && skill.HasMaxHpPercentages ? skill.ShieldMaxHpPer : (float?)null,
                DamageCount = skill.HasDamageHitCounts ? skill.DamageHits : (int?)null,
                CureCount = skill.HasCureHitCounts ? skill.CureHits : (int?)null,
                ApplyBuffStatusCount = skill.HasStatusApplicationCounts ? skill.BuffApplications : (int?)null,
                ApplyDebuffStatusCount = skill.HasStatusApplicationCounts ? skill.DebuffApplications : (int?)null
            };
        }
    }
}
