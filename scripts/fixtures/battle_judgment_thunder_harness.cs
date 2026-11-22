using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleJudgmentThunderHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Judgment thunder: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (bool large in new[] { false, true })
        foreach (int seed in new[] { 7, 37 })
        {
            int dx = direction[0], dy = direction[1];
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            cp.Units.Add(Unit(1, 1, 10, 10, formula));
            cp.Units.Add(Unit(2, 2, 10 + dx * 2, 10 + dy * 2, formula));
            var far = Unit(3, 2, 10 + dx * 5, 10 + dy * 5, formula);
            if (large) far.BodyOffsets.Add(new FightGridPoint(dx, dy));
            cp.Units.Add(far);
            cp.Units.Add(Unit(4, 2, 10 - dx * 6, 10 - dy * 6, formula));
            cp.Units.Add(Unit(5, 1, 10 + dx * 3, 10 + dy * 3, formula));
            var skill = catalog.CreateSkill(11970, 0, 10093, rank, level, 8,
                catalog.CalculateSkillProps(11970, rank, level, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            cp.Units[0].Skills.Add(skill); cp.Units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4 }); cp.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(saved.Sequence, saved.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved), "restoration changed selection or triple light damage");
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11970);
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11970).ToArray();
            Check(hits.Length == 3 && hits.Select(hit => hit.TargetId).Distinct().Count() == 1
                && hits.All(hit => hit.TargetId == 2 || hit.TargetId == 3)
                && (!large || hits.All(hit => hit.TargetId == 3)), "five-cell range, single target or large-body priority changed");
            var props = catalog.CalculateSkillProps(11970, rank, level, 8);
            long expected = (long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m);
            Check(hits.All(hit => hit.SourceId == 1 && hit.SkillItemId == 10093 && hit.HitEntityId == 11971
                && hit.DamageEntityId == 11972 && hit.ElementType == 103 && hit.DamageType == 1
                && !hit.TrueDamage && !hit.IsCure && hit.Damage == -expected), "cultivated fixed/percentage ordinary light damage changed");
            var times = hits.Select(hit => hit.Delay - cast.Delay).OrderBy(value => value).ToArray();
            var original = new[] { .85f, 1.05f, 1.2f };
            Check(times.Zip(original, (actual, expectedTime) => Math.Abs(actual - expectedTime) < .0001f).All(value => value),
                "original three lightning hit times changed");
            Check(cp.Units.Single(unit => unit.Id == 4).Hp == 1000000 && cp.Units.Single(unit => unit.Id == 5).Hp == 1000000
                && cp.Units.All(unit => unit.Statuses.Count == 0), "the skill affected a distant target, ally or added an unconfigured status");
            string after = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == after,
                "replay dealt the three hits twice");
            cases++;
        }
        return "Judgment thunder passed: " + cases + " cultivation, five-cell range, large-body priority, triple light timing, replay and restore cases.";
    }

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
