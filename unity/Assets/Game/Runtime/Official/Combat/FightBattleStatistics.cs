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
        public long ShieldGranted;
        public long ShieldReceived;
        public long UnattributedShieldGranted;
        public int BuffApplications;
        public int DebuffApplications;
        public int UnattributedBuffApplications;
        public int UnattributedDebuffApplications;
    }

    [Serializable]
    public sealed class FightBattleSkillStatistics
    {
        public int UnitId;
        public int SkillEntityId;
        public int SkillItemId;
        public int Casts;
        public int Hits;
        // Old checkpoints have no per-cast target history. Keep their averages explicitly unknown.
        public bool HasDamageHitCounts;
        public int DamageHits;
        public int DamageTargets;
        public bool HasCureHitCounts;
        public int CureHits;
        public bool HasStatusApplicationCounts;
        public int BuffApplications;
        public int DebuffApplications;
        public long Damage;
        public long AdditionalAttributedDamage;
        public long Heal;
        public bool HasShieldGrantCounts;
        public long ShieldGranted;
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
                SkillItemId = skillItemId,
                HasDamageHitCounts = true,
                HasCureHitCounts = true,
                HasStatusApplicationCounts = true,
                HasShieldGrantCounts = true
            };
            Skills.Add(created);
            return created;
        }

        public void RecordCast(int unitId, int skillEntityId, int skillItemId)
        {
            Unit(unitId).Casts++;
            Skill(unitId, skillEntityId, skillItemId).Casts++;
        }

        public void RecordPendingStatusApplications(IReadOnlyList<FightBattleUnitState> units)
        {
            if (units == null) return;
            foreach (FightBattleUnitState owner in units)
            {
                if (owner == null || owner.PendingStatusApplications == null) continue;
                foreach (FightBattleStatusApplicationEvent application in owner.PendingStatusApplications)
                {
                    if (application == null || application.CreatorId <= 0
                        || application.StatusType < 1 || application.StatusType > 3) continue;
                    bool buff = application.StatusType == 1;
                    FightBattleUnitStatistics source = Unit(application.CreatorId);
                    if (buff) source.BuffApplications = checked(source.BuffApplications + 1);
                    else source.DebuffApplications = checked(source.DebuffApplications + 1);
                    if (application.RootSkillId <= 0)
                    {
                        if (buff) source.UnattributedBuffApplications = checked(source.UnattributedBuffApplications + 1);
                        else source.UnattributedDebuffApplications = checked(source.UnattributedDebuffApplications + 1);
                        continue;
                    }
                    FightBattleSkillStatistics skill = Skill(application.CreatorId,
                        application.RootSkillId, application.ApplyingSkillItemId);
                    if (buff) skill.BuffApplications = checked(skill.BuffApplications + 1);
                    else skill.DebuffApplications = checked(skill.DebuffApplications + 1);
                }
                owner.PendingStatusApplications.Clear();
            }
        }

        // The confirmed report value is successfully added shield, independent of later absorption.
        // Rebuilding resolves its configured/custom value; the pending bit prevents replay, refresh,
        // discard and existing stack updates from counting the same grant again.
        public void RecordPendingShieldGrants(IReadOnlyList<FightBattleUnitState> units)
        {
            if (units == null) return;
            foreach (FightBattleUnitState owner in units)
            {
                if (owner == null || owner.Statuses == null) continue;
                foreach (FightBattleStatusState status in owner.Statuses)
                {
                    if (status == null || !status.ShieldGrantPending || !status.ShieldInitialized) continue;
                    status.ShieldGrantPending = false;
                    long amount = Math.Max(0L, status.ShieldValue);
                    if (amount == 0) continue;
                    FightBattleUnitStatistics target = Unit(owner.Id);
                    target.ShieldReceived = checked(target.ShieldReceived + amount);
                    if (status.CreatorId <= 0) continue;
                    FightBattleUnitStatistics source = Unit(status.CreatorId);
                    source.ShieldGranted = checked(source.ShieldGranted + amount);
                    if (status.RootSkillId <= 0)
                    {
                        source.UnattributedShieldGranted = checked(source.UnattributedShieldGranted + amount);
                        continue;
                    }
                    FightBattleSkillStatistics skill = Skill(status.CreatorId, status.RootSkillId,
                        status.ApplyingSkillItemId);
                    skill.ShieldGranted = checked(skill.ShieldGranted + amount);
                }
            }
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

        public void RecordHit(FightBattleDamageEvent damage, ISet<int> castDamageTargets = null)
        {
            if (damage == null || damage.IsSkillCost) return;
            FightBattleUnitStatistics source = Unit(damage.SourceId);
            FightBattleUnitStatistics target = Unit(damage.TargetId);
            source.Hits++;
            target.HitsTaken++;
            FightBattleSkillStatistics skill = Skill(damage.SourceId, damage.SkillEntityId,
                damage.SkillItemId);
            skill.Hits++;
            // User-confirmed server gap: immunity and full shielding are damage hits; dodge is not.
            // Each ExecuteSkill invocation owns its target set, including recursive/replayed skills.
            if (!damage.IsCure && !damage.Dodge && !damage.Blinding && damage.DamageType != 0)
            {
                skill.DamageHits = checked(skill.DamageHits + 1);
                if (castDamageTargets == null) skill.HasDamageHitCounts = false;
                else if (castDamageTargets.Add(damage.TargetId))
                    skill.DamageTargets = checked(skill.DamageTargets + 1);
            }
            long amount = Math.Abs(damage.Damage);
            if (damage.IsCure)
            {
                // A landed cure counts even when full HP clamps its actual healing to zero.
                if (!damage.Dodge && !damage.Blinding) skill.CureHits = checked(skill.CureHits + 1);
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
