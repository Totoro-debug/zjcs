using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFlameSoulHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Flame soul: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 4, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int action in new[] { 0, 2, 4, 5, 6 })
        foreach (int element in new[] { 0, 100, 102, 105 })
        foreach (bool forbidden in new[] { false, true })
        {
            var actor = Unit(1, 1, formula);
            var passive = catalog.CreateStatus(25103, 1, 1, 725103, 25103, 10599, rank, level, 8);
            actor.Statuses.Add(passive);
            if (forbidden) passive.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
            var skill = new FightBattleSkillDefinition { EntityId = 10800, ItemId = 10028,
                CanTriggerChild = true, ActionType = action, ElementType = element };
            bool expected = !forbidden && (action == 2 || action == 6) && element == 102;
            for (int count = 1; count <= 10; count++)
            {
                bool applied = FightSkillStartStatuses.Apply(actor, skill, catalog, new FightRandomStream(23));
                FightStatusPropertyRuntime.RebuildAll(new[] { actor }, catalog, formula);
                Check(applied == expected, "element and action conditions or forbidden state were ignored");
                var buff = actor.Statuses.SingleOrDefault(status => status.ClassId == 24386);
                Check((buff != null) == expected, "a non-fire action acquired the fire stack");
                if (expected)
                {
                    Check(buff.StackCount == Math.Min(count, 8) && buff.DurationRound == 2
                        && buff.CurDurationRound == 2 && buff.CanDispersed
                        && buff.ApplyingSkillItemId == 10599 && buff.SourceSkillRank == rank
                        && buff.SourceSkillLevel == level && buff.SourceSubRank == 8,
                        "stack cap, refresh, cultivation, source or dispel policy changed");
                    var values = catalog.CalculateSkillProps(24386, rank, level, 8);
                    Check(buff.StatusProperties.All(prop => values[prop.Id] == prop.Value)
                        && values[58] > 0 && values[75] > 0,
                        "the two original cultivated critical properties were lost");
                    Check(actor.Properties.Single(prop => prop.Id == 58).Value == values[58] * buff.StackCount
                        && actor.Properties.Single(prop => prop.Id == 75).Value == values[75] * buff.StackCount,
                        "the active critical bonuses did not scale with the original stack count");
                }
                // Round-trip the unit between applications; the integration below
                // restores a complete initialized battle checkpoint.
                actor = FightBattleCheckpointSerializer.CopyUnit(actor);
            }
            Check(actor.Statuses.Single(status => status.ClassId == 25103).SkillStartTriggeredCount == (expected ? 10 : 0),
                "trigger counters did not survive restoration");
            skill.CanTriggerChild = false;
            Check(!FightSkillStartStatuses.Apply(actor, skill, catalog, new FightRandomStream(23)),
                "a child-disabled skill triggered the passive");
            if (expected)
            {
                var buff = actor.Statuses.Single(status => status.ClassId == 24386);
                FightStatusLifecycle.Initialize(buff, 0);
                FightStatusLifecycle.TickTurn(actor, new[] { actor }, 1, FightStatusLifecycle.TimingEnd);
                Check(actor.Statuses.Contains(buff), "the critical stack expired after only one round");
                FightStatusLifecycle.TickTurn(actor, new[] { actor }, 2, FightStatusLifecycle.TimingEnd);
                FightStatusPropertyRuntime.RebuildAll(new[] { actor }, catalog, formula);
                Check(!actor.Statuses.Contains(buff)
                    && actor.Properties.Where(prop => prop.Id == 58 || prop.Id == 75).All(prop => prop.Value == 0),
                    "expiry left the critical stack or its properties on the actor");
            }
            cases++;
        }
        VerifyActualCast(catalog, formula, items);
        int criticalCasts = 0, controlCriticalCasts = 0;
        for (int seed = 1; seed <= 64; seed++)
        {
            if (CastIsCritical(catalog, formula, items, seed, false)) controlCriticalCasts++;
            if (CastIsCritical(catalog, formula, items, seed, true)) criticalCasts++;
        }
        // The official formula includes a base 5% chance even with zero crit properties.
        Check(criticalCasts > controlCriticalCasts,
            "the cast-time stack did not affect its own fireball critical roll: " + criticalCasts + "/" + controlCriticalCasts);
        return "Flame soul passed: " + cases + " cultivated elemental/action conditions, forbidden state, eight-stack cap, two-round refresh, child policy and restoration cases; original fireball cast integration and 64 paired same-cast critical comparisons.";
    }

    private static bool CastIsCritical(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int seed, bool passive)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        var actor = Unit(1, 1, formula); var enemy = Unit(2, 2, formula);
        actor.X = 5; actor.Y = 5; enemy.X = 5; enemy.Y = 6;
        var skill = catalog.CreateSkill(10800, 0, 10028, 4, 2, 8,
            catalog.CalculateSkillProps(10800, 4, 2, 8).Select(pair =>
                new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        if (passive) actor.Statuses.Add(catalog.CreateStatus(25103, 1, 1, 725103, 25103, 10599, 4, 2, 8));
        cp.Units.AddRange(new[] { actor, enemy });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        return executor.Step(cp.Sequence, cp.Counter + 1).Event.DamageEvents.Single(hit => hit.SkillEntityId == 10800).Critical;
    }

    private static void VerifyActualCast(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 37,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        var actor = Unit(1, 1, formula); var enemy = Unit(2, 2, formula);
        actor.X = 5; actor.Y = 5; enemy.X = 5; enemy.Y = 6;
        var skill = catalog.CreateSkill(10800, 0, 10028, 4, 2, 8,
            catalog.CalculateSkillProps(10800, 4, 2, 8).Select(pair =>
                new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        actor.Statuses.Add(catalog.CreateStatus(25103, 1, 1, 725103, 25103, 10599, 4, 2, 8));
        cp.Units.AddRange(new[] { actor, enemy });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        string original = FightBattleCheckpointSerializer.Save(cp);
        var restored = FightBattleCheckpointSerializer.Load(original);
        var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(cp.Sequence, cp.Counter + 1).Event;
        resumed.Step(restored.Sequence, restored.Counter + 1);
        Check(step.SkillCasts.Any(cast => cast.SkillEntityId == 10800)
            && step.DamageEvents.Any(hit => hit.SkillEntityId == 10800 && hit.Damage < 0)
            && actor.Statuses.Single(status => status.ClassId == 24386).StackCount == 1,
            "the actual original fireball did not apply a single cast-time critical stack");
        Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
            "resuming before the fire cast changed damage or stacks");
        string completed = FightBattleCheckpointSerializer.Save(cp);
        Check(executor.Step(cp.Sequence, cp.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(cp) == completed, "replay applied another fire stack");
    }

    private static FightBattleUnitState Unit(int id, int side, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritRatePercent", 0 }, { "CritRatePercentValue", 0 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 },
            { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
