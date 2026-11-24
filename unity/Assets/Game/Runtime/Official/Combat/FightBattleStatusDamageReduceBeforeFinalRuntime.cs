using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// The verified BattleFormulaHandler.StatusDamage calculation shared by
    /// DamageReduceBeforeFinal evidence and future status dispatch code.
    /// The client component has no execution body, so this helper intentionally
    /// does not schedule the configured child skill.
    /// </summary>
    public static class FightBattleStatusDamageReduceBeforeFinalRuntime
    {
        public static long CalculateStatusDamage(long originalValue,
            IList<FightPropertyValue> statusProperties, int damagePropertyId,
            int fixedDamagePropertyId, FightDamageFormula formula, long maximumValue = -1L)
        {
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            var properties = new Dictionary<int, long>();
            foreach (FightPropertyValue value in statusProperties ?? Array.Empty<FightPropertyValue>())
                if (value != null) properties[value.Id] = value.Value;
            return formula.CalculateStatusDamage(properties, damagePropertyId,
                fixedDamagePropertyId, originalValue, maximumValue);
        }
    }
}
