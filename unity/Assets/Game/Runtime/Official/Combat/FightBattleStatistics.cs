using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// One resolved hit or presentation-only HP-cost float in a battle step. Hit records keep the
    /// official flags for HUD and statistics; <see cref="IsSkillCost"/> records are not hits.
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
        // Original FightHitBaseComponentInfo.DamageId; DamageId above is the runtime serial.
        public int DamageEntityId;
        public int DamageType;
        public int ElementType;
        public float ElementConfrontation;
        public bool IsElementAffect;
        // Explicit offline evidence gap; do not report an invented overlapping-state modifier.
        public bool ElementConfrontationUnresolved;
        public bool IsSkillCost;
        public float Delay;
        public int Anchor;
        public bool IsCure;
        public bool Critical;
        public bool Block;
        public bool Dodge;
        public bool Immunity;
        public bool Blinding;
        public bool IgnoreShield;
        public bool TrueDamage;
        public bool BreakResilience;
        public long Damage;
        // Original DamageResultParameter.DamageValue, before HP/overheal clamping.
        public bool HasDamageValue;
        public long DamageValue;
        public long ShieldAddFinalDamageValue => (HasDamageValue ? DamageValue : Damage) - ShieldDamage;
        public long AbsoluteDamage;
        public long ShieldDamage;
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
        public long AdditionalAttributedDamage;
        public long DamageAndShieldDamageDealt;
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
        public long AdditionalAttributedDamage;
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

        // Additional source credit is statistical only: it creates no second hit, HP loss or shield loss.
        public void RecordAdditionalDamage(int sourceId, int skillEntityId, int skillItemId, long amount)
        {
            FightBattleUnitStatistics source = Unit(sourceId);
            FightBattleSkillStatistics skill = Skill(sourceId, skillEntityId, skillItemId);
            source.DamageDealt = checked(source.DamageDealt + amount);
            source.DamageAndShieldDamageDealt = checked(source.DamageAndShieldDamageDealt + amount);
            source.AdditionalAttributedDamage = checked(source.AdditionalAttributedDamage + amount);
            skill.Damage = checked(skill.Damage + amount);
            skill.AdditionalAttributedDamage = checked(skill.AdditionalAttributedDamage + amount);
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
                source.DamageAndShieldDamageDealt = checked(source.DamageAndShieldDamageDealt
                    + amount + damage.ShieldDamage);
                target.DamageTaken += amount;
                skill.Damage += amount;
            }
        }
    }
}
