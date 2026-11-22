using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Applies the official FightStatusDamageProcessComponent at the BeforeShield phase.
    /// The component's paired fixed/percent properties are read from the status snapshot, so a restored
    /// battle keeps the same result as the original application.
    /// </summary>
    public static class FightStatusDamageProcessRuntime
    {
        public static long BeforeShield(FightBattleUnitState source, FightBattleUnitState target,
            long value, FightDamageFormula formula, int resultType)
        {
            if (source == null || target == null || formula == null || target.Statuses == null
                || value == 0) return value;
            double noIndependentReduce = 0.0;
            double noIndependentAdd = 0.0;
            double noIndependentFixed = 0.0;
            bool hasNoIndependent = false;
            double sign = value < 0 ? -1.0 : 1.0;
            double amount = System.Math.Abs((double)value);
            foreach (FightBattleStatusState status in target.Statuses)
            {
                FightBattleStatusDamageProcessInfo process = status == null ? null : status.DamageProcess;
                if (process == null || !process.Supported || status.HasCreatorDamageBoost
                    || process.ProcessPhase != 0 || process.Target != 2
                    || process.DamageType != resultType)
                    continue;
                if (process.TriggerOnlyDamageFromApplicator && source.Id != status.CreatorId)
                    continue;
                if (process.TriggerOnlyDamageFromCreatorAndSummons
                    && source.Id != status.CreatorId && source.MasterId != status.CreatorId)
                    continue;
                if (process.CheckSourceIsEnemy && source.Side == target.Side) continue;
                if (!HasSourceActionType(source, process.SourceActionTypes)) continue;

                IDictionary<int, long> properties = status.ResolvedProperties == null
                    ? ToDictionary(status.StatusProperties) : ToDictionary(status.ResolvedProperties);
                if (properties == null) continue;
                double reduce = process.ReducePercentPropertyId <= 0 ? 0.0
                    : formula.ReadProperty(properties, process.ReducePercentPropertyId);
                double add = process.AddPercentPropertyId <= 0 ? 0.0
                    : formula.ReadProperty(properties, process.AddPercentPropertyId);
                double fixedAdd = formula.ReadProperty(properties,
                    FightStatusPropertyPairs.FixedProperty(process.AddPercentPropertyId));
                double fixedReduce = formula.ReadProperty(properties,
                    FightStatusPropertyPairs.FixedProperty(process.ReducePercentPropertyId));
                if (process.NoIndependentMultiplier)
                {
                    // The dedicated official component prevents each matching status
                    // from multiplying the damage independently. Accumulate its
                    // percentage terms and apply one combined modifier after the
                    // ordinary per-status modifiers have been evaluated.
                    noIndependentReduce += reduce;
                    noIndependentAdd += add;
                    noIndependentFixed += fixedAdd - fixedReduce;
                    hasNoIndependent = true;
                    continue;
                }
                amount = Math.Max(0.0, amount + fixedAdd - fixedReduce);
                if (reduce != 0.0) amount /= Math.Max(1.0 + reduce, 0.1);
                if (add != 0.0) amount *= 1.0 + add;
            }
            if (hasNoIndependent)
            {
                amount = Math.Max(0.0, amount + noIndependentFixed);
                if (noIndependentReduce != 0.0)
                    amount /= Math.Max(1.0 + noIndependentReduce, 0.1);
                if (noIndependentAdd != 0.0)
                    amount *= 1.0 + noIndependentAdd;
            }
            amount *= sign;
            return amount >= long.MaxValue ? long.MaxValue
                : amount <= long.MinValue ? long.MinValue : (long)amount;
        }

        private static bool HasSourceActionType(FightBattleUnitState source, IList<int> actionTypes)
        {
            if (actionTypes == null || actionTypes.Count == 0) return true;
            if (source.Statuses == null) return false;
            foreach (FightBattleStatusState status in source.Statuses)
                if (status != null && actionTypes.Contains(status.ActionType)) return true;
            return false;
        }

        private static Dictionary<int, long> ToDictionary(IList<FightPropertyValue> values)
        {
            var result = new Dictionary<int, long>();
            if (values == null) return result;
            foreach (FightPropertyValue value in values)
                if (value != null) result[value.Id] = value.Value;
            return result;
        }
    }

    /// <summary>
    /// Confirmed offline wiring for 24062's BeforeShield damage component. Its description restricts
    /// the bonus to the status creator. The shared offline rule applies its fixed
    /// increment before the percentage, as with other paired fixed/percent effects.
    /// </summary>
    public static class FightStatusCreatorDamageBoostRuntime
    {
        public static long BeforeShield(FightBattleUnitState source, FightBattleUnitState target,
            long damage, FightDamageFormula formula)
        {
            if (source == null || target == null || target.Statuses == null || damage > 0) return damage;
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (status == null || !status.HasCreatorDamageBoost || status.CreatorId != source.Id
                    || status.ResolvedProperties == null) continue;
                var properties = new Dictionary<int, long>();
                foreach (FightPropertyValue value in status.ResolvedProperties)
                    if (value != null) properties[value.Id] = value.Value;
                double percent = formula.ReadProperty(properties, formula.PropertyId("StatusDmgAddPer"));
                double fixedDamage = formula.ReadProperty(properties, formula.PropertyId("FixedStatusDmgAdd"));
                double amount = (-(double)damage + fixedDamage) * (1.0 + percent);
                damage = amount <= 0.0 ? 0L : amount >= long.MaxValue ? -long.MaxValue : -(long)amount;
            }
            return damage;
        }
    }
}
