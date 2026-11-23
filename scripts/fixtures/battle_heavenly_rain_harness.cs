using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleHeavenlyRainHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (long hp in new long[] { 9800, 10000, 10200, 19999 })
            Verify(catalog, formula, items, hp);
        return "Heavenly rain passed: lowest HP percentage within seven cells, strict half-HP threshold, cultivated cure, maximum HP cap and replay.";
    }

    private static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, long hp)
    {
        var caster = Unit(1, 1, 4, 1000, 22000, 22000, formula);
        var near = Unit(2, 1, 5, 700, hp == 19999 ? 5000 : 4900, 5000, formula);
        var receiver = Unit(3, 1, 11, 600, hp, 20000, formula);
        var outside = Unit(4, 1, 12, 500, 1, 20000, formula);
        var enemy = Unit(5, 2, 5, 400, 19000, 19000, formula);
        enemy.Y = 4;
        const int rank = 2, level = 2;
        var properties = catalog.CalculateSkillProps(12020, rank, level, 8).Select(pair =>
            new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();
        var skill = catalog.CreateSkill(12020, 0, 10096, rank, level, 8, properties);
        var definition = skill.HitGraphEntities.Single(hit => hit.EntityId == 12021);
        Check(definition.ExtApplyDamageTargetHpLessThan == .5f
            && Math.Abs(definition.ExtApplyDamageScale - 1.2f) < .0001f,
            "the original half-HP threshold or 20-percent scale changed");
        caster.Skills.Add(skill);
        skill.ResetCooldownAtStart = true;
        caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 723910,
            Grid = new FightBattleGrid { Width = 15, Height = 12, DefaultGroundType = 2 } };
        checkpoint.Units.AddRange(new[] { caster, near, receiver, outside, enemy });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(enemy.Id);
        checkpoint.Waves.Add(wave);
        var sourceProps = caster.Properties.ToDictionary(pair => pair.Id, pair => pair.Value);
        foreach (var property in properties) sourceProps[property.Id] = property.Value;
        long baseline = formula.CalculateCure(sourceProps, receiver.Properties.ToDictionary(pair => pair.Id,
            pair => pair.Value), definition.DamageProperty, definition.FixedDamageProperty).Cure;
        long expected = hp < 10000 ? (long)Math.Round(baseline * (double)definition.ExtApplyDamageScale,
            MidpointRounding.AwayFromZero) : baseline;
        expected = Math.Min(expected, receiver.MaxHp - hp);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var heals = step.DamageEvents.Where(value => value.SkillItemId == 10096).ToArray();
        Check(heals.Length == 1, $"configured target at {hp} HP produced {heals.Length} cures");
        var cure = heals[0];
        Check(cure.SourceId == 1 && cure.TargetId == 3 && cure.SkillEntityId == 12020
            && cure.HitEntityId == 12021 && cure.DamageEntityId == 12022 && cure.ElementType == 0
            && cure.IsCure && !cure.TrueDamage && Math.Abs(cure.Delay - 1f) < .0001f,
            $"the original heal chose another target, route or delay (target {cure.TargetId})");
        Check(expected > 0 && cure.BeforeHp == hp && cure.Damage == expected && cure.AfterHp == hp + expected,
            $"healing at {hp} HP yielded {cure.Damage}, expected {expected} (base {baseline})");
        Check(near.Hp == (hp == 19999 ? 5000 : 4900) && outside.Hp == 1,
            "healing affected the nearby ally or the out-of-range unit");
        string stored = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(stored);
        var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(replay.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == stored, "the cure was duplicated on replay");
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int speed, long hp, long max,
        FightDamageFormula formula)
    {
        var state = new FightBattleUnitState { Id = id, Side = side, X = x, Y = 5, CharacterType = 1,
            Profession = 17, Level = 70, SubRank = 8, MoveMask = 5, Hp = hp, MaxHp = max, LastMaxHp = max };
        state.Turn.Id = id;
        state.Turn.Hp = hp;
        state.Turn.Speed = speed;
        foreach (var value in new (string Name, long Value)[] { ("MaxHp", max), ("Attack", 2400), ("Speed", speed),
            ("Defence", 10), ("BaseCritRatePercentValue", 10000), ("BaseCritAvoidPercentValue", 10000) })
            state.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(value.Name), Value = value.Value });
        return state;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Heavenly rain: " + message);
    }
}
