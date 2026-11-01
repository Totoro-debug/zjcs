using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Applies the official FightStatusDamageProcessComponent at the BeforeShield phase.
    /// The component's percent properties are read from the status snapshot, so a restored
    /// battle keeps the same result as the original application.
    /// </summary>
    public static class FightStatusDamageProcessRuntime
    {
        public static long BeforeShield(FightBattleUnitState source, FightBattleUnitState target,
            long value, FightDamageFormula formula, int resultType)
        {
            if (source == null || target == null || formula == null || target.Statuses == null
                || value == 0) return value;
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
                double amount = value;
                double reduce = process.ReducePercentPropertyId <= 0 ? 0.0
                    : formula.ReadProperty(properties, process.ReducePercentPropertyId);
                double add = process.AddPercentPropertyId <= 0 ? 0.0
                    : formula.ReadProperty(properties, process.AddPercentPropertyId);
                if (reduce != 0.0) amount /= Math.Max(1.0 + reduce, 0.1);
                if (add != 0.0) amount *= 1.0 + add;
                value = amount >= long.MaxValue ? long.MaxValue
                    : amount <= long.MinValue ? long.MinValue : (long)amount;
            }
            return value;
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
    /// the bonus to the status creator; the user confirmed percentage first, then fixed damage.
    /// Other damage-process component branches remain unsupported.
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
                double amount = -(double)damage * (1.0 + percent) + fixedDamage;
                damage = amount <= 0.0 ? 0L : amount >= long.MaxValue ? -long.MaxValue : -(long)amount;
            }
            return damage;
        }
    }
}
