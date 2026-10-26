using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleReportUnit
    {
        public int UnitId;
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
        public long HealDealt;
        public long HealTaken;
    }

    [Serializable]
    public sealed class FightBattleReportSkill
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
                    HealDealt = stats == null ? 0 : stats.HealDealt,
                    HealTaken = stats == null ? 0 : stats.HealTaken
                };
                if (unit.Id == 1)
                {
                    report.PlayerHp = unit.Hp;
                    report.PlayerMaxHp = unit.MaxHp;
                }
                report.TotalDamageDealt += row.DamageDealt;
                report.TotalDamageTaken += row.DamageTaken;
                report.TotalHealDealt += row.HealDealt;
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
                    Damage = skill.Damage,
                    AdditionalAttributedDamage = skill.AdditionalAttributedDamage,
                    Heal = skill.Heal
                });
            }
            return report;
        }
    }
}
