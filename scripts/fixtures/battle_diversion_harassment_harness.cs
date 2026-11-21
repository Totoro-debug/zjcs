using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleDiversionHarassmentHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Diversion harassment: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, nominalApplied = 0, nominalRejected = 0;
        foreach (int cultivation in new[] { 0, 1 })
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int type in new[] { 1, 4, 2, 3, 7 })
        foreach (int resistance in new[] { -1, 0, 1 })
        foreach (int seed in Enumerable.Range(1, 8))
        {
            int dx = direction[0], dy = direction[1];
            var actor = Unit(1, 1, 10, 10, 1, formula);
            var primary = Unit(2, 2, 10 + dx, 10 + dy, type, formula);
            var near = Unit(3, 2, primary.X + 2 * dx, primary.Y + 2 * dy, 2, formula);
            var outside = Unit(4, 2, primary.X + 5 * dx, primary.Y + 5 * dy, 2, formula);
            var ally = Unit(5, 1, primary.X + dx + dy, primary.Y + dy - dx, 1, formula);
            primary.Properties.Add(new FightPropertyValue {
                Id = formula.PropertyId("EffectDodgeValue"), Value = resistance * 100000
            });
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, primary, near, outside, ally });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4 });
            checkpoint.Waves.Add(wave);
            int rank = cultivation == 0 ? 4 : 5, level = cultivation == 0 ? 2 : 20;
            var props = catalog.CalculateSkillProps(11890, rank, level, 8);
            var skill = catalog.CreateSkill(11890, 0, 10084, rank, level, 8,
                props.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null;
            FightBattleSkillCastEvent cast = null;
            while (cast == null && checkpoint.Counter < 8 && !checkpoint.Finished)
            {
                step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                restored.Step(copy.Sequence, copy.Counter + 1);
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10084);
            }
            Check(cast != null, "original cultivated skill did not cast");
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11890).ToArray();
            Check(hits.Select(value => value.TargetId).OrderBy(value => value).SequenceEqual(new[] { 2, 3 }),
                "target-centred radius two differs: hits=" + string.Join(",", hits.Select(value => value.TargetId))
                + ", type=" + type + ", direction=" + dx + "/" + dy + ", actor=" + actor.X + "/" + actor.Y);
            long expected = -(long)((1000 * formula.ReadProperty(props.ToDictionary(p => p.Key, p => p.Value), 200)
                + formula.ReadProperty(props.ToDictionary(p => p.Key, p => p.Value), 190)) * 1000 / 1010);
            double cx = cast.Presentation.TargetX - cast.Presentation.SourceX;
            double cy = cast.Presentation.TargetY - cast.Presentation.SourceY;
            double expectedDelay = cast.Delay + .35f + Math.Sqrt(cx * cx + cy * cy) * .05f + .06f;
            Check(expected < 0 && hits.All(value => value.HitEntityId == 11891 && value.DamageEntityId == 11892
                && value.ElementType == 103 && value.DamageType == 1 && value.Damage == expected
                && !value.TrueDamage && !value.IsCure && Math.Abs(value.Delay - expectedDelay) < .0001f),
                "light damage, cultivation or original delay differs");
            bool applied = primary.Statuses.Any(status => status.ClassId == 24216);
            bool nonCharacter = type == 2 || type == 3 || type == 7;
            if (nonCharacter || resistance < 0) Check(applied, "guaranteed original taunt was resisted");
            if (!nonCharacter && resistance > 0) Check(!applied, "character taunt ignored effect resistance");
            if (!nonCharacter && resistance == 0)
            {
                if (applied) nominalApplied++; else nominalRejected++;
            }
            Check(near.Statuses.Any(status => status.ClassId == 24216)
                && !ally.Statuses.Any(status => status.ClassId == 24216)
                && !outside.Statuses.Any(status => status.ClassId == 24216), "taunt recipients differ from hit recipients");
            if (applied)
            {
                var taunt = primary.Statuses.Single(status => status.ClassId == 24216);
                Check(taunt.CreatorId == actor.Id && taunt.DurationRound == 1 && taunt.CanDispersed
                    && taunt.ActionType == 58, "one-round taunt lost its owner/action/lifecycle");
            }
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                "save restoration changed the area or probability result");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved, "receipt replay taunted twice");
            cases++;
        }
        Check(nominalApplied > 0 && nominalRejected > 0, "85-percent character taunt became unconditional or impossible");
        return "Diversion harassment passed: " + cases
            + " direction, character category, resistance, light damage, taunt lifecycle and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, int type, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = type, Profession = 4, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000
        };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new[] {
            new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
            new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
            new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed },
            new FightPropertyValue { Id = formula.PropertyId("BaseEffectRate"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("BaseEffectDodge"), Value = 1000 }
        }) unit.Properties.Add(value);
        return unit;
    }
}
