namespace Zjcs.Combat.Official
{
    public static class FightStatusApplicationRules
    {
        public static bool Roll(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
            FightRandomStream random, FightBattleUnitState source, FightBattleUnitState target,
            int statusId, double baseRate, bool affectedByProperties)
        {
            double rate = catalog.CalculateStatusApplicationRate(source, target, statusId,
                baseRate, affectedByProperties, formula);
            // These application events consume a draw even for a guaranteed outcome.
            return random.Next() <= rate;
        }

        public static bool Roll(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
            FightRandomStream random, FightBattleUnitState source, FightBattleUnitState target,
            FightBattleStatusApplication application)
        {
            if (application.CharacterTypeRateOverrides != null)
                foreach (FightBattleStatusRateOverride rateOverride in application.CharacterTypeRateOverrides)
                    if (rateOverride != null && rateOverride.CharacterType == target.CharacterType)
                        return Roll(catalog, formula, random, source, target, application.StatusId,
                            rateOverride.Rate, rateOverride.AffectedByProp);

            return Roll(catalog, formula, random, source, target, application.StatusId,
                application.Chance, application.AffectedByProp);
        }
    }
}
