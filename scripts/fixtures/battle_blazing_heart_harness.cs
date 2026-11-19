using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBlazingHeartHarness
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

    private static long Cast(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int entityId, int itemId, int rank, int level)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var player = Unit(1, 5, formula);
        var target = Unit(2, 7, formula);
        if (rank > 0)
        {
            var secret = items.Required(10520);
            var properties = catalog.CalculatePassiveSkillProps(secret.PassiveRankPropId,
                secret.PassiveGroupLevelPropId, secret.PassiveRankFactorId, secret.PassivePropFactors,
                rank, level, player.SubRank);
            if (properties.Count != 1 || !properties.TryGetValue(formula.PropertyId("FireDamageAdd"), out long value) || value <= 0)
                throw new InvalidOperationException("炽热之心 did not resolve its original fire affinity.");
            foreach (var property in properties)
                player.Properties.Add(new FightPropertyValue { Id = property.Key, Value = property.Value });
        }
        var skill = catalog.CreateSkill(entityId, 0, itemId, 2, 2, 5,
            catalog.CalculateSkillProps(entityId, 2, 2, 5).Select(value =>
                new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.LimitedTimes = 1;
        player.Skills.Add(skill);
        player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        checkpoint.Units.AddRange(new[] { player, target });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(target.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var restoredState = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        var restored = FightBattleExecutor.Restore(restoredState, formula, new FightDamageSettlement(), catalog, items);
        var result = executor.Step(0, 1).Event;
        restored.Step(0, 1);
        if (FightBattleCheckpointSerializer.Save(checkpoint) != FightBattleCheckpointSerializer.Save(restoredState))
            throw new InvalidOperationException("炽热之心 changed the battle after checkpoint restore.");
        var hit = result.DamageEvents.Single(value => value.SourceId == player.Id && value.TargetId == target.Id
            && !value.IsCure && value.Damage != 0);
        return Math.Abs(hit.Damage);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        long fire = Cast(catalog, formula, items, 10800, 10028, 0, 0);
        long fireLow = Cast(catalog, formula, items, 10800, 10028, 1, 1);
        long fireHigh = Cast(catalog, formula, items, 10800, 10028, 2, 2);
        long water = Cast(catalog, formula, items, 10500, 10000, 0, 0);
        long waterHigh = Cast(catalog, formula, items, 10500, 10000, 2, 2);
        if (fireLow <= fire || fireHigh <= fireLow || waterHigh != water)
            throw new InvalidOperationException($"炽热之心 affected the wrong damage: fire {fire}/{fireLow}/{fireHigh}, water {water}/{waterHigh}.");
        return $"炽热之心 battle and restore passed: fire {fire}/{fireLow}/{fireHigh}, water {water}/{waterHigh}.";
    }
}
