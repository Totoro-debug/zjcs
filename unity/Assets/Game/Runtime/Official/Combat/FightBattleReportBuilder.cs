using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleReportUnit
    {
        public int UnitId;
        public int MasterId;
        public int CharacterType;
        public int Side;
        public int MonsterId;
        public int MonsterType;
        public int Level;
        public long Hp;
        public long MaxHp;
        public bool Alive;
        public int Casts;
        public int Hits;
        public int HitsTaken;
        public long DamageDealt;
        public long AdditionalAttributedDamage;
        public long DamageTaken;
        public bool HasAbsoluteDamageTaken;
        public long AbsoluteDamageTaken;
        public bool HasBreakResilienceCount;
        public int BreakResilienceCount;
        public bool HasStatusTypeCounts;
        public int OrdinaryDebuffApplications;
        public int AbnormalDebuffApplications;
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
    public sealed class FightBattleReportSkill
    {
        public int UnitId;
        public int SkillEntityId;
        public int SkillItemId;
        public int Casts;
        public int Hits;
        public bool HasDamageHitCounts;
        public int DamageHits;
        public int DamageTargets;
        public bool HasCureHitCounts;
        public int CureHits;
        public bool HasStatusApplicationCounts;
        public int BuffApplications;
        public int DebuffApplications;
        public float AvgDamageCount;
        public float AvgDamageCountPerTarget;
        public long Damage;
        public long AdditionalAttributedDamage;
        public long Heal;
        public bool HasShieldGrantCounts;
        public long ShieldGranted;
        public float DamagePer;
        public float HealPer;
        public float ShieldPer;
        public bool HasMaxHpPercentages;
        public float HealMaxHpPer;
        public float ShieldMaxHpPer;
    }

    /// <summary>
    /// End-of-battle report data built from the official statistics sample. The official report panel
    /// prefabs are not bound yet, so this type only provides the data the panel will consume.
    /// </summary>
    [Serializable]
    public sealed class FightBattleReport
    {
        public int StageId;
        public bool Won;
        public int Reason;
        public int Round;
        public long PlayerHp;
        public long PlayerMaxHp;
        public long EnemyHp;
        public long EnemyMaxHp;
        public long TotalDamageDealt;
        public long TotalDamageTaken;
        public long TotalHealDealt;
        public long TotalShieldGranted;
        public List<FightBattleReportUnit> Units = new List<FightBattleReportUnit>();
        public List<FightBattleReportSkill> Skills = new List<FightBattleReportSkill>();

        public FightBattleReportUnit Unit(int unitId)
        {
            foreach (FightBattleReportUnit row in Units)
                if (row != null && row.UnitId == unitId) return row;
            return null;
        }
    }

    public static class FightBattleReportBuilder
    {
        /// <summary>
        /// Builds the report from the checkpoint outcome and the accumulated statistics. Unit rows keep
        /// the roster order; skill rows keep the statistics order so damage/heal totals are stable for
        /// the same battle.
        /// </summary>
        public static FightBattleReport Build(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            var report = new FightBattleReport { StageId = checkpoint.StageId };
            if (checkpoint.Outcome != null)
            {
                report.Won = checkpoint.Outcome.Won;
                report.Reason = checkpoint.Outcome.Reason;
                report.Round = checkpoint.Outcome.Round;
                report.PlayerHp = checkpoint.Outcome.PlayerHp;
                report.EnemyHp = checkpoint.Outcome.EnemyHp;
                report.EnemyMaxHp = checkpoint.Outcome.EnemyMaxHp;
            }
            FightBattleStatistics statistics = checkpoint.Statistics ?? new FightBattleStatistics();
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                if (unit == null) continue;
                FightBattleUnitStatistics stats = null;
                foreach (FightBattleUnitStatistics candidate in statistics.Units)
                    if (candidate != null && candidate.UnitId == unit.Id) { stats = candidate; break; }
                var row = new FightBattleReportUnit
                {
                    UnitId = unit.Id,
                    MasterId = unit.MasterId,
                    CharacterType = unit.CharacterType,
                    Side = unit.Side,
                    MonsterId = unit.MonsterId,
                    MonsterType = unit.MonsterType,
                    Level = unit.Level,
                    Hp = unit.Hp,
                    MaxHp = unit.MaxHp,
                    Alive = unit.Hp > 0,
                    Casts = stats == null ? 0 : stats.Casts,
                    Hits = stats == null ? 0 : stats.Hits,
                    HitsTaken = stats == null ? 0 : stats.HitsTaken,
                    DamageDealt = stats == null ? 0 : stats.DamageDealt,
                    AdditionalAttributedDamage = stats == null ? 0 : stats.AdditionalAttributedDamage,
                    DamageTaken = stats == null ? 0 : stats.DamageTaken,
                    HasAbsoluteDamageTaken = stats != null && stats.HasAbsoluteDamageTaken,
                    AbsoluteDamageTaken = stats == null ? 0 : stats.AbsoluteDamageTaken,
                    HasBreakResilienceCount = stats != null && stats.HasBreakResilienceCount,
                    BreakResilienceCount = stats == null ? 0 : stats.BreakResilienceCount,
                    HasStatusTypeCounts = stats != null && stats.HasStatusTypeCounts,
                    OrdinaryDebuffApplications = stats == null ? 0 : stats.OrdinaryDebuffApplications,
                    AbnormalDebuffApplications = stats == null ? 0 : stats.AbnormalDebuffApplications,
                    HealDealt = stats == null ? 0 : stats.HealDealt,
                    HealTaken = stats == null ? 0 : stats.HealTaken,
                    ShieldGranted = stats == null ? 0 : stats.ShieldGranted,
                    ShieldReceived = stats == null ? 0 : stats.ShieldReceived,
                    UnattributedShieldGranted = stats == null ? 0 : stats.UnattributedShieldGranted,
                    BuffApplications = stats == null ? 0 : stats.BuffApplications,
                    DebuffApplications = stats == null ? 0 : stats.DebuffApplications,
                    UnattributedBuffApplications = stats == null ? 0 : stats.UnattributedBuffApplications,
                    UnattributedDebuffApplications = stats == null ? 0 : stats.UnattributedDebuffApplications
                };
                if (unit.Id == 1)
                {
                    report.PlayerHp = unit.Hp;
                    report.PlayerMaxHp = unit.MaxHp;
                }
                report.TotalDamageDealt += row.DamageDealt;
                report.TotalDamageTaken += row.DamageTaken;
                report.TotalHealDealt += row.HealDealt;
                report.TotalShieldGranted += row.ShieldGranted;
                report.Units.Add(row);
            }
            foreach (FightBattleSkillStatistics skill in statistics.Skills)
            {
                if (skill == null) continue;
                report.Skills.Add(new FightBattleReportSkill
                {
                    UnitId = skill.UnitId,
                    SkillEntityId = skill.SkillEntityId,
                    SkillItemId = skill.SkillItemId,
                    Casts = skill.Casts,
                    Hits = skill.Hits,
                    HasDamageHitCounts = skill.HasDamageHitCounts,
                    DamageHits = skill.DamageHits,
                    DamageTargets = skill.DamageTargets,
                    HasCureHitCounts = skill.HasCureHitCounts,
                    CureHits = skill.CureHits,
                    HasStatusApplicationCounts = skill.HasStatusApplicationCounts,
                    BuffApplications = skill.BuffApplications,
                    DebuffApplications = skill.DebuffApplications,
                    AvgDamageCount = skill.HasDamageHitCounts && skill.Casts > 0
                        ? (float)skill.DamageHits / skill.Casts : 0f,
                    AvgDamageCountPerTarget = skill.HasDamageHitCounts && skill.DamageTargets > 0
                        ? (float)skill.DamageHits / skill.DamageTargets : 0f,
                    Damage = skill.Damage,
                    AdditionalAttributedDamage = skill.AdditionalAttributedDamage,
                    Heal = skill.Heal,
                    HasShieldGrantCounts = skill.HasShieldGrantCounts,
                    ShieldGranted = skill.ShieldGranted
                });
            }
            foreach (FightBattleReportSkill skill in report.Skills)
            {
                long damage = 0, heal = 0, shield = 0;
                FightBattleReportUnit source = report.Unit(skill.UnitId);
                // Unattributed applications could belong to any skill of this caster. Keep the
                // recorded lower bounds, but do not present them as complete per-skill totals.
                foreach (FightBattleUnitStatistics candidate in statistics.Units)
                    if (candidate != null && candidate.UnitId == skill.UnitId)
                        skill.HasStatusApplicationCounts &= candidate.UnattributedBuffApplications == 0
                            && candidate.UnattributedDebuffApplications == 0;
                bool completeShield = source == null || source.UnattributedShieldGranted == 0;
                foreach (FightBattleReportSkill candidate in report.Skills)
                {
                    if (candidate.UnitId != skill.UnitId) continue;
                    damage = checked(damage + candidate.Damage);
                    heal = checked(heal + candidate.Heal);
                    shield = checked(shield + candidate.ShieldGranted);
                    completeShield &= candidate.HasShieldGrantCounts;
                }
                skill.DamagePer = damage > 0 ? (float)((double)skill.Damage / damage) : 0f;
                skill.HealPer = heal > 0 ? (float)((double)skill.Heal / heal) : 0f;
                skill.HasShieldGrantCounts = completeShield;
                skill.ShieldPer = completeShield && shield > 0 ? (float)((double)skill.ShieldGranted / shield) : 0f;
                skill.HasMaxHpPercentages = source != null && source.MaxHp > 0;
                if (!skill.HasMaxHpPercentages) continue;
                skill.HealMaxHpPer = (float)((double)skill.Heal / source.MaxHp);
                skill.ShieldMaxHpPer = completeShield ? (float)((double)skill.ShieldGranted / source.MaxHp) : 0f;
            }
            return report;
        }
    }
}
