using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWarlockExplosionHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Warlock explosion: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        var lateCultivation = catalog.CalculateSkillProps(12070, 3, 1, 26);
        Check(lateCultivation.ContainsKey(200) && lateCultivation[200] == 7560
            && lateCultivation[190] == 535341,
            "new official cultivation table lost its missing-key default or rank/factor rounding");
        foreach (int rank in new[] { 1, 2, 3, 4, 7, 14, 26, 34 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int subRank in new[] { 5, 8, 26, 40 })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (int distance in new[] { 1, 5 })
        foreach (int seed in new[] { 7, 37 })
        {
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            state.Units.Add(Unit(1, 1, 8, 8, formula));
            state.Units.Add(Unit(2, 2, 8 + distance * direction[0], 8 + distance * direction[1], formula));
            state.Units.Add(Unit(3, 2, 8 + 6 * direction[0], 8 + 6 * direction[1], formula));
            state.Units.Add(Unit(4, 1, 8 + distance * direction[0] - direction[1],
                8 + distance * direction[1] + direction[0], formula));
            foreach (var unit in state.Units) unit.SubRank = subRank;
            var skill = catalog.CreateSkill(12070, 0, 10098, rank, level, subRank,
                catalog.CalculateSkillProps(12070, rank, level, subRank)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            state.Units[0].Skills.Add(skill);
            state.Units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); state.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(state.Sequence, state.Counter + 1).Event;
            restored.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "restore changed target selection or explosion result");
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 12070);
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 12070).ToArray();
            Check(hits.Length == 1 && hits[0].TargetId == 2,
                "the five-cell single-target scope changed: " + string.Join(",", hits.Select(hit => hit.TargetId)));
            var props = catalog.CalculateSkillProps(12070, rank, level, subRank);
            long percent = props.TryGetValue(200, out long percentage) ? percentage : 0L;
            long fixedValue = props.TryGetValue(190, out long flat) ? flat : 0L;
            long expected = (long)((1000m * percent / 10000m + fixedValue) * 1000m / 1010m);
            var hit = hits[0];
            Check(hit.SourceId == 1 && hit.SkillItemId == 10098 && hit.HitEntityId == 12071
                && hit.DamageEntityId == 10004 && hit.ElementType == 102 && hit.DamageType == 1
                && !hit.TrueDamage && !hit.IsCure && hit.HasDamageValue && hit.DamageValue == -expected
                && hit.Damage == -Math.Min(expected, 1000000L),
                "cultivated fixed/percentage fire damage changed: " + rank + "/" + level
                    + " expected=" + expected + " actual=" + hit.Damage + " element=" + hit.ElementType
                    + " type=" + hit.DamageType);
            Check(Math.Abs(hit.Delay - .49f) < .0001f,
                "original cast plus hit timing changed: " + hit.Delay + " versus .49");
            Check(state.Units.Single(unit => unit.Id == 3).Hp == 1000000
                && state.Units.Single(unit => unit.Id == 4).Hp == 1000000
                && state.Units.All(unit => unit.Statuses.Count == 0),
                "the explosion affected a distant target, ally or added an unconfigured status");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(runner.Step(state.Sequence, state.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(state) == saved,
                "receipt replay dealt the explosion twice");
            cases++;
        }
        return "Warlock explosion passed: " + cases + " rank/level, five-cell single-target fire damage, timing, attribution, replay and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }

    public static string VerifyCheckpoint(string json, FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var checkpoint = FightBattleCheckpointSerializer.Load(json);
        var actor = checkpoint.Units.Single(unit => unit.Id == 1);
        Check(actor.Profession == 12 && actor.Skills.Any(skill => skill.ItemId == 10098
            && skill.EntityId == 12070 && skill.Rank == 4 && skill.Level == 2),
            "the acquired save lost its cultivated third-profession skill");
        var receipts = checkpoint.Receipts.Where(receipt => receipt.Event != null
            && receipt.Event.DamageEvents.Any(hit => hit.SkillItemId == 10098)).ToArray();
        Check(receipts.Length > 0, "the Player did not persist actual explosion damage");
        var restored = FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        string before = FightBattleCheckpointSerializer.Save(checkpoint);
        foreach (var receipt in receipts)
            Check(restored.Step(receipt.Sequence, receipt.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == before,
                "restoring the actual Player checkpoint repeated its explosion");
        return "Warlock explosion Player checkpoint passed: cultivated profession, actual damage and restored receipt replay.";
    }
}
