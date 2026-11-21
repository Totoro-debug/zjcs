using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleHolyPurificationHarness
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Holy purification: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var removedBuffs = new HashSet<int>();
        int cases = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { -1, 0 }, new[] { 1, 0 } })
        foreach (int seed in new[] { 1, 17, 31, 91 })
        {
            int dx = direction[0], dy = direction[1];
            FightGridPoint Position(int length, int width = 0) => new FightGridPoint(
                8 + length * dx + width * dy, 8 + length * dy - width * dx);
            var actor = Unit(1, 1, Position(0), formula);
            var front = Unit(2, 2, Position(1), formula);
            var edge = Unit(3, 2, Position(5), formula);
            var beyond = Unit(4, 2, Position(6), formula);
            var side = Unit(5, 2, Position(3, 1), formula);
            var ally = Unit(6, 1, Position(3), formula);
            int buffOne = 24237, buffTwo = 24214, negative = 24040;
            foreach (var unit in new[] { front, beyond, side, ally })
            {
                foreach (int statusId in new[] { buffOne, buffTwo, negative })
                    unit.Statuses.Add(catalog.CreateStatus(statusId, unit.Id, unit.Id,
                        unit.Id * 100000 + statusId));
            }
            edge.Statuses.Add(catalog.CreateStatus(negative, edge.Id, edge.Id, 800000));
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 18, Height = 18, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, front, edge, beyond, side, ally });
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(new[] { front.Id, edge.Id, beyond.Id, side.Id });
            checkpoint.Waves.Add(wave);
            var skill = catalog.CreateSkill(11860, 0, 10079, 5, 20, 5,
                catalog.CalculateSkillProps(11860, 5, 20, 5)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                "loading changed the light damage or random dispel");
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11860);
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11860).ToArray();
            Check(hits.Select(value => value.TargetId).OrderBy(id => id).SequenceEqual(new[] { front.Id, edge.Id }),
                "the authored 1x5 line included an ally or missed an in-range enemy");
            Check(hits.All(value => value.HitEntityId == 11861 && value.DamageEntityId == 11862
                && value.DamageType == 1 && value.ElementType == 103 && value.TrueDamage
                && value.Damage < 0 && !value.IsCure && Math.Abs(value.Delay - cast.Delay - .4f) < .0001f),
                "the original light destruction damage or hit delay differs");
            Check(front.Statuses.Count(value => value.ClassId == buffOne || value.ClassId == buffTwo) == 1
                && front.Statuses.Any(value => value.ClassId == negative) && edge.Statuses.Any(value => value.ClassId == negative)
                && new[] { beyond, side, ally }.All(unit => unit.Statuses.Count == 3)
                && step.DispersedUnitIds.SequenceEqual(new[] { front.Id }),
                "the skill must remove one eligible buff and preserve negative statuses");
            removedBuffs.Add(front.Statuses.Any(value => value.ClassId == buffOne) ? buffTwo : buffOne);
            cases++;
        }
        Check(removedBuffs.Count == 2, "the tested battle seeds did not exercise both random removals");
        return "Holy purification passed: " + cases + " light destruction, 1x5 coverage, random dispel and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, FightGridPoint position, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = side, Profession = 3,
            Level = 50, SubRank = 5, X = position.X, Y = position.Y,
            Hp = 100000, MaxHp = 100000, LastMaxHp = 100000, MoveMask = 5
        };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
        return unit;
    }
}
