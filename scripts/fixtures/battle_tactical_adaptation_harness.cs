using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleTacticalAdaptationHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Tactical adaptation: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        Check(items.Required(10609).PassiveStatusIds.SequenceEqual(new[] { 25118 }), "the original passive binding changed");
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int count in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 19,
                Grid = new FightBattleGrid { Width = 15, Height = 15, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 6, 6, formula); state.Units.Add(actor);
            var listener = catalog.CreateStatus(25118, 1, 1, 725118, 25118, 10609, rank, level, 8);
            Check(listener.MoveRangeRulesSupported && listener.MoveRangeHandleType == 2
                && listener.MoveRangeTargetCount == 3 && listener.MoveRangeOffsets.Count == 13,
                "the original threshold, radius or owner-status handler changed");
            actor.Statuses.Add(listener);
            var offsets = new[] { new FightGridPoint(0, 1), new FightGridPoint(1, 0),
                new FightGridPoint(0, -1), new FightGridPoint(-1, 0), new FightGridPoint(2, 0) };
            for (int i = 0; i < count; i++)
            {
                var enemy = Unit(i + 2, 2, 6 + offsets[i].X, 6 + offsets[i].Y, formula);
                if (i == 0) enemy.BodyOffsets.Add(new FightGridPoint(0, 1));
                state.Units.Add(enemy);
            }
            state.Units.Add(Unit(20, 1, 7, 7, formula));
            var dead = Unit(21, 2, 5, 5, formula); dead.Hp = dead.Turn.Hp = 0; state.Units.Add(dead);
            var ended = Unit(22, 2, 5, 6, formula); ended.Ended = true; state.Units.Add(ended);
            var excluded = Unit(23, 2, 5, 7, formula); excluded.CharacterType = 6; state.Units.Add(excluded);
            state.Units.Add(Unit(24, 2, 3, 6, formula));
            int expected = count <= 3 ? 24394 : 24393;
            FightStatusTriggerRuntime.ApplyMoveRange(actor, state.Units, catalog, new FightRandomStream(19), false);
            Verify(actor, listener, count, expected, rank, level, state.Units, catalog, formula);
            int serial = actor.Statuses.Single(value => value.ClassId == expected).InstanceId;
            FightStatusTriggerRuntime.ApplyMoveRange(actor, state.Units, catalog, new FightRandomStream(20), true);
            Check(actor.Statuses.Single(value => value.ClassId == expected).InstanceId == serial,
                "unchanged enemy counts recreated their permanent status");
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            FightStatusPropertyRuntime.RebuildAll(copy.Units, catalog, formula);
            var restored = copy.Units.Single(value => value.Id == 1);
            Verify(restored, restored.Statuses.Single(value => value.ClassId == 25118), count, expected,
                rank, level, copy.Units, catalog, formula);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "restoration changed the threshold state or cultivated properties");

            // The outside unit enters through a body cell while its centre remains outside.
            var entering = state.Units.Single(value => value.Id == 24);
            entering.BodyOffsets.Add(new FightGridPoint(1, 0));
            FightStatusTriggerRuntime.ApplyMoveRange(actor, state.Units, catalog, new FightRandomStream(21), false);
            Verify(actor, listener, count + 1, count + 1 <= 3 ? 24394 : 24393,
                rank, level, state.Units, catalog, formula);
            entering.BodyOffsets.RemoveAt(1);
            FightStatusTriggerRuntime.ApplyMoveRange(actor, state.Units, catalog, new FightRandomStream(22), false);
            Verify(actor, listener, count, expected, rank, level, state.Units, catalog, formula);

            FightStatusLifecycle.SetForbidden(actor, listener, FightStatusLifecycle.ForbiddenMutex, true);
            FightStatusTriggerRuntime.ApplyMoveRange(actor, state.Units, catalog, new FightRandomStream(23), false);
            Check(!actor.Statuses.Any(value => value.ClassId == 24393 || value.ClassId == 24394),
                "a forbidden listener retained its owner-status effect");
            FightStatusLifecycle.SetForbidden(actor, listener, FightStatusLifecycle.ForbiddenMutex, false);
            FightStatusTriggerRuntime.ApplyMoveRange(actor, state.Units, catalog, new FightRandomStream(24), false);
            Verify(actor, listener, count, expected, rank, level, state.Units, catalog, formula);
            cases++;
        }
        int damageCases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int count in new[] { 3, 4 })
        {
            string originalState, restoredState;
            long original = Damage(catalog, formula, items, rank, level, count, false, out originalState);
            long restored = Damage(catalog, formula, items, rank, level, count, true, out restoredState);
            var props = catalog.CalculateSkillProps(count == 3 ? 24394 : 24393, rank, level, 8);
            long fixedValue = props[formula.PropertyId(count == 3 ? "FixedStatusDmgAdd" : "FixedStatusDmgReduce")];
            long percent = props[formula.PropertyId(count == 3 ? "StatusDmgAddPer" : "StatusDmgReducePer")];
            double value = 1000 * (1000d / 1010d);
            long expected = -(long)(count == 3 ? (value + fixedValue) * (1 + percent / 10000d)
                : (value - fixedValue) / (1 + percent / 10000d));
            Check(original == expected && restored == expected && originalState == restoredState,
                "the selected cultivated fixed/percent effect changed actual damage or restoration");
            damageCases++;
        }
        return "Tactical adaptation passed: " + cases + " cultivated enemy counts, three/four threshold, body cells, exclusions, exclusive status changes, forbidden recovery and save restoration cases; "
            + damageCases + " actual fixed/percent damage and restoration cases.";
    }

    private static void Verify(FightBattleUnitState actor, FightBattleStatusState listener, int count, int expected,
        int rank, int level, IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        Check(listener.MoveRangeLastTargetCount == count, "enemy entities were miscounted: " + listener.MoveRangeLastTargetCount + "/" + count);
        var applied = actor.Statuses.Where(value => value.ClassId == 24393 || value.ClassId == 24394).ToArray();
        Check(applied.Length == 1 && applied[0].ClassId == expected && applied[0].CreatorId == 1 && applied[0].OwnerId == 1
            && applied[0].ApplyingSkillItemId == 10609 && applied[0].SourceSkillRank == rank && applied[0].SourceSkillLevel == level
            && applied[0].SourceSubRank == 8 && applied[0].DurationRound == -1 && !applied[0].CanDispersed,
            "the two original owner statuses overlap or lost source/cultivation");
        FightStatusPropertyRuntime.RebuildAll(units, catalog, formula);
        var props = catalog.CalculateSkillProps(expected, rank, level, 8);
        foreach (var pair in props)
            Check(actor.Properties.Any(value => value.Id == pair.Key && value.Value == pair.Value),
                "the selected cultivated effect was not applied to battle properties");
        int other = expected == 24394 ? 24393 : 24394;
        foreach (var pair in catalog.CalculateSkillProps(other, rank, level, 8))
            Check(!actor.Properties.Any(value => value.Id == pair.Key && value.Value != 0),
                "the opposite threshold effect remained in battle properties");
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 100;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = 100 });
        return unit;
    }

    private static long Damage(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int count, bool restore, out string finalState)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var actor = Unit(1, 1, 5, 5, formula);
        actor.Profession = 8;
        actor.Turn.Speed = 1000;
        actor.Properties.Single(value => value.Id == formula.PropertyId("Speed")).Value = 1000;
        actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 });
        actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 });
        actor.Statuses.Add(catalog.CreateStatus(25118, 1, 1, 725118, 25118, 10609, rank, level, 8));
        var skill = new FightBattleSkillDefinition { EntityId = 990609, Slot = 0, TargetType = 0,
            SkillType = 0, ActionType = 2, ActionDuration = 1, Duration = 1 };
        skill.Range.Add(new FightGridPoint(0, 0));
        skill.Properties.Add(new FightPropertyValue { Id = 200, Value = 10000 });
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990619, DamageId = 10005,
            DamageType = 1, BasicProperty = 1, DamageProperty = 200, FixedDamageProperty = 190, ActionType = 4, Delay = .1f });
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        state.Units.Add(actor);
        var offsets = new[] { new FightGridPoint(1, 0), new FightGridPoint(-1, 0), new FightGridPoint(0, 1), new FightGridPoint(0, -1) };
        var wave = new FightBattleWaveState();
        for (int i = 0; i < count; i++)
        {
            state.Units.Add(Unit(i + 2, 2, 5 + offsets[i].X, 5 + offsets[i].Y, formula));
            wave.UnitIds.Add(i + 2);
        }
        state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        if (restore)
        {
            state = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            executor = FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
        }
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        Check(step.ActorId == 1 && step.DamageEvents.Count == 1 && step.DamageEvents[0].TargetId == 1,
            "the actual controlled damage was not executed");
        finalState = FightBattleCheckpointSerializer.Save(state);
        return step.DamageEvents[0].Damage;
    }
}
