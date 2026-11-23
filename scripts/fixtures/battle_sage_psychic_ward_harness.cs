using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSagePsychicWardHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (int rank in new[] { 1, 2, 4 }) Verify(catalog, formula, items, rank, 2);
        return "Psychic ward passed: five-cell lowest HP percentage, one-use cultivated reduction, 4% boundary, status end and replay.";
    }

    private static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level)
    {
        var caster = Unit(1, 1, 4, 1100, 20000, 20000, formula);
        var near = Unit(2, 1, 5, 900, 4900, 5000, formula);
        var receiver = Unit(3, 1, 9, 800, 8000, 10000, formula);
        var outside = Unit(4, 1, 10, 700, 1, 10000, formula);
        var attacker = Unit(5, 2, 10, 600, 10000, 10000, formula);
        attacker.Y = 4;
        var properties = catalog.CalculateSkillProps(11760, rank, level, caster.SubRank);
        var skill = catalog.CreateSkill(11760, 0, 10077, rank, level, caster.SubRank, properties.Select(value =>
            new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.ResetCooldownAtStart = true;
        caster.Skills.Add(skill);
        caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 723004,
            Grid = new FightBattleGrid { Width = 14, Height = 12, DefaultGroundType = 2 } };
        checkpoint.Units.AddRange(new[] { caster, near, receiver, outside, attacker });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(attacker.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var mark = receiver.Statuses.SingleOrDefault(status => status.ClassId == 24181);
        Check(mark != null && near.Statuses.Count == 0 && outside.Statuses.Count == 0,
            "the original ward did not select the eligible ally with the lowest HP percentage");
        var start = step.StatusViewEvents.SingleOrDefault(view => view.Status.ClassId == 24181
            && view.Kind == FightBattleStatusViewEventKind.Start);
        Check(start != null && start.OwnerId == receiver.Id && Math.Abs(start.Delay - .4f) < .0001f
            && mark.CreatorId == caster.Id && mark.ApplyingSkillItemId == 10077 && mark.SourceSkillRank == rank
            && mark.SourceSkillLevel == level && mark.DurationRound == -1 && mark.CanDispersed
            && mark.ShieldKind == FightBattleShieldKind.None && receiver.Hp == 8000,
            "the ward changed source, timing, duration, dispel behavior or HP");

        var stored = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(stored);
        var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(replay.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == stored, "the persisted ward was reapplied on replay");

        long property = catalog.CalculateSkillProps(24181, rank, level, caster.SubRank)[232];
        double scale = formula.ReadProperty(new System.Collections.Generic.Dictionary<int, long> { [232] = property }, 232);
        Check(scale > 0 && mark.StatusProperties.Single(value => value.Id == 232).Value == property,
            "the status lost its own cultivated reduction property");
        VerifyConsumption(attacker, receiver, formula, scale);
        VerifyConsumption(restored.Units.Single(unit => unit.Id == attacker.Id),
            restored.Units.Single(unit => unit.Id == receiver.Id), formula, scale);
    }

    private static void VerifyConsumption(FightBattleUnitState attacker, FightBattleUnitState receiver,
        FightDamageFormula formula, double scale)
    {
        var executor = new FightBattleHitExecutor(formula, new FightDamageSettlement());
        int ended = 0;
        receiver.StatusEnded = (unit, status) => { if (status.ClassId == 24181) ended++; };
        var hit = new FightBattleHitDefinition { ActionType = 4, DamageType = 1,
            BasicProperty = 1, DamageProperty = 200, FixedDamageProperty = 190, HasCustomDamage = true };
        var skill = new FightBattleSkillDefinition { ActionType = 2 };
        int damageId = 0;
        long Apply(long amount)
        {
            hit.CustomDamage = amount;
            return executor.Apply(new FightRandomStream(723004), ++damageId, attacker, receiver, skill,
                hit, 0, false).Formula.Damage;
        }
        Check(Apply(399) == -399 && Apply(400) == -400 && ended == 0
            && receiver.Statuses.Any(status => status.ClassId == 24181), "a hit at or below 4% consumed the ward");
        Check(Apply(401) == -(long)(401 / (1 + scale)) && ended == 1
            && receiver.Statuses.All(status => status.ClassId != 24181),
            "the first hit above 4% did not use the cultivated reduction and end the mark once");
        Check(Apply(1200) == -1200 && ended == 1, "the already consumed ward reduced another hit");
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int speed, long hp, long max,
        FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 17,
            Level = 70, SubRank = 8, X = x, Y = 5, MoveMask = 5, Hp = hp, MaxHp = max, LastMaxHp = max };
        unit.Turn.Id = id;
        unit.Turn.Hp = hp;
        unit.Turn.Speed = speed;
        foreach (var prop in new (string Name, long Value)[] { ("MaxHp", max), ("Attack", 1000), ("Speed", speed),
            ("Defence", 0), ("BaseCritRatePercentValue", 10000), ("BaseCritAvoidPercentValue", 10000) })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(prop.Name), Value = prop.Value });
        return unit;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Psychic ward: " + message);
    }
}
