using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleValueLimit
    {
        public int TargetType, PropertyId, Percent;
        public bool UseFixedValue;
        public long FixedValue;
    }

    public static class FightDamageStatusValueLimit
    {
        public static long Apply(long value, FightBattleValueLimit limit,
            FightBattleUnitState source, FightBattleUnitState target, FightDamageFormula formula)
        {
            if (limit == null) return value;
            double maximum;
            if (limit.UseFixedValue) maximum = limit.FixedValue;
            else
            {
                var owner = limit.TargetType == 0 ? source : limit.TargetType == 1 ? target
                    : throw new NotSupportedException("Unsupported custom status limit target: " + limit.TargetType);
                var properties = new Dictionary<int, long>();
                foreach (var property in owner.Properties) properties[property.Id] = property.Value;
                maximum = formula.ReadProperty(properties, limit.PropertyId) * limit.Percent / 10000d;
            }
            long cap = maximum >= long.MaxValue ? long.MaxValue : (long)Math.Max(0d, maximum);
            return Math.Min(Math.Max(0L, value), cap);
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleValueLimit ReadDamageStatusValueLimit(int damageId,
            Dictionary<string, object> components)
        {
            // Confirmed solo interpretation of water-blade's description, including
            // its built-in variant: the original limit caps the converted shield.
            if (damageId != 11982 && damageId != 13582) return null;
            var limit = Component(components, "FightDamageLimitComponent")
                ?? throw new InvalidOperationException("Water-blade's original value limit is missing.");
            return new FightBattleValueLimit {
                TargetType = Int(limit, "UpperLimitTarget"), PropertyId = Int(limit, "UpperLimitPropType"),
                Percent = Int(limit, "UpperLimitPercent"), UseFixedValue = Bool(limit, "UseFixedValue"),
                FixedValue = Long(limit, "FixedValueLimit")
            };
        }
    }
}
