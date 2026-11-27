using System;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightDamageFormula
    {
        // Original BattleFormulaHandler.CalcTrueDamage. IsTrueDamage denotes
        // breaking damage; DamageType.CustomDamageWithoutBattleFormula is separate.
        public long CalcTrueDamage(long damageValue, long totalShieldValue)
        {
            if (damageValue >= 0 || totalShieldValue == 0L) return damageValue;
            long amount = Math.Abs(damageValue);
            double extra = config.TrueDamageAddPerWhenHasShield;
            long result = amount <= totalShieldValue
                ? (long)((double)amount * (1.0 + extra))
                : (long)((double)totalShieldValue
                    + ((double)amount - (double)totalShieldValue / (1.0 + extra)));
            return -result;
        }

        public long CalculateTrueDamageTargetScale(long damageValue, int targetCharacterType)
        {
            if (damageValue >= 0 || !config.TrueDamageCharacterTypes.Contains(targetCharacterType))
                return damageValue;
            return (long)(damageValue * (1.0 + config.TrueDamageAddPerWhenIsSummon));
        }
    }
}
