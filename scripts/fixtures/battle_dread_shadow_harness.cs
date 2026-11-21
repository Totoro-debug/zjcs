using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleDreadShadowHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Dread shadow: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0, applied = 0, resisted = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int cultivation in new[] { 0, 1 }) foreach (int type in new[] { 1, 2 })
        foreach (int resistance in new[] { -1, 0, 1 }) foreach (bool large in new[] { false, true })
        foreach (int seed in Enumerable.Range(1, 8))
        {
            int dx = direction[0], dy = direction[1];
            int rank = cultivation == 0 ? 4 : 7, level = cultivation == 0 ? 2 : 20;
            var actor = Unit(1, 1, 8, 8, 1, formula);
            actor.Direction = dx < 0 ? 1 : dx > 0 ? 3 : dy > 0 ? 2 : 0;
            var primary = Unit(2, 2, 8 + 2 * dx, 8 + 2 * dy, type, formula);
            if (large) primary.BodyOffsets.Add(new FightGridPoint(-dx, -dy));
            primary.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = resistance * 100000 });
            var near = Unit(3, 2, 8 + dy, 8 - dx, 2, formula);
            var outside = Unit(4, 2, 8 + 3 * dx, 8 + 3 * dy, 2, formula);
            var ally = Unit(5, 1, 8 - dy, 8 + dx, 1, formula);
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 18, Height = 18, DefaultGroundType = 2 } };
            state.Units.AddRange(new[] { actor, primary, near, outside, ally });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4 }); state.Waves.Add(wave);
            var props = catalog.CalculateSkillProps(12130, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value);
            var skill = catalog.CreateSkill(12130, 0, 10104, rank, level, 8,
                props.Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null;
            FightBattleSkillCastEvent cast = null;
            while (cast == null && state.Counter < 8 && !state.Finished)
            {
                step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10104);
            }
            Check(cast != null, "the original skill did not cast");
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 12130).ToArray();
            Check(hits.Length == (large ? 3 : 2) && hits.Count(hit => hit.TargetId == 2) == (large ? 2 : 1)
                && hits.Count(hit => hit.TargetId == 3) == 1, "self-centred radius two, body cells or friend exclusion changed");
            long expected = -(long)((1000 * formula.ReadProperty(props, 200) + formula.ReadProperty(props, 190)) * 1000 / 1010);
            Check(hits.All(hit => hit.SourceId == 1 && hit.HitEntityId == 12131 && hit.DamageEntityId == 12132
                && hit.ElementType == 104 && hit.DamageType == 1 && hit.Damage == expected && !hit.IsCure
                && !hit.TrueDamage && Math.Abs(hit.Delay - cast.Delay - .42f) < .0001f),
                "the cultivated dark damage or authored timing changed");
            bool hasFear = primary.Statuses.Any(status => status.ClassId == 24194);
            if (resistance < 0) Check(hasFear, "guaranteed fear was resisted");
            if (resistance > 0) Check(!hasFear, "fear ignored effect resistance");
            if (resistance == 0) { if (hasFear) applied++; else resisted++; }
            foreach (var owner in new[] { primary, near }) foreach (var status in owner.Statuses.Where(value => value.ClassId == 24194))
                Check(status.CreatorId == 1 && status.OwnerId == owner.Id && status.ActionType == 78
                    && status.DurationRound == 2 && status.CanDispersed && status.SourceSkillRank == rank
                    && status.SourceSkillLevel == level && status.SourceSubRank == 8 && status.ApplyingSkillItemId == 10104,
                    "fear lost its original source, action, cultivation or two-turn lifetime");
            Check(!outside.Statuses.Any(status => status.ClassId == 24194)
                && !ally.Statuses.Any(status => status.ClassId == 24194), "fear escaped the actual hostile hit area");
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "checkpoint restoration changed damage, fear recipients or random progress");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
                "receipt replay repeated fear or damage");
            cases++;
        }
        Check(applied > 0 && resisted > 0, "the sixty-percent nominal fear became unconditional or impossible");
        return "Dread shadow passed: " + cases + " directions, cultivation, dark area, body cells, character types, resistance, fear and restoration cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, int type, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = type, Profession = 8, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var property in new[] { new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
            new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
            new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed },
            new FightPropertyValue { Id = formula.PropertyId("BaseEffectRate"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("BaseEffectDodge"), Value = 1000 } }) unit.Properties.Add(property);
        return unit;
    }
}
