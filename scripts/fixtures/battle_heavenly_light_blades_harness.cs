using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleHeavenlyLightBladesHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Heavenly light blades: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int rank in new[] { 4, 5 })
        foreach (int level in new[] { 2, 20 })
        foreach (bool friendly in new[] { false, true })
        {
            int dx = direction[0], dy = direction[1];
            var actor = Unit(1, 1, 10, 10, formula);
            actor.Direction = dx < 0 ? 1 : dx > 0 ? 3 : dy > 0 ? 2 : 0;
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 22, Height = 22, DefaultGroundType = 2 }
            };
            checkpoint.Units.Add(actor);
            var wave = new FightBattleWaveState();
            int id = 2, largeId = 0;
            for (int row = 1; row <= 5; row++) for (int column = -2; column <= 2; column++)
            {
                // Leave room for the second cell of the actual large enemy.
                if (row == 3 && column == 1) continue;
                int side = friendly && row == 2 && column == -1 ? 1 : 2;
                var unit = Unit(id++, side, 10 + row * dx + column * dy, 10 + row * dy - column * dx, formula);
                if (row == 3 && column == 0)
                {
                    largeId = unit.Id; unit.BodyOffsets.Add(new FightGridPoint(dy, -dx));
                }
                checkpoint.Units.Add(unit);
                if (side == 2) wave.UnitIds.Add(unit.Id);
            }
            checkpoint.Waves.Add(wave);
            var props = catalog.CalculateSkillProps(11900, rank, level, 8);
            var skill = catalog.CreateSkill(11900, 0, 10085, rank, level, 8,
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
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10085);
            }
            Check(cast != null && actor.X == 10 && actor.Y == 10, "front-area cast moved or did not release");
            var expected = checkpoint.Units.Where(unit => unit.Side == 2)
                .SelectMany(unit => unit.BodyOffsets.Select(body => new {
                    unit.Id, X = unit.X + body.X - 10, Y = unit.Y + body.Y - 10
                })).Where(cell => cell.X * dx + cell.Y * dy >= 1 && cell.X * dx + cell.Y * dy <= 4
                    && Math.Abs(cell.X * dy - cell.Y * dx) <= 1).ToArray();
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11900).ToArray();
            Check(hits.Select(value => value.TargetId).OrderBy(value => value).SequenceEqual(
                    expected.Select(cell => cell.Id).OrderBy(value => value))
                && hits.Count(value => value.TargetId == largeId) == 2 && hits.Length == (friendly ? 11 : 12),
                "three-by-four area, friendly exclusion or large-body occurrences differ");
            long expectedDamage = -(long)((1000 * formula.ReadProperty(props.ToDictionary(p => p.Key, p => p.Value), 200)
                + formula.ReadProperty(props.ToDictionary(p => p.Key, p => p.Value), 190)) * 1000 / 1010);
            foreach (var hit in hits)
            {
                var target = checkpoint.Units.Single(unit => unit.Id == hit.TargetId);
                int row = (target.X - 10) * dx + (target.Y - 10) * dy;
                float[] delays = { 0f, .02f, .06f, .12f };
                Check(hit.SourceId == actor.Id && hit.HitEntityId >= 11901 && hit.HitEntityId <= 11903
                    && hit.DamageEntityId == 10026 && hit.ElementType == 103 && hit.DamageType == 1
                    && !hit.TrueDamage && !hit.IsCure && hit.Damage == expectedDamage
                    && Math.Abs(hit.Delay - cast.Delay - .76f - delays[row - 1]) < .0001f,
                    "cultivated light damage or original row timing differs");
            }
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                "checkpoint restoration changed the three authored hit columns");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved, "receipt replay repeated light damage");
            cases++;
        }
        return "Heavenly light blades passed: " + cases
            + " direction, full area, large-body, cultivation, light damage, row timing and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 4, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000
        };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new[] {
            new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
            new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
            new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed }
        }) unit.Properties.Add(value);
        return unit;
    }
}
