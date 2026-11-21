using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleShieldChargeHarness
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Shield charge: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { -1, 0 }, new[] { 1, 0 } })
        foreach (string mode in new[] { "clear", "blocked-landing", "blocked-side" })
        {
            int dx = direction[0], dy = direction[1];
            FightGridPoint Point(int length, int width) => new FightGridPoint(10 + length * dx + width * dy,
                10 + length * dy - width * dx);
            var actor = Unit(1, 1, Point(0, 0), formula);
            var left = Unit(2, 2, Point(2, -1), formula);
            var right = Unit(3, 2, Point(3, 1), formula);
            var center = Unit(4, 2, Point(1, 0), formula);
            var outside = Unit(5, 2, Point(3, 3), formula);
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 }
            };
            if (mode == "blocked-landing") checkpoint.Grid.BlockedPositions.Add(Point(5, 0));
            if (mode == "blocked-side") checkpoint.Grid.BlockedPositions.Add(Point(2, -2));
            checkpoint.Units.AddRange(new[] { actor, left, right, center, outside });
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(new[] { left.Id, right.Id, center.Id, outside.Id });
            checkpoint.Waves.Add(wave);
            var skill = catalog.CreateSkill(11730, 0, 10073, 5, 20, 5,
                catalog.CalculateSkillProps(11730, 5, 20, 5)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                "loading changed the charge path, hits or side movement");
            if (mode == "blocked-landing")
            {
                Check(step.SkillCasts.Count == 0 && step.DamageEvents.Count == 0 && step.Movements.Count == 0
                    && actor.SkillState.Slots[0].ReleasedTimes == 0, "blocked landing consumed the original charge");
                cases++;
                continue;
            }
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11730);
            var charge = step.Movements.Single(value => value.UnitId == actor.Id && value.MoveBehaviourId == 11732);
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11730).ToArray();
            Check(charge.Path.SequenceEqual(Enumerable.Range(0, 6).Select(i => Point(i, 0)))
                && Math.Abs(charge.Delay - cast.Delay - .82f) < .0001f && Math.Abs(charge.MoveDuration - .36f) < .0001f,
                "the charge did not follow the original straight five-cell path");
            var targets = new[] { center, left, right };
            Check(hits.Length == targets.Length, "the 3x4 area did not hit exactly its three enemies");
            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                var hit = hits.Single(value => value.TargetId == target.Id);
                Check(hit.HitEntityId == 11731 && hit.DamageEntityId == 11733 && hit.DamageType == 1
                    && hit.ElementType == 0 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                    && Math.Abs(hit.Delay - cast.Delay - .82f - (i + 1) * .08f) < .0001f,
                    "the attack's physical damage or per-row impact delay changed");
                var push = step.Movements.SingleOrDefault(value => value.UnitId == target.Id
                    && value.MoveBehaviourId == 11734);
                if (target == left && mode == "blocked-side")
                    Check(push == null && Point(2, -1).Equals(new FightGridPoint(target.X, target.Y)),
                        "a blocked side hit moved the target through the obstacle or toward the charge line");
                else
                    Check(push != null && push.Path.Count == 2 && Math.Abs(push.Delay - hit.Delay) < .0001f
                        && (target == center ? push.Path[1].Equals(Point(1, 1)) || push.Path[1].Equals(Point(1, -1))
                            : push.Path[1].Equals(target == left ? Point(2, -2) : Point(3, 2))),
                        "impact did not displace the enemy exactly one cell away from the original charge line");
            }
            cases++;
        }
        return "Shield charge passed: " + cases + " original path, physical impact, side push and restore cases.";
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
