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
            int fixedDamagePropertyId, long maximumValue = -1L)
        {
            double amount = Math.Abs((double)originalValue) * Read(statusProperties, damagePropertyId)
                + Read(statusProperties, fixedDamagePropertyId);
            if (maximumValue > 0) amount = Math.Min(amount, maximumValue);
            return -(long)Math.Max(0.0, amount);
        }

        private static double Read(IList<FightPropertyValue> properties, int id)
        {
            if (properties == null || id <= 0) return 0.0;
            foreach (FightPropertyValue value in properties)
                if (value != null && value.Id == id) return value.Value;
            return 0.0;
        }
    }
}
