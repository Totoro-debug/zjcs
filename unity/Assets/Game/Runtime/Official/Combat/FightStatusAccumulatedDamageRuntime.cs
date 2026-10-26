using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleDamageContribution
    {
        public int SourceId;
        public int SkillEntityId;
        public int SkillItemId;
        public long HpDamage;
    }

    [Serializable]
    public sealed class FightBattleStatusAccumulatedDamage
    {
        public bool RulesSupported;
        public int SkillEntityId;
        public long AccumulationDamagePer;
        public int DamageResultType;
        public List<int> HitDamageTypes = new List<int>();
        public bool OnlyDamageByCreator;
        public bool TriggerDamageAddToDamageSource;
        public long HpDamage;
        public List<FightBattleDamageContribution> Contributions = new List<FightBattleDamageContribution>();

        public FightBattleStatusAccumulatedDamage Copy()
        {
            var result = new FightBattleStatusAccumulatedDamage
            {
                RulesSupported = RulesSupported, SkillEntityId = SkillEntityId,
                AccumulationDamagePer = AccumulationDamagePer, DamageResultType = DamageResultType,
                HitDamageTypes = new List<int>(HitDamageTypes), OnlyDamageByCreator = OnlyDamageByCreator,
                TriggerDamageAddToDamageSource = TriggerDamageAddToDamageSource, HpDamage = HpDamage
            };
            foreach (FightBattleDamageContribution value in Contributions)
                result.Contributions.Add(new FightBattleDamageContribution { SourceId = value.SourceId,
                    SkillEntityId = value.SkillEntityId, SkillItemId = value.SkillItemId, HpDamage = value.HpDamage });
            return result;
        }
    }

    /// <summary>
    /// Local status-end wiring for the official accumulated-damage component. Status 24062's
    /// description excludes true damage and uses actual life lost, not damage absorbed by shields.
    /// </summary>
    public static class FightStatusAccumulatedDamageRuntime
    {
        public static void RecordHit(FightBattleUnitState target, FightBattleDamageEvent damage)
        {
            if (target == null || target.Statuses == null || damage == null || damage.TargetId != target.Id
                || damage.IsSkillCost || damage.IsCure || damage.TrueDamage || damage.BeforeHp <= damage.AfterHp) return;
            long amount = damage.BeforeHp - damage.AfterHp;
            foreach (FightBattleStatusState status in target.Statuses)
            {
                FightBattleStatusAccumulatedDamage accumulator = status?.AccumulatedDamage;
                if (accumulator == null || !accumulator.RulesSupported || accumulator.DamageResultType != 1
                    || !accumulator.HitDamageTypes.Contains(damage.DamageType)
                    || accumulator.OnlyDamageByCreator && damage.SourceId != status.CreatorId) continue;
                accumulator.HpDamage = checked(accumulator.HpDamage + amount);
                FightBattleDamageContribution contribution = accumulator.Contributions.Find(value =>
                    value.SourceId == damage.SourceId && value.SkillEntityId == damage.SkillEntityId
                    && value.SkillItemId == damage.SkillItemId);
                if (contribution == null)
                {
                    contribution = new FightBattleDamageContribution { SourceId = damage.SourceId,
                        SkillEntityId = damage.SkillEntityId, SkillItemId = damage.SkillItemId };
                    accumulator.Contributions.Add(contribution);
                }
                contribution.HpDamage = checked(contribution.HpDamage + amount);
            }
        }

        public static void RecordAdditionalDamage(FightBattleStatusState status, long actualHpDamage,
            FightBattleStatistics statistics)
        {
            FightBattleStatusAccumulatedDamage accumulator = status?.AccumulatedDamage;
            if (statistics == null || actualHpDamage <= 0 || accumulator == null
                || !accumulator.TriggerDamageAddToDamageSource || accumulator.HpDamage <= 0) return;
            var weights = new List<long>();
            foreach (FightBattleDamageContribution contribution in accumulator.Contributions)
                weights.Add(contribution.HpDamage);
            var shares = new List<long>();
            DistributeLongProportional(actualHpDamage, weights, accumulator.HpDamage, ref shares);
            for (int i = 0; i < shares.Count; i++)
            {
                FightBattleDamageContribution source = accumulator.Contributions[i];
                statistics.RecordAdditionalDamage(source.SourceId, source.SkillEntityId,
                    source.SkillItemId, shares[i]);
            }
        }

        // Direct port of Common.FightStatusUtilsHandler.DistributeLongProportional.
        // The final source receives the integer remainder; Math.Round uses its original default mode.
        public static void DistributeLongProportional(long total, List<long> weights,
            long totalWeight, ref List<long> result)
        {
            result.Clear();
            int count = weights.Count;
            if (count == 0) return;
            if (count == 1) { result.Add(total); return; }
            long allocated = 0L;
            for (int i = 0; i < count - 1; i++)
            {
                if (totalWeight == 0L) { result.Add(0L); continue; }
                long share = (long)Math.Round((double)total * weights[i] / totalWeight);
                result.Add(share);
                allocated += share;
            }
            result.Add(total - allocated);
        }

        public static long CustomDamage(FightBattleStatusState status)
        {
            FightBattleStatusAccumulatedDamage accumulator = status?.AccumulatedDamage;
            if (accumulator == null || !accumulator.RulesSupported) return 0;
            decimal damage = (decimal)accumulator.HpDamage * accumulator.AccumulationDamagePer / 10000m;
            return damage >= long.MaxValue ? long.MaxValue : (long)damage;
        }
    }
}
