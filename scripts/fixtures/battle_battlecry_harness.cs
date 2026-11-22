using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBattlecryHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Battlecry: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, advances = 0, unchanged = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int seed in new[] { 1, 7, 23, 37, 79, 131 })
        foreach (int effectRate in new[] { -100000, 100000 })
        {
            var cp = State(seed, formula);
            var actor = cp.Units[0];
            actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = effectRate });
            var skill = catalog.CreateSkill(12010, 0, 10092, rank, level, 8,
                catalog.CalculateSkillProps(12010, rank, level, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            actor.Skills.Add(skill);
            var slot = catalog.CreateSkillSlot(skill, 0); slot.LimitedTimes = 1;
            actor.SkillState.Slots.Add(slot);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            long[] originalNext = cp.Units.Select(unit => cp.Timeline.Entries.Single(e => e.Id == unit.Id).Next).ToArray();
            long[] intervals = cp.Units.Select(unit => unit.Turn.IntervalTicks).ToArray();
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(saved.Sequence, saved.Counter + 1);
            Check(step.ActorId == 1 && step.SkillCasts.Any(cast => cast.SkillEntityId == 12010), "initial cooldown did not allow the first action");
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved), "restoration changed the attack snapshot or advance roll");
            var props = catalog.CalculateSkillProps(24192, rank, level, 8);
            long bonus = (long)(1000m * props[2000] / 10000m + props[2010]);
            foreach (var unit in cp.Units)
            {
                bool eligible = unit.Id == 1 || unit.Id == 3;
                var buff = unit.Statuses.SingleOrDefault(status => status.ClassId == 24192);
                var advance = unit.Statuses.SingleOrDefault(status => status.ClassId == 24384);
                Check((buff != null) == eligible && (eligible || advance == null), "range, friend or official role filtering changed: " + unit.Id);
                if (!eligible) continue;
                Check(buff.ResolvedProperties.Single(value => value.Id == 1).Value == bonus,
                    "fixed plus percentage conversion did not use the same original caster snapshot: " + unit.Id);
                Check(buff.DurationRound == 2 && buff.RoundTarget == 2 && buff.RoundUpdateTiming == 1,
                    "original two-round beneficiary clock changed: " + unit.Id + ":" + buff.DurationRound + "/" + buff.RoundTarget + "/" + buff.RoundUpdateTiming);
                var entry = cp.Timeline.Entries.Single(e => e.Id == unit.Id);
                long baseline = unit.Id == 1 ? cp.Timeline.Current + unit.Turn.IntervalTicks : originalNext[cp.Units.IndexOf(unit)];
                long interval = unit.Id == 1 ? unit.Turn.IntervalTicks : intervals[cp.Units.IndexOf(unit)];
                long delta = advance == null ? 0 : (long)Math.Round((double)0.2f * interval, MidpointRounding.AwayFromZero);
                Check(entry.Next == baseline - delta, "the next action was not immediately advanced exactly 20%: " + unit.Id + ": " + entry.Next + "/" + baseline + "/" + delta);
                if (advance == null) unchanged++; else { advances++; Check(advance.Components.RoundInterval.TriggeredCount == 1, "advance did not execute on application"); }
            }
            Check(cp.Units.All(unit => unit.Hp == 1000000), "a pure buff inflicted damage or healing");
            string after = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == after, "replay advanced the timeline twice");
            for (int i = 0; i < 16 && !cp.Finished; i++)
            {
                runner.Step(cp.Sequence, cp.Counter + 1); restored.Step(saved.Sequence, saved.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved), "subsequent beneficiary turns diverged after restore");
                Check(cp.Units.SelectMany(unit => unit.Statuses).Where(status => status.ClassId == 24384)
                    .All(status => status.Components.RoundInterval.TriggeredCount == 1), "application advance repeated at round start: "
                        + string.Join(",", cp.Units.SelectMany(unit => unit.Statuses).Where(status => status.ClassId == 24384)
                            .Select(status => status.OwnerId + "/" + status.Components.RoundInterval.TriggeredCount)));
            }
            Check(cp.Units.SelectMany(unit => unit.Statuses).All(status => status.ClassId != 24192 && status.ClassId != 24384),
                "the beneficiary clocks did not expire both original statuses");
            cases++;
        }
        Check(advances > 0 && unchanged > 0, "50% advance became unconditional or never applied");
        cases += UniqueCases(catalog, formula, items);
        return "Battlecry passed: " + cases + " cultivation, fixed caster snapshot, role/range, immediate 20% advance, probability, expiry, replay and restore cases.";
    }

    static int UniqueCases(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 7 })
        foreach (bool stronger in new[] { false, true })
        {
            var cp = State(37, formula);
            var first = cp.Units[0]; var second = cp.Units.Single(unit => unit.Id == 3);
            first.Properties.Single(value => value.Id == 1).Value = stronger ? 1000 : 10000;
            second.Properties.Single(value => value.Id == 1).Value = stronger ? 10000 : 1000;
            second.Properties.Single(value => value.Id == 4).Value = 900; second.Turn.Speed = 900;
            foreach (var caster in new[] { first, second })
            {
                var skill = catalog.CreateSkill(12010, 0, 10092, rank, 2, 8,
                    catalog.CalculateSkillProps(12010, rank, 2, 8)
                        .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                // Isolate property competition from the independently verified 50% timeline roll.
                foreach (var hit in skill.Hits) hit.AppliedStatuses.RemoveAll(value => value.StatusId == 24384);
                caster.Skills.Add(skill); var slot = catalog.CreateSkillSlot(skill, 0); slot.LimitedTimes = 1;
                caster.SkillState.Slots.Add(slot);
            }
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            runner.Step(cp.Sequence, cp.Counter + 1);
            var winner = first.Statuses.Single(status => status.ClassId == 24192);
            long oldValue = winner.ResolvedProperties.Single(value => value.Id == 1).Value;
            long secondAttack = second.Properties.Single(value => value.Id == 1).Value;
            var props = catalog.CalculateSkillProps(24192, rank, 2, 8);
            long secondValue = (long)(secondAttack * (decimal)props[2000] / 10000m + props[2010]);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(saved.Sequence, saved.Counter + 1);
            Check(step.ActorId == second.Id && step.SkillCasts.Any(cast => cast.SkillEntityId == 12010), "second caster did not exercise property competition");
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved), "unique effect competition diverged after restore");
            foreach (var unit in new[] { first, second })
            {
                var buff = unit.Statuses.Single(status => status.ClassId == 24192);
                Check(buff.CreatorId == (stronger ? second.Id : first.Id)
                    && buff.ResolvedProperties.Single(value => value.Id == 1).Value == (stronger ? secondValue : oldValue),
                    "the unique attack effect stacked, retained the weaker source, or refreshed from its own new bonus");
            }
            cases++;
        }
        return cases;
    }

    static FightBattleCheckpoint State(int seed, FightDamageFormula formula)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        cp.Units.Add(Unit(1, 1, 1, 10, 10, 1000, formula));
        cp.Units.Add(Unit(2, 2, 2, 11, 10, 300, formula));
        cp.Units.Add(Unit(3, 1, 4, 10, 13, 5000, formula));
        cp.Units.Add(Unit(4, 1, 1, 10, 14, 5000, formula));
        cp.Units.Add(Unit(5, 1, 3, 11, 11, 5000, formula));
        cp.Units.Add(Unit(6, 1, 7, 9, 11, 5000, formula));
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        return cp;
    }

    static FightBattleUnitState Unit(int id, int side, int type, int x, int y, int attack, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = type, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
        foreach (var pair in new Dictionary<string, int> { { "Attack", attack }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
