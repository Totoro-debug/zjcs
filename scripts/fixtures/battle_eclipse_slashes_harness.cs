using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleEclipseSlashesHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int seed in new[] { 13, 104729 })
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
                StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, small, large, ally });
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(new[] { small.Id, large.Id });
            checkpoint.Waves.Add(wave);
            var skill = catalog.CreateSkill(11440, 0, 10065, rank, level, 5,
                catalog.CalculateSkillProps(11440, rank, level, 5)
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
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10065);
            }
            if (cast == null)
            {
                var slot = actor.SkillState.Slots.Single();
                var selectable = FightBattleTargeting.SelectInSkillRange(actor, checkpoint.Units, skill);
                throw new InvalidOperationException("Eclipse Slashes: cast missing for direction (" + dx + "," + dy
                    + "), large body in range=" + largeBodyInRange + ", actor round=" + actor.SkillState.Round
                    + ", configured cooldown=" + slot.Cooldown + ", verified range=" + skill.RangeVerified
                    + ", position=" + actor.X + "/" + actor.Y + ", targets="
                    + string.Join(",", selectable.Select(value => value.Id)) + ".");
            }
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11440).ToArray();
            int expected = largeBodyInRange ? large.Id : small.Id;
            if (hits.Length != 6 || hits.Any(hit => hit.TargetId != expected))
                throw new InvalidOperationException("Eclipse Slashes must hit only the body-range-priority enemy six times.");
            var props = catalog.CalculateSkillProps(11440, rank, level, 5);
            var delays = new[] { .3f, .4f, .78f, .9f, 1.04f, 1.48f };
            for (int index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];
                int percent = index == 5 ? 201 : 200, fixedPart = index == 5 ? 191 : 190;
                long damage = -(long)((1000m * props[percent] / 10000m + props[fixedPart]) * 1000m / 1010m);
                if (hit.SourceId != 1 || hit.SkillItemId != 10065
                    || hit.HitEntityId != (index == 5 ? 11442 : 11441)
                    || hit.DamageEntityId != (index == 5 ? 11443 : 10020) || hit.ElementType != 0
                    || hit.DamageType != 1 || hit.TrueDamage || hit.IsCure || hit.Damage != damage
                    || hit.Critical || hit.Block || hit.AfterHp - hit.BeforeHp != hit.Damage
                    || Math.Abs(hit.Delay - cast.Delay - delays[index]) >= .0001f)
                    throw new InvalidOperationException("Eclipse Slashes cultivated segment " + index
                        + " numeric/source/timing mismatch: " + hit.Damage + "/" + damage);
            }
            if (ally.Hp != ally.MaxHp) throw new InvalidOperationException("Eclipse Slashes damaged a friendly unit.");
            string beforeReplay = FightBattleCheckpointSerializer.Save(checkpoint);
            if (!executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                || FightBattleCheckpointSerializer.Save(checkpoint) != beforeReplay)
                throw new InvalidOperationException("Eclipse Slashes replay repeated one of its six hits.");
            if (FightBattleCheckpointSerializer.Save(checkpoint) != FightBattleCheckpointSerializer.Save(copy))
                throw new InvalidOperationException("Eclipse Slashes: loading changed target selection or settlement.");
            cases++;
        }
        return "Eclipse Slashes passed: " + cases + " cultivation, six hit timings/formulas, body-range priority, friendly exclusion and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 7,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000000, MaxHp = 1000000000,
            LastMaxHp = 1000000000, MoveMask = 5
        };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
        foreach (var pair in new System.Collections.Generic.Dictionary<string,int> {
            { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 },
            { "BaseBlockAvoidPercentValue", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
