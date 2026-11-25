using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBlockMasteryHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Block mastery: " + message); }

    private static long Cast(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool equipped, bool blocked)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 17,
            Grid = new FightBattleGrid { Width = 12, Height = 12 } };
        foreach (int id in new[] { 1, 2 })
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
                Profession = id == 1 ? 1 : 3, SubRank = 8, Level = 50, X = 5 + id, Y = 5,
                Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            unit.Properties.AddRange(new[] { new FightPropertyValue { Id = 1, Value = 1000 },
                new FightPropertyValue { Id = 2, Value = 10 }, new FightPropertyValue { Id = 3, Value = 1000000 },
                new FightPropertyValue { Id = 4, Value = unit.Turn.Speed } });
            foreach (string name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue",
                "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("BlockPercent"), Value = blocked ? 10000 : -10000 });
            checkpoint.Units.Add(unit);
        }
        var attacker = checkpoint.Units[0]; var holder = checkpoint.Units[1];
        if (equipped)
        {
            var item = items.Required(10587);
            holder.Properties.AddRange(catalog.CalculatePassiveSkillProps(item.PassiveRankPropId,
                item.PassiveGroupLevelPropId, item.PassiveRankFactorId, item.PassivePropFactors,
                7, 30, 8, 10587).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }));
        }
        var skill = catalog.CreateSkill(10600, 0, 10008, 7, 30, 8,
            catalog.CalculateSkillProps(10600, 7, 30, 8).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
        attacker.Skills.Add(skill); attacker.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(0, 1).Event;
        replay.Step(0, 1);
        Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
            "real damage differs after a cold checkpoint");
        var hit = step.DamageEvents.Single(x => x.SourceId == 1 && x.TargetId == 2 && x.SkillEntityId == 10600);
        Check(hit.Block == blocked && hit.Damage < 0, "original skill did not produce the requested hit branch");
        return Math.Abs(hit.Damage);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, string pack, string hash, string manifest)
    {
        var item = items.Required(10587);
        Func<int, int, int, int, IReadOnlyDictionary<int, long>> props = (rank, level, subRank, id) =>
            catalog.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                item.PassiveRankFactorId, item.PassivePropFactors, rank, level, subRank, id);
        var oldFuture = new Dictionary<int, long>(props(7, 30, 26, 0).ToDictionary(x => x.Key, x => x.Value));
        catalog.ApplyPassiveSkillRulePack(pack, hash, manifest, items);
        Check(props(7, 30, 26, 0).OrderBy(x => x.Key).SequenceEqual(oldFuture.OrderBy(x => x.Key)),
            "installing a skill changed the shared cultivation input");
        Check(!props(7, 30, 26, 10587).OrderBy(x => x.Key).SequenceEqual(oldFuture.OrderBy(x => x.Key)),
            "the new official subrank cultivation group was ignored");
        int count = 0;
        foreach (var growth in new[] { new[] { 1, 1, 5 }, new[] { 7, 30, 8 }, new[] { 7, 30, 26 } })
        {
            var values = props(growth[0], growth[1], growth[2], 10587);
            int percent = formula.PropertyId("BlockValuePercent"), fixedValue = formula.PropertyId("BlockValue");
            Check(values.Count == 2 && values[percent] > 0 && values[fixedValue] > 0,
                "both official resistance properties were not cultivated at " + string.Join("/", growth)
                    + ": " + string.Join(",", values.Select(x => x.Key + "=" + x.Value)));
            foreach (bool blocked in new[] { false, true })
            foreach (int mode in new[] { 1, 2, 3 })
            foreach (double avoid in new[] { 0.0, 0.75 })
            {
                var source = new Dictionary<int, long> { { 1, 1000 } };
                formula.AddPropertyValue(source, "SkillAttack1", 1.0);
                source[formula.PropertyId("BaseBlockAvoidPercentValue")] = 10000;
                formula.AddPropertyValue(source, "BlockAvoidPercent", avoid);
                var target = new Dictionary<int, long> { { 2, 10 }, { 3, 1000000 } };
                if ((mode & 1) != 0) target[percent] = values[percent];
                if ((mode & 2) != 0) target[fixedValue] = values[fixedValue];
                var request = new FightDamageFormulaRequest { SourceProps = source, TargetProps = target,
                    SourceType = 1, TargetType = 1, SourceLevel = 50, TargetLevel = 50,
                    SourceSubRank = growth[2], TargetSubRank = growth[2], TargetHp = 1000000,
                    SkillDamageBasicPropType = 1, SkillDamagePropType = formula.PropertyId("SkillAttack1") };
                double amount = 1000.0 * 1000 / 1010;
                if (blocked)
                {
                    double fixedReduce = (mode & 2) != 0 ? values[fixedValue] : 0;
                    amount += Math.Max(-0.9 * amount, -fixedReduce);
                    double ratio = (mode & 1) != 0 ? values[percent] / 10000.0 : 0;
                    amount /= Math.Max(1.5, 1 + ratio - avoid);
                }
                var result = formula.Calculate(request, new FightDamageTypeData { Block = blocked });
                Check(Math.Abs(result.Damage) == (long)amount,
                    "original fixed reduction/clamp/percentage order differs: " + result.Damage + " / " + (long)amount);
                count++;
            }
        }
        bool rejected = false;
        try { catalog.ApplyPassiveSkillRulePack(pack, hash, manifest, items); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "duplicate source was accepted");
        rejected = false;
        try { catalog.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
            item.PassiveRankFactorId, new Dictionary<int, long>(), 7, 30, 8, 10587); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "mismatched item factors were accepted");
        Check(Cast(catalog, formula, items, false, false) == Cast(catalog, formula, items, true, false),
            "non-blocked damage changed");
        Check(Cast(catalog, formula, items, true, true) < Cast(catalog, formula, items, false, true),
            "real blocked damage did not decrease");
        return "Block mastery passed: " + count + " fixed/percentage/clamp cases, four real hit/checkpoint cases, source isolation and rejection checks.";
    }
}
