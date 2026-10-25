using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightResilienceFormula
    {
        public static int CalculateChange(FightDamageFormula formula,
            IDictionary<int, long> sourceProperties, double rate, long skillFactor)
        {
            if (formula == null) throw new ArgumentNullException(nameof(formula));

            double breakResilience = formula.ReadProperty(sourceProperties,
                formula.PropertyId("BreakResilience"));
            double breakResiliencePercent = formula.ReadProperty(sourceProperties,
                formula.PropertyId("BreakResiliencePercent"));
            double value = rate * (breakResilience * (1.0 + breakResiliencePercent))
                * (skillFactor / 10000.0);
            return -(int)Math.Max(1.0, value);
        }

        public static long CalculateBrokenDamage(FightDamageFormula formula,
            IList<FightPropertyValue> targetProperties,
            IList<FightPropertyValue> breakerProperties,
            int maxHpPercent, int attackPercent)
        {
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            double targetMaxHp = formula.ReadProperty(ToDictionary(targetProperties),
                formula.PropertyId("MaxHp"));
            double breakerAttack = formula.ReadProperty(ToDictionary(breakerProperties),
                formula.PropertyId("Attack"));
            double damage = Math.Min(targetMaxHp * (maxHpPercent / 10000.0),
                breakerAttack * (attackPercent / 10000.0));
            return -(long)Math.Max(0.0, damage);
        }

        private static Dictionary<int, long> ToDictionary(IList<FightPropertyValue> properties)
        {
            var result = new Dictionary<int, long>();
            if (properties == null) return result;
            for (int i = 0; i < properties.Count; i++)
            {
                FightPropertyValue property = properties[i];
                if (property != null) result[property.Id] = property.Value;
            }
            return result;
        }
    }
}
