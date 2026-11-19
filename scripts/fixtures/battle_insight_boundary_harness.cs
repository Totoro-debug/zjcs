using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleInsightBoundaryHarness
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
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }

    private static (bool frozen, double chance, FightBattleCheckpoint checkpoint) Cast(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int seed, bool equipped)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var player = Unit(1, 5, formula);
        var target = Unit(2, 5, formula);
        target.X = 6;
        if (equipped)
        {
            var secret = items.Required(10559);
            var properties = catalog.CalculatePassiveSkillProps(secret.PassiveRankPropId,
                secret.PassiveGroupLevelPropId, secret.PassiveRankFactorId, secret.PassivePropFactors,
                2, 2, player.SubRank);
            if (properties.Count != 1 || !properties.TryGetValue(formula.PropertyId("EffectRate"), out long value) || value <= 0)
                throw new InvalidOperationException("洞悉 did not resolve its original effect accuracy.");
            foreach (var property in properties)
                player.Properties.Add(new FightPropertyValue { Id = property.Key, Value = property.Value });
        }
        double chance = formula.CalculateStatusEffectRate(player.Properties, target.Properties, .4f, 3);
        var skill = catalog.CreateSkill(10900, 0, 10035, 2, 2, 5,
            catalog.CalculateSkillProps(10900, 2, 2, 5).Select(value =>
                new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.LimitedTimes = 1;
        player.Skills.Add(skill);
        player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        checkpoint.Units.AddRange(new[] { player, target });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(target.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        executor.Step(0, 1);
        return (target.Statuses.Any(value => value.ClassId == 24215), chance, checkpoint);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var baseline = Cast(catalog, formula, items, 83629000, false);
        var boosted = Cast(catalog, formula, items, 83629000, true);
        var clearBaseline = Cast(catalog, formula, items, 100003, false);
        var clearBoosted = Cast(catalog, formula, items, 100003, true);
        var frozenBaseline = Cast(catalog, formula, items, 500015, false);
        var frozenBoosted = Cast(catalog, formula, items, 500015, true);
        if (baseline.frozen || !boosted.frozen || clearBaseline.frozen || clearBoosted.frozen
            || !frozenBaseline.frozen || !frozenBoosted.frozen || boosted.chance <= baseline.chance)
            throw new InvalidOperationException("洞悉 did not respect the original effect-accuracy boundary.");
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(boosted.checkpoint));
        if (!restored.Units.Single(value => value.Id == 2).Statuses.Any(value => value.ClassId == 24215))
            throw new InvalidOperationException("The actual frozen status did not survive checkpoint restore.");
        return $"洞悉 effect-rate boundary passed: {baseline.chance} -> {boosted.chance}, frozen only with the secret.";
    }
}
