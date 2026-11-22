using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleFixedPercentHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Fixed/percent: " + message); }

    public static string Run(FightDamageFormula formula)
    {
        foreach (var pair in new[] {
            new[] { "StatusDmgAddPer", "FixedStatusDmgAdd" },
            new[] { "StatusDmgReducePer", "FixedStatusDmgReduce" },
            new[] { "StatusIgnoreDefence", "FixedStatusIgnoreDefence" } })
            Check(FightStatusPropertyPairs.FixedProperty(formula.PropertyId(pair[0])) == formula.PropertyId(pair[1]),
                "the original percentage/fixed property identities changed");
        var source = new Dictionary<int, long>();
        var target = new Dictionary<int, long>();
        void Source(string name, long value) { Check(formula.PropertyId(name)>0, "unknown source property " + name); source[formula.PropertyId(name)] = value; }
        void Target(string name, long value) { Check(formula.PropertyId(name)>0, "unknown target property " + name); target[formula.PropertyId(name)] = value; }
        Source("Attack", 10000); Source("SkillAttack1", 5000); Source("SkillFixedAttack1", 5000);
        var request = new FightDamageFormulaRequest { SourceProps = source, TargetProps = target,
            SkillDamageBasicPropType = formula.PropertyId("Attack"), SkillDamagePropType = formula.PropertyId("SkillAttack1"),
            SkillDamageFixedPropType = formula.PropertyId("SkillFixedAttack1"), SourceType = 1, TargetType = 2,
            SourceLevel = 70, TargetLevel = 70, SourceSubRank = 8, TargetSubRank = 8, SourceElementType = 102 };
        Check(formula.Calculate(request, new FightDamageTypeData()).Damage == -10000,
            "50% attack + 5000 no longer equals 10000 at 10000 attack");
        Source("FixedStatusDmgAdd", 1000); Source("StatusDmgAddPer", 2000);
        Target("StatusDmgReducePer", 1000);
        Source("BaseElementAdd", 10000); Source("FireDamageAdd", 2000);
        Target("BaseElementReduce", 10000); Target("FireDamageReduce", 1000);
        Source("BaseElementMaster", 10000); Source("ElementMaster", 4000);
        Target("BaseElementResistance", 10000); Target("ElementResistance", 2000);
        Source("DmgAddPercent", 1000); Source("MonsterDmgAddScale", 1500); Target("DmgReducePercent", 2000);
        Source("CritPowerPercent", 5000);
        Target("BaseCritAvoidPercentValue", 10000);
        // FixedStatusDmgAdd follows the original skill coefficient; the skill's
        // own fixed 5000 is already part of the unscaled 10000 base above.
        long expected = -(long)(10500 * (1.6 / 1.3) * (1.25 / 1.2) * 1.5 * (1.2 / 1.1));
        long actual = formula.Calculate(request, new FightDamageTypeData { Critical = true }).Damage;
        Check(actual == expected,
            "fixed/base damage did not enter the original mastery, affinity, crit, monster and resistance multipliers: actual=" + actual + " expected=" + expected);
        source.Clear(); target.Clear();
        Source("MaxHp", 20000); Source("SkillCureByHp", 5000); Source("SkillFixedCure", 5000);
        Source("BaseCureAdd", 10000); Target("BaseBeCureAdd", 10000);
        Check(formula.CalculateCure(source, target, formula.PropertyId("SkillCureByHp"), formula.PropertyId("SkillFixedCure")).Cure == 15000,
            "the fixed healing term was omitted or multiplied by the base HP coefficient");
        Source("CureAddPercent", 2000); Target("BeCureAddPercent", 1000); Source("FinalCureScale", 1000);
        Check(formula.CalculateCure(source, target, formula.PropertyId("SkillCureByHp"), formula.PropertyId("SkillFixedCure")).Cure == 21450,
            "fixed/base healing did not enter the original healing bonus chain");
        return "Fixed/percent foundation passed: original property pairs, 50% attack + 5000, complete damage multipliers and fixed/base healing.";
    }
}
