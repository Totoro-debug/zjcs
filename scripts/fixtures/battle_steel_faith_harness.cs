using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSteelFaithHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Steel faith: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (bool taunted in new[] { false, true })
        foreach (bool shielded in new[] { false, true })
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        {
            if (taunted)
            {
                VerifyTriggeredHit(catalog, formula, rank, level, shielded, direction);
                cases++;
                continue;
            }
            var target = Unit(1, 6, 6, formula);
            var attacker = Unit(2, 6 + direction[0], 6 + direction[1], formula);
            var steel = catalog.CreateStatus(25108, 1, 1, 725108, 25108, 10592, rank, level, 8);
            target.Statuses.Add(steel);
            Check(items.Required(10592).PassiveStatusIds.SequenceEqual(new[] { 25108 })
                && steel.DamageProcess != null && steel.DamageProcess.Supported,
                "the equipped secret lost its configured damage-process status");
            if (taunted)
            {
                var taunt = catalog.CreateStatus(24216, 2, 1, 724216);
                taunt.DurationRound = -1;
                attacker.Statuses.Add(taunt);
            }
            if (shielded) target.Statuses.Add(catalog.CreateStatus(24377, 1, 1, 724377, customShieldValue: 100));
            var slash = catalog.CreateSkill(10600, 0, 10008, 2, 2, 8,
                catalog.CalculateSkillProps(10600, 2, 2, 8)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            slash.LimitedTimes = 1;
            attacker.Skills.Add(slash); attacker.SkillState.Slots.Add(catalog.CreateSkillSlot(slash, 0));
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            state.Units.AddRange(new[] { target, attacker });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(attacker.Id); state.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            string initial = FightBattleCheckpointSerializer.Save(state);
            var copy = FightBattleCheckpointSerializer.Load(initial);
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var control = FightBattleCheckpointSerializer.Load(initial);
            control.Units.Single(unit => unit.Id == 1).Statuses.RemoveAll(status => status.ClassId == 25108);
            var plain = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            FightBattleDamageEvent hit = null, original = null;
            while (hit == null && !state.Finished && state.Counter < 8)
            {
                var step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                var baseline = plain.Step(control.Sequence, control.Counter + 1).Event;
                Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                    "restoration changed taunt, damage reduction or shield absorption");
                hit = step.DamageEvents.SingleOrDefault(value => value.SourceId == 2 && value.TargetId == 1);
                original = baseline.DamageEvents.SingleOrDefault(value => value.SourceId == 2 && value.TargetId == 1);
            }
            Check(hit != null && original != null && original.Damage - original.ShieldDamage < 0,
                "the original slash did not damage HP or shield: rank=" + rank + ", level=" + level
                + ", taunt=" + taunted + ", shield=" + shielded);
            var raw = catalog.CalculateSkillProps(25108, rank, level, 8).ToDictionary(p => p.Key, p => p.Value);
            double reduce = formula.ReadProperty(raw, formula.PropertyId("StatusDmgReducePer"));
            Check(reduce > 0, "the original cultivation data contains no reduction");
            long baseAmount = -original.Damage + original.ShieldDamage;
            double fixedReduce = formula.ReadProperty(raw, formula.PropertyId("FixedStatusDmgReduce"));
            long expected = taunted ? (long)(Math.Max(0, baseAmount - fixedReduce) / (1.0 + reduce)) : baseAmount;
            Check(-hit.Damage + hit.ShieldDamage == expected
                && hit.ShieldDamage == (shielded ? Math.Min(100, expected) : 0)
                && hit.BeforeHp - hit.AfterHp == Math.Max(0, expected - hit.ShieldDamage),
                "taunt-dependent reduction was not applied before shield and HP settlement");
            Check(FightStatusDamageProcessRuntime.BeforeShield(attacker, target, 1000, formula, 2) == 1000,
                "a damage-only secret reduced healing");
            attacker.Side = target.Side;
            Check(FightStatusDamageProcessRuntime.BeforeShield(attacker, target, -1000, formula, 1) == -1000,
                "the enemy-only condition reduced allied damage");
            attacker.Side = 2; attacker.Statuses.RemoveAll(status => status.ActionType == 58);
            Check(FightStatusDamageProcessRuntime.BeforeShield(attacker, target, -1000, formula, 1) == -1000,
                "the secret still reduced damage after taunt ended");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(state) == saved, "replay settled the same hit twice");
            cases++;
        }
        return "Steel faith passed: " + cases + " cultivation, taunt, shield, healing, ally, expiry and restore cases.";
    }

    private static void VerifyTriggeredHit(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        int rank, int level, bool shielded, int[] direction)
    {
        // Taunt prevents selecting the original slash as a new active skill.
        // Exercise an already triggered damage settlement through its shared
        // production hit executor, preserving that original selection rule.
        var target = Unit(1, 6, 6, formula);
        var source = Unit(2, 6 + direction[0], 6 + direction[1], formula);
        target.Statuses.Add(catalog.CreateStatus(25108, 1, 1, 725108, 25108, 10592, rank, level, 8));
        source.Statuses.Add(catalog.CreateStatus(24216, 2, 1, 724216));
        if (shielded) target.Statuses.Add(catalog.CreateStatus(24377, 1, 1, 724377, customShieldValue: 100));
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
        state.Units.AddRange(new[] { target, source });
        FightStatusPropertyRuntime.RebuildAll(state.Units, catalog, formula);
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var skill = new FightBattleSkillDefinition { EntityId = 990592, ActionType = 2, CanTriggerChild = true };
        var hit = new FightBattleHitDefinition { EntityId = 990593, DamageType = 8, ActionType = 1,
            HasCustomDamage = true, CustomDamage = 1000 };
        var resolver = new FightBattleHitExecutor(formula, new FightDamageSettlement());
        var result = resolver.Apply(new FightRandomStream(91), 1, source, target, skill, hit, 1, false);
        resolver.Apply(new FightRandomStream(91), 1, copy.Units[1], copy.Units[0], skill, hit, 1, false);
        var raw = catalog.CalculateSkillProps(25108, rank, level, 8).ToDictionary(p => p.Key, p => p.Value);
        double fixedReduce = formula.ReadProperty(raw, formula.PropertyId("FixedStatusDmgReduce"));
        long expected = (long)(Math.Max(0, 1000 - fixedReduce) / (1.0 + formula.ReadProperty(raw, formula.PropertyId("StatusDmgReducePer"))));
        Check(expected > 0 && expected < 1000
            && -result.Settlement.RealDamageValue + result.ShieldDamage == expected
            && result.ShieldDamage == (shielded ? Math.Min(100, expected) : 0),
            "triggered damage from a taunted enemy did not reduce before shield absorption");
        Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
            "restoration changed the taunt-dependent triggered hit");
        Check(FightStatusDamageProcessRuntime.BeforeShield(source, target, -1, formula, 1) == 0,
            "fixed reduction turned an absorbed hit into healing or retained damage");
        Check(FightStatusDamageProcessRuntime.BeforeShield(source, target, 1000, formula, 2) == 1000,
            "taunt-dependent damage reduction also changed healing");
        source.Side = target.Side;
        Check(FightStatusDamageProcessRuntime.BeforeShield(source, target, -1000, formula, 1) == -1000,
            "taunt-dependent damage reduction affected an ally");
        source.Side = 2; source.Statuses.Clear();
        Check(FightStatusDamageProcessRuntime.BeforeShield(source, target, -1000, formula, 1) == -1000,
            "taunt-dependent damage reduction persisted after taunt ended");
    }

    private static FightBattleUnitState Unit(int id, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 4,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 2 ? 1000 : 10;
        foreach (var pair in new Dictionary<string, long> {
            { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", unit.MaxHp }, { "Speed", unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
