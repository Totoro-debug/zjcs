using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// One resolved hit of a battle step. It keeps both the settlement value and the official hit
    /// flags so the HUD can choose the matching floating text and the report can attribute the
    /// damage to its actual source (skill, passive or status).
    /// </summary>
    [Serializable]
    public sealed class FightBattleDamageEvent
    {
        public int DamageId;
        public int SourceId;
        public int TargetId;
        public int SkillEntityId;
        public int SkillItemId;
        public int HitEntityId;
        public int DamageType;
        public bool IsCure;
        public bool Critical;
        public bool Block;
        public bool Dodge;
        public bool Immunity;
        public bool Blinding;
        public long Damage;
        public long AbsoluteDamage;
        public long BeforeHp;
        public long AfterHp;
        public long MaxHp;
    }

    [Serializable]
    public sealed class FightBattleUnitStatistics
    {
        public int UnitId;
        public int Casts;
        public int Hits;
        public int HitsTaken;
        public long DamageDealt;
        public long DamageTaken;
        public long HealDealt;
        public long HealTaken;
    }

    [Serializable]
    public sealed class FightBattleSkillStatistics
    {
        public int UnitId;
        public int SkillEntityId;
        public int SkillItemId;
        public int Casts;
        public int Hits;
        public long Damage;
        public long Heal;
    }

    /// <summary>
    /// Accumulated battle statistics used by the end report and skill tabs. Damage and healing use
    /// the actual HP settlement value, so falloff, shielding and over-heal are already applied.
    /// </summary>
    [Serializable]
    public sealed class FightBattleStatistics
    {
        public List<FightBattleUnitStatistics> Units = new List<FightBattleUnitStatistics>();
        public List<FightBattleSkillStatistics> Skills = new List<FightBattleSkillStatistics>();

        public FightBattleUnitStatistics Unit(int unitId)
        {
            foreach (FightBattleUnitStatistics value in Units)
                if (value != null && value.UnitId == unitId) return value;
            var created = new FightBattleUnitStatistics { UnitId = unitId };
            Units.Add(created);
            return created;
        }

        public FightBattleSkillStatistics Skill(int unitId, int skillEntityId, int skillItemId)
        {
            foreach (FightBattleSkillStatistics value in Skills)
                if (value != null && value.UnitId == unitId && value.SkillEntityId == skillEntityId)
                    return value;
            var created = new FightBattleSkillStatistics
            {
                UnitId = unitId,
                SkillEntityId = skillEntityId,
                SkillItemId = skillItemId
            };
            Skills.Add(created);
            return created;
        }

        public void RecordCast(int unitId, int skillEntityId, int skillItemId)
        {
            Unit(unitId).Casts++;
            Skill(unitId, skillEntityId, skillItemId).Casts++;
        }

        public void RecordHit(FightBattleDamageEvent damage)
        {
            if (damage == null) return;
            FightBattleUnitStatistics source = Unit(damage.SourceId);
            FightBattleUnitStatistics target = Unit(damage.TargetId);
            source.Hits++;
            target.HitsTaken++;
            FightBattleSkillStatistics skill = Skill(damage.SourceId, damage.SkillEntityId,
                damage.SkillItemId);
            skill.Hits++;
            long amount = Math.Abs(damage.Damage);
            if (damage.IsCure)
            {
                source.HealDealt += amount;
                target.HealTaken += amount;
                skill.Heal += amount;
            }
            else
            {
                source.DamageDealt += amount;
                target.DamageTaken += amount;
                skill.Damage += amount;
            }
        }
    }
}
