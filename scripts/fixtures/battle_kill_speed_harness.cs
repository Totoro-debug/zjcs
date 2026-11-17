using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleKillSpeedHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Kill speed: " + message);
    }

    private static FightBattleUnitState Unit(int id, int side, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1,
            Level = 50, SubRank = 1, X = 4 + id, Y = 4, Hp = hp, MaxHp = 1000, LastMaxHp = 1000,
            Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = id == 1 ? 1000 : 10 } };
        foreach (var entry in new Dictionary<int, long> { { 1, 100 }, { 2, 100 }, { 3, 1000 }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var actor = Unit(1, 1, 1000, formula);
        var enemy = Unit(2, 2, 1, formula);
        var properties = actor.Properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList();
        var passive = catalog.CreateStatus(25028, 1, 1, 1, 25028, 10546, 2, 20, 1, properties);
        Check(passive.HasKillSkill && passive.KillSkillRulesSupported && passive.KillSkillStatusEntityId == 24220
            && passive.KillSkillRate == 1 && passive.KillSkillMaxCount == -1, "configured kill trigger differs");
        actor.Statuses.Add(passive);
        var skill = new FightBattleSkillDefinition { EntityId = 990701, Slot = 0, TargetType = 1, ActionType = 2 };
        skill.Range.Add(new FightGridPoint(1, 0));
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990702, ActionType = 4,
            DamageType = 8, HasCustomDamage = true, CustomDamage = 100 });
        actor.Skills.Add(skill);
        actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 12, Height = 12 } };
        cp.Units.AddRange(new[] { actor, enemy });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
        var step = runner.Step(0, 1).Event; restored.Step(0, 1);
        Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved), "checkpoint continuation differs");
        Check(step.DamageEvents.Any(x => x.SkillEntityId == 990701 && x.TargetId == 2 && x.AfterHp == 0), "fixture did not defeat the target");
        var buff = actor.Statuses.SingleOrDefault(x => x.ClassId == 24220);
        Check(buff != null && buff.StackCount == 1 && buff.ApplyingSkillItemId == 10546
            && buff.CreatorId == 1 && buff.SourceSkillRank == 2 && buff.SourceSkillLevel == 20,
            "actual kill did not apply a sourced speed stack");
        Check(buff.MaxStackedCount == 4 && buff.CanDispersed, "configured stack cap or dispel flag differs");
        for (int i = 0; i < 5; i++)
        {
            var defeated = Unit(3 + i, 2, 0, formula);
            Check(FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, skill, cp.Units,
                catalog, new FightRandomStream(200 + i)) == 1, "subsequent kill did not invoke the passive");
            buff = actor.Statuses.Single(x => x.ClassId == 24220);
            Check(buff.StackCount == Math.Min(4, i + 2), "speed stacks exceed the configured maximum");
        }
        FightStatusPropertyRuntime.RebuildAll(cp.Units, catalog, formula);
        Check(actor.Properties.Any(p => p.Id == formula.PropertyId("Speed") && p.Value > 1000),
            "stacked original effect did not increase the owner's speed");
        return "Kill speed passed: actual defeat, source, four-stack cap, speed and checkpoint.";
    }
}
