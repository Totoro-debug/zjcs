using System;
using Zjcs.Combat.Official;

public static class BattleSanctuaryConfigHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Sanctuary config: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        var status = catalog.CreateStatus(24304, 2, 1, 1, 10580, 10580, 4, 30, 8);
        var component = status.Components.DamageReduceBeforeFinal;
        Check(component != null, "24304 did not retain DamageReduceBeforeFinal configuration");
        Check(component.ReduceDamagePropertyId == formula.PropertyId("FinalDamageReducePercent"),
            "24304 reduction property changed");
        Check(component.CustomDamageSkillId == 11780
            && component.CustomDamagePropertyId == formula.PropertyId("DamageByDamage")
            && component.CustomDamageSource == 1
            && component.CustomDamageTarget == 3
            && component.CustomDamageTargetPosition == 1,
            "24304 custom damage skill target/source changed");
        Check(component.UnitPropertySource == 3
            && component.UnitPropertyId == formula.PropertyId("Defence")
            && component.UnitPropertyTarget == 2
            && component.UnitPercent == 100
            && component.ResultPercent == -100
            && component.IsCheckSkillTrigger,
            "24304 creator/applicator defence transfer configuration changed");
        long custom = FightBattleStatusDamageReduceBeforeFinalRuntime.CalculateStatusDamage(
            1000, new[] {
                new FightPropertyValue { Id = formula.PropertyId("DamageByDamage"), Value = 3000 },
                new FightPropertyValue { Id = formula.PropertyId("FixedDamageByDamage"), Value = 1200 }
            }, formula.PropertyId("DamageByDamage"), formula.PropertyId("FixedDamageByDamage"), formula);
        Check(custom == -1500, "official StatusDamage percent/fixed property units changed: " + custom);
        CheckFormula(formula);
        var linked = catalog.CreateStatus(24305, 1, 2, 2, 10580, 10580, 4, 30, 8);
        Check(linked.DurationRound == 1 && linked.ActionType == 50 && !linked.CanDispersed,
            "24305 linked status lifetime or dispel rule changed");
        return "Sanctuary configuration passed: 24304 reduction/custom/defence fields and 24305 linked status verified."
            + " Execution order and rounding remain unverified because the official client component is empty.";
    }

    private static void CheckFormula(FightDamageFormula formula)
    {
        int rate = formula.PropertyId("DamageByDamage");
        int fixedValue = formula.PropertyId("FixedDamageByDamage");
        foreach (long[] values in new[] {
            new long[] { 1000, 3000, 1200, -1, -1500 },
            new long[] { -1000, 3000, 1200, -1, -1500 },
            new long[] { 0, 3000, 1200, -1, -1200 },
            new long[] { 1000, 3000, 1200, 1000, -1000 },
            new long[] { 1000, 3000, 1200, 0, -1500 },
            new long[] { 1000, -3000, 100, -1, 0 },
            new long[] { 7, 3333, 0, -1, -2 },
            new long[] { 1000, 0, 1200, -1, -1200 }
        })
        {
            long actual = FightBattleStatusDamageReduceBeforeFinalRuntime.CalculateStatusDamage(values[0],
                new[] { new FightPropertyValue { Id = rate, Value = values[1] },
                    new FightPropertyValue { Id = fixedValue, Value = values[2] } },
                rate, fixedValue, formula, values[3]);
            Check(actual == values[4], "shared StatusDamage boundary failed: " + actual);
        }
        Check(FightBattleStatusDamageReduceBeforeFinalRuntime.CalculateStatusDamage(1000, null,
            rate, fixedValue, formula) == 0, "absent status properties produced damage");
        bool overflow = false;
        try { FightBattleStatusDamageReduceBeforeFinalRuntime.CalculateStatusDamage(long.MinValue,
            null, rate, fixedValue, formula); }
        catch (OverflowException) { overflow = true; }
        Check(overflow, "the official absolute-value overflow contract changed");
    }
}
