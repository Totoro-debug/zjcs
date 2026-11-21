using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSurgingTideHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Surging tide: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (int rank in new[] { 4, 5 })
        foreach (int level in new[] { 2, 20 })
        foreach (bool friendly in new[] { false, true })
        {
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 }
            };
            var actor = Unit(1, 1, 10, 10, formula);
            actor.Direction = direction;
            checkpoint.Units.Add(actor);
            var wave = new FightBattleWaveState();
            int id = 2;
            for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++)
            {
                int distance = Math.Abs(x) + Math.Abs(y);
                if (distance == 0 || distance > 3) continue;
                var unit = Unit(id++, friendly && x == 0 && y == -1 ? 1 : 2, 10 + x, 10 + y, formula);
                if (x == 0 && y == 1)
                {
                    unit.BodyOffsets.Add(new FightGridPoint(1, 0));
                }
                checkpoint.Units.Add(unit);
                if (unit.Side == 2) wave.UnitIds.Add(unit.Id);
            }
            checkpoint.Waves.Add(wave);
            var values = catalog.CalculateSkillProps(10880, rank, level, 8);
            var skill = catalog.CreateSkill(10880, 0, 10047, rank, level, 8,
                values.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null;
            FightBattleSkillCastEvent cast = null;
            while (cast == null && checkpoint.Counter < 8 && !checkpoint.Finished)
            {
                step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10047);
            }
            Check(cast != null, "the original skill did not cast");
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 10880).ToArray();
            // This skill's confirmed solo exception settles each target only once;
            // the twelve official scope offsets and their presentation remain intact.
            var expected = checkpoint.Units.Where(unit => unit.Side == 2)
                .SelectMany(unit => unit.BodyOffsets.DefaultIfEmpty(new FightGridPoint(0, 0))
                    .Where(body => Math.Abs(unit.X + body.X - actor.X) + Math.Abs(unit.Y + body.Y - actor.Y) <= 2)
                    .Select(body => unit.Id)).Distinct().OrderBy(value => value).ToArray();
            Check(hits.Select(value => value.TargetId).OrderBy(value => value).SequenceEqual(expected),
                "radius-two targets differ: expected [" + string.Join(",", expected) + "] but received ["
                + string.Join(",", hits.Select(value => value.TargetId)) + "]; direction=" + direction
                + ", position=" + actor.X + "/" + actor.Y);
            // With equal level/subrank and no other modifiers, the official damage
            // formula reduces to (attack * coefficient + fixed) * attack / (attack + defence).
            long expectedDamage = -(long)((1000 * formula.ReadProperty(values.ToDictionary(p => p.Key, p => p.Value), 200)
                + formula.ReadProperty(values.ToDictionary(p => p.Key, p => p.Value), 190)) * 1000 / 1010);
            Check(expectedDamage < 0 && hits.All(value => value.SourceId == actor.Id
                && value.HitEntityId == 10881 && value.DamageEntityId == 10024 && value.ElementType == 101
                && value.DamageType == 1 && !value.TrueDamage && !value.IsCure
                && value.Damage == expectedDamage && value.AfterHp - value.BeforeHp == expectedDamage
                && Math.Abs(value.Delay - cast.Delay - .3f) < .0001f),
                "water damage, cultivation, defence reduction or original hit timing differs");
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                "disk restoration changed the area damage");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved, "receipt replay applied damage again");
            cases++;
        }
        return "Surging tide passed: " + cases + " area, large-body, cultivation, water damage, timing and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 4, Level = 50, SubRank = 8,
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
