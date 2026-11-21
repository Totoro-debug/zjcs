using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStarShatteringSlashHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] offset in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (bool largeBodyInRange in new[] { false, true })
        {
            int dx = offset[0], dy = offset[1];
            var actor = Unit(1, 1, 6, 6, formula);
            var small = Unit(2, 2, 6 - dy, 6 + dx, formula);
            var large = Unit(3, 2, 6 + 2 * dx, 6 + 2 * dy, formula);
            if (largeBodyInRange)
            {
                large.BodyOffsets.Add(new FightGridPoint(0, 0));
                large.BodyOffsets.Add(new FightGridPoint(-dx, -dy));
            }
            var ally = Unit(4, 1, 6 + dy, 6 - dx, formula);
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, small, large, ally });
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(new[] { small.Id, large.Id });
            checkpoint.Waves.Add(wave);
            var skill = catalog.CreateSkill(10940, 0, 10039, 5, 20, 5,
                catalog.CalculateSkillProps(10940, 5, 20, 5)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
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
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10039);
            }
            if (cast == null)
            {
                var slot = actor.SkillState.Slots.Single();
                var selectable = FightBattleTargeting.SelectInSkillRange(actor, checkpoint.Units, skill);
                throw new InvalidOperationException("Star-shattering slash: cast missing for direction (" + dx + "," + dy
                    + "), large body in range=" + largeBodyInRange + ", actor round=" + actor.SkillState.Round
                    + ", configured cooldown=" + slot.Cooldown + ", verified range=" + skill.RangeVerified
                    + ", position=" + actor.X + "/" + actor.Y + ", targets="
                    + string.Join(",", selectable.Select(value => value.Id)) + ".");
            }
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 10940).ToArray();
            int expected = largeBodyInRange ? large.Id : small.Id;
            if (hits.Length != 1 || hits[0].TargetId != expected)
                throw new InvalidOperationException("Star-shattering slash: expected one hit on " + expected
                    + ", got [" + string.Join(",", hits.Select(hit => hit.TargetId)) + "].");
            var hit = hits[0];
            if (hit.HitEntityId != 10941 || hit.DamageEntityId != 10015 || hit.ElementType != 0
                || hit.DamageType != 1 || hit.TrueDamage || hit.IsCure || hit.Damage >= 0
                || hit.AfterHp - hit.BeforeHp != hit.Damage
                || Math.Abs(hit.Delay - cast.Delay - 1.35f) >= 0.0001f)
                throw new InvalidOperationException("Star-shattering slash: physical damage or hit timing differs.");
            if (FightBattleCheckpointSerializer.Save(checkpoint) != FightBattleCheckpointSerializer.Save(copy))
                throw new InvalidOperationException("Star-shattering slash: loading changed target selection or settlement.");
            cases++;
        }
        return "Star-shattering slash passed: " + cases + " body-range priority, physical hit and resume cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 3,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 100000, MaxHp = 100000,
            LastMaxHp = 100000, MoveMask = 5
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
