using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightDamageFormula
    {
        // Original BattleFormulaHandler.StatusDamage. Select the supplied result
        // and status properties at the call site; this method only calculates it.
        public long CalculateStatusDamage(IDictionary<int, long> statusProps,
            int damagePropType, int fixedDamagePropType, long originalValue, long maximumValue = -1L)
        {
            double amount = Math.Abs(originalValue);
            amount = amount * Prop(statusProps, damagePropType) + Prop(statusProps, fixedDamagePropType);
            if (maximumValue > 0) amount = Math.Min(amount, maximumValue);
            return -(long)Math.Max(0.0, amount);
        }
    }
}
