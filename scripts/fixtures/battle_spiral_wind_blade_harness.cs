using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSpiralWindBladeHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Spiral wind blade: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, applied = 0, rejected = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (bool large in new[] { false, true })
        foreach (int rate in new[] { -10000, 0, 100000 })
        {
            int dx = direction[0], dy = direction[1];
            var state = State(rank * 7919 + level * 137 + dx * 7 + dy, formula);
            var target = Unit(2, 2, 10 + 3 * dx, 10 + 3 * dy, formula);
            if (large) target.BodyOffsets.Add(new FightGridPoint(-dx, -dy));
            state.Units.Add(target);
            state.Units.Add(Unit(3, 2, 10 + 4 * dx - dy, 10 + 4 * dy + dx, formula));
            state.Units.Add(Unit(4, 2, 10 + 5 * dx, 10 + 5 * dy, formula));
            state.Units.Add(Unit(5, 1, 10 + 3 * dx + dy, 10 + 3 * dy - dx, formula));
            var executor = Begin(state, catalog, formula, items, rank, level, rate);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = Step(state, executor, copy, restored);
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11960);
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 11960).ToArray();
            Check(cast.Presentation.TargetX == target.X && cast.Presentation.TargetY == target.Y
                && hits.Length == (large ? 9 : 6) && hits.Count(hit => hit.TargetId == 2) == (large ? 6 : 3)
                && hits.Count(hit => hit.TargetId == 3) == 3,
                "the three-cell point selection or square/body scope changed");
            long expected = Damage(catalog, rank, level, 11960);
            Check(hits.All(hit => hit.SourceId == 1 && hit.SkillItemId == 10091 && hit.HitEntityId == 11961
                && hit.DamageEntityId == 11962 && hit.ElementType == 100 && hit.DamageType == 1
                && !hit.TrueDamage && !hit.IsCure && hit.Damage == -expected),
                "cultivated fixed/percentage wind damage changed");
            var primary = hits.Where(hit => hit.TargetId == 2 && hit.DamageX == target.X && hit.DamageY == target.Y)
                .OrderBy(hit => hit.Delay).ToArray();
            Check(primary.Length == 3 && primary.Select((hit, index) =>
                Math.Abs(hit.Delay - cast.Delay - .65f - index * .2f) < .0001f).All(value => value),
                "the original .65/.85/1.05 second wave timing changed");
            foreach (var unit in state.Units.Where(unit => unit.Side == 2 && unit.Id != 4))
            {
                var tear = unit.Statuses.SingleOrDefault(status => status.ClassId == 24376);
                if (rate == 100000) Check(tear != null, "increased effect hit did not affect the 40% baseline rolls");
                if (rate == -10000) Check(tear == null, "zero effect chance still applied laceration");
                if (rate == 0) { if (tear == null) rejected++; else applied++; }
                if (tear != null) Check(tear.CreatorId == 1 && tear.ApplyingSkillItemId == 10091
                    && tear.SourceSkillRank == rank && tear.SourceSkillLevel == level && tear.SourceSubRank == 8
                    && tear.DurationRound == 1 && tear.RoundTarget == 2 && tear.RoundUpdateTiming == 1
                    && tear.ActionStartRulesSupported && tear.ActionStartActionTypes.SequenceEqual(new[] { 1, 6, 7, 8 }),
                    "laceration lost its cultivated source, clock or supported action-start definition");
            }
            Check(state.Units.Single(unit => unit.Id == 4).Hp == 1000000
                && state.Units.Single(unit => unit.Id == 5).Hp == 1000000,
                "the square damaged a distant or friendly unit");
            cases++;
        }
        Check(applied > 0 && rejected > 0, "the original 40% status chance became unconditional");
        foreach (int rank in new[] { 1, 7 })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        {
            var state = State(37, formula);
            var target = Unit(2, 2, 10 + direction[0] * 3, 10 + direction[1] * 3, formula);
            target.Profession = 4;
            target.MoveDist = 8; target.MoveMask = 5;
            target.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MoveDist"), Value = 8 });
            var slash = Skill(catalog, 10600, 10008, rank, 2);
            target.Skills.Add(slash); target.SkillState.Slots.Add(catalog.CreateSkillSlot(slash, 0));
            state.Units.Add(target);
            var executor = Begin(state, catalog, formula, items, rank, 2, 100000);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            Step(state, executor, copy, restored);
            FightBattleStepEvent movement = null; bool expired = false;
            var trace = new List<string>();
            for (int i = 0; i < 12 && !state.Finished; i++)
            {
                var step = Step(state, executor, copy, restored);
                trace.Add(step.ActorId + ":moves=" + string.Join(",", step.Movements.Select(move => move.UnitId + "/" + move.MoveBehaviourId))
                    + ":damage=" + string.Join(",", step.DamageEvents.Select(hit => hit.SkillEntityId))
                    + ":statuses=" + string.Join(",", target.Statuses.Select(status => status.ClassId + "/" + status.DurationRound)));
                if (step.Movements.Any(value => value.UnitId == 2)) movement = step;
                expired |= step.StatusViewEvents.Any(view => view.Status.ClassId == 24376
                    && view.Kind == FightBattleStatusViewEventKind.End);
                if (movement != null && expired) break;
            }
            Check(movement != null && expired, "the original melee unit did not move while torn or the one-round status never expired: "
                + rank + "/" + direction[0] + "," + direction[1] + ": " + string.Join(";", trace));
            var tearHits = movement.DamageEvents.Where(hit => hit.SkillEntityId == 11950).ToArray();
            Check(tearHits.Length == 1 && tearHits[0].SourceId == 1 && tearHits[0].TargetId == 2
                && tearHits[0].SkillItemId == 10091 && tearHits[0].HitEntityId == 11951
                && tearHits[0].DamageEntityId == 10004 && tearHits[0].ElementType == 100
                && tearHits[0].Damage == -Damage(catalog, rank, 2, 11950),
                "ordinary movement did not trigger exactly one original cultivated wind laceration from its creator");
            cases++;
        }
        return "Spiral wind blade passed: " + cases + " cultivated triple wind, square/body scope, probability, movement laceration, expiry and restore cases.";
    }

    private static long Damage(FightOfficialCombatCatalog catalog, int rank, int level, int entity)
    {
        var props = catalog.CalculateSkillProps(entity, rank, level, 8);
        return (long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m);
    }

    private static FightBattleStepEvent Step(FightBattleCheckpoint state, FightBattleExecutor executor,
        FightBattleCheckpoint copy, FightBattleExecutor restored)
    {
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        restored.Step(copy.Sequence, copy.Counter + 1);
        Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
            "restoration changed wind waves, laceration, movement or the one-round clock");
        string saved = FightBattleCheckpointSerializer.Save(state);
        Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
            "receipt replay applied a wave or laceration twice");
        return step;
    }

    private static FightBattleExecutor Begin(FightBattleCheckpoint state, FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int rank, int level, int rate)
    {
        var source = state.Units[0];
        source.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = rate });
        var skill = Skill(catalog, 11960, 10091, rank, level);
        source.Skills.Add(skill); source.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(state.Units.Where(unit => unit.Side == 2).Select(unit => unit.Id));
        state.Waves.Add(wave);
        return FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
    }

    private static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog catalog, int entity, int item, int rank, int level)
        => catalog.CreateSkill(entity, 0, item, rank, level, 8, catalog.CalculateSkillProps(entity, rank, level, 8)
            .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());

    private static FightBattleCheckpoint State(int seed, FightDamageFormula formula)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        state.Units.Add(Unit(1, 1, 10, 10, formula)); return state;
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
