using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleLightIncarnationHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Light incarnation: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (bool equipped in new[] { false, true })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (bool large in new[] { false, true })
        foreach (int seed in new[] { 7, 37 })
        {
            int dx = direction[0], dy = direction[1];
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            cp.Units.Add(Unit(1, 1, 10, 10, formula));
            var item = items.Required(10525);
            Check(item.EntityId == 0 && item.PassiveStatusIds.Count() == 0 && item.PassivePropFactors.Count == 1,
                "property-only secret unexpectedly acquired a status or child graph");
            var bonus = catalog.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                item.PassiveRankFactorId, item.PassivePropFactors, rank, level, 8, 10525);
            Check(bonus.Count == 1 && bonus.ContainsKey(413) && bonus[413] > 0,
                "cultivation lost the sole light affinity contribution");
            Check(formula.PropertyId("LightDamageAdd") == 413, "official light affinity property changed");
            // Current official 1913 level rows and 2051 rank factors, independent
            // of the runtime cultivation calculation under test.
            long levelValue = level == 1 ? 595 : level == 2 ? 605 : 780;
            long rankFactor = rank == 1 ? 2800 : rank == 2 ? 3000 : rank == 4 ? 5100
                : rank == 5 ? 5400 : rank == 6 ? 8000 : 8800;
            Check(bonus[413] == levelValue * rankFactor / 10000,
                "cultivation differs from official level values and rank factors");
            long affinity = equipped ? bonus[413] : 0;
            cp.Units[0].Properties.Add(new FightPropertyValue { Id = 413, Value = affinity });
            foreach (int element in new[] { 0, 101, 102, 103, 104, 105 })
            {
                var sourceProps = cp.Units[0].Properties.ToDictionary(value => value.Id, value => value.Value);
                sourceProps[200] = 10000;
                var request = new FightDamageFormulaRequest {
                    SourceProps = sourceProps,
                    TargetProps = Unit(2, 2, 1, 1, formula).Properties.ToDictionary(value => value.Id, value => value.Value),
                    SkillDamageBasicPropType = 1, SkillDamagePropType = 200, SkillDamageFixedPropType = 190,
                    SourceType = 1, TargetType = 1, SourceLevel = 70, TargetLevel = 70,
                    SourceSubRank = 8, TargetSubRank = 8, SourceElementType = element
                };
                long actualDamage = formula.Calculate(request, new FightDamageTypeData()).Damage;
                // Original Damage averages all five affinities for physical damage.
                decimal scale = element == 0 ? 1m + affinity / 50000m
                    : element == 103 ? 1m + affinity / 10000m : 1m;
                Check(actualDamage == -(long)(1000m * 1000m / 1010m * scale),
                    "light affinity changed another element or lost the physical average: " + element + "/" + affinity + "/" + actualDamage + "/" + (long)(1000m * 1000m / 1010m * scale));
            }
            cp.Units.Add(Unit(2, 2, 10 + dx * 2, 10 + dy * 2, formula));
            var far = Unit(3, 2, 10 + dx * 5, 10 + dy * 5, formula);
            if (large) far.BodyOffsets.Add(new FightGridPoint(dx, dy));
            cp.Units.Add(far);
            cp.Units.Add(Unit(4, 2, 10 - dx * 6, 10 - dy * 6, formula));
            cp.Units.Add(Unit(5, 1, 10 + dx * 3, 10 + dy * 3, formula));
            var skill = catalog.CreateSkill(11970, 0, 10093, rank, 2, 8,
                catalog.CalculateSkillProps(11970, rank, 2, 8)
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
            var props = catalog.CalculateSkillProps(11970, rank, 2, 8);
            long expected = (long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m * (1m + affinity / 10000m));
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
        return "Light incarnation passed: " + cases + " equipped/unequipped cultivation, light affinity damage formula, triple hit timing and checkpoint cases.";
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
