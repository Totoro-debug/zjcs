using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBlockAwarenessBoundaryHarness
{
    private static FightBattleUnitState Unit(int id, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = id == 1 ? 1 : 10,
            Level = 50, SubRank = 5, X = 5, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveMask = 5 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = id == 1 ? 10 : 1000;
        foreach (var value in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 },
            { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (string name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue",
            "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge",
            "BaseElementAdd", "BaseElementReduce" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        return unit;
    }

    private static (bool block, long damage) Cast(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int seed, int mode, int itemId)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var defender = Unit(1, 5, formula);
        var attacker = Unit(2, 7, formula);
        defender.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("BlockPercent"), Value = 500 });
        defender.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("BlockValuePercent"), Value = 10000 });
        if (mode != 0)
        {
            var secret = items.Required(itemId);
            var properties = catalog.CalculatePassiveSkillProps(secret.PassiveRankPropId,
                secret.PassiveGroupLevelPropId, secret.PassiveRankFactorId, secret.PassivePropFactors,
                2, 2, defender.SubRank);
            if (properties.Count != 2 || !properties.TryGetValue(formula.PropertyId("BlockPercent"), out long percent) || percent <= 0
                || !properties.TryGetValue(formula.PropertyId("BlockPercentValue"), out long value) || value <= 0)
                throw new InvalidOperationException("Block-awareness secret did not resolve both original properties: " + itemId);
            foreach (var property in properties)
                if ((property.Key == formula.PropertyId("BlockPercent") && (mode & 1) != 0)
                    || (property.Key == formula.PropertyId("BlockPercentValue") && (mode & 2) != 0))
                {
                    var existing = defender.Properties.FirstOrDefault(value => value.Id == property.Key);
                    if (existing == null) defender.Properties.Add(new FightPropertyValue { Id = property.Key, Value = property.Value });
                    else existing.Value += property.Value;
                }
        }
        var skill = catalog.CreateSkill(10500, 0, 10000, 2, 2, 5,
            catalog.CalculateSkillProps(10500, 2, 2, 5).Select(value =>
                new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.LimitedTimes = 1;
        attacker.Skills.Add(skill);
        attacker.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        checkpoint.Units.AddRange(new[] { defender, attacker });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(attacker.Id); checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
        var result = executor.Step(0, 1).Event;
        var replay = restored.Step(0, 1).Event;
        var hit = result.DamageEvents.Single(value => value.SourceId == 2 && value.TargetId == 1 && value.Damage != 0);
        var restoredHit = replay.DamageEvents.Single(value => value.SourceId == 2 && value.TargetId == 1 && value.Damage != 0);
        if (hit.Block != restoredHit.Block || hit.Damage != restoredHit.Damage
            || FightBattleCheckpointSerializer.Save(checkpoint) != FightBattleCheckpointSerializer.Save(saved))
            throw new InvalidOperationException("Block-awareness result changed after checkpoint restore.");
        return (hit.Block, Math.Abs(hit.Damage));
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        const int percentSeed = 1625122892, valueSeed = 145998428;
        const int itemId = 10507;
        var baseline = Cast(catalog, formula, items, percentSeed, 0, itemId);
        var percent = Cast(catalog, formula, items, percentSeed, 1, itemId);
        var combined = Cast(catalog, formula, items, percentSeed, 3, itemId);
        var valueBaseline = Cast(catalog, formula, items, valueSeed, 0, itemId);
        var value = Cast(catalog, formula, items, valueSeed, 2, itemId);
        var valueCombined = Cast(catalog, formula, items, valueSeed, 3, itemId);
        if (baseline.block || !percent.block || !combined.block || valueBaseline.block
            || !value.block || !valueCombined.block || combined.damage >= baseline.damage)
            throw new InvalidOperationException("Block-awareness secret did not respect both original boundaries: " + itemId);
        return "格挡意识 block boundaries passed for both original properties.";
    }
}
