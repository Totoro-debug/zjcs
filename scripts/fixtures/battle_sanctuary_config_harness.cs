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
        var linked = catalog.CreateStatus(24305, 1, 2, 2, 10580, 10580, 4, 30, 8);
        Check(linked.DurationRound == 1 && linked.ActionType == 50 && !linked.CanDispersed,
            "24305 linked status lifetime or dispel rule changed");
        return "Sanctuary configuration passed: 24304 reduction/custom/defence fields and 24305 linked status verified."
            + " Execution order and rounding remain unverified because the official client component is empty.";
    }
}
