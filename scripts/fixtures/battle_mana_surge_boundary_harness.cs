using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleManaSurgeBoundaryHarness
{
    private static FightBattleUnitState Unit(int id, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 10,
            Level = 50, SubRank = 5, X = 5, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveMask = 5 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 },
            { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (string name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue",
            "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge",
            "BaseElementAdd", "BaseElementReduce" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        return unit;
    }

    private static (bool critical, long damage) Cast(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int seed, int mode, int itemId)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var player = Unit(1, 5, formula);
        var target = Unit(2, 7, formula);
        if (mode != 0)
        {
            var secret = items.Required(itemId);
            var properties = catalog.CalculatePassiveSkillProps(secret.PassiveRankPropId,
                secret.PassiveGroupLevelPropId, secret.PassiveRankFactorId, secret.PassivePropFactors,
                2, 2, player.SubRank);
            if (properties.Count != 2 || !properties.TryGetValue(formula.PropertyId("CritRatePercent"), out long percent) || percent <= 0
                || !properties.TryGetValue(formula.PropertyId("CritRatePercentValue"), out long value) || value <= 0)
                throw new InvalidOperationException("Critical-rate secret did not resolve both original properties: " + itemId);
            foreach (var property in properties)
                if ((property.Key == formula.PropertyId("CritRatePercent") && (mode & 1) != 0)
                    || (property.Key == formula.PropertyId("CritRatePercentValue") && (mode & 2) != 0))
                    player.Properties.Add(new FightPropertyValue { Id = property.Key, Value = property.Value });
        }
        var skill = catalog.CreateSkill(10500, 0, 10000, 2, 2, 5,
            catalog.CalculateSkillProps(10500, 2, 2, 5).Select(value =>
                new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.LimitedTimes = 1;
        player.Skills.Add(skill);
        player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        checkpoint.Units.AddRange(new[] { player, target });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(target.Id); checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var result = executor.Step(0, 1).Event;
        var hit = result.DamageEvents.Single(value => value.SourceId == 1 && value.TargetId == 2 && value.Damage != 0);
        return (hit.Critical, Math.Abs(hit.Damage));
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        const int percentSeed = 16049372, valueSeed = 1949381294;
        foreach (int itemId in new[] { 10554, 10500 })
        {
            var baseline = Cast(catalog, formula, items, percentSeed, 0, itemId);
            var percent = Cast(catalog, formula, items, percentSeed, 1, itemId);
            var combined = Cast(catalog, formula, items, percentSeed, 3, itemId);
            var valueBaseline = Cast(catalog, formula, items, valueSeed, 0, itemId);
            var value = Cast(catalog, formula, items, valueSeed, 2, itemId);
            var valueCombined = Cast(catalog, formula, items, valueSeed, 3, itemId);
            if (baseline.critical || !percent.critical || !combined.critical || valueBaseline.critical
                || !value.critical || !valueCombined.critical || baseline.damage != 442 || combined.damage != 575)
                throw new InvalidOperationException("Critical-rate secret did not respect both original boundaries: " + itemId);
        }
        return "Critical boundaries passed for 10554 and 10500: damage 442 -> 575.";
    }
}
