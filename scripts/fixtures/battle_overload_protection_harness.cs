using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleOverloadProtectionHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        Check(catalog.ReportsAccumulatedHpTriggers(25043), "the current official trigger-count flag was lost");
        int cases = 0;
        foreach (int rank in new[] { 3, 4, 5 })
        foreach (int level in new[] { 1, 2, 10 })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 55,
                Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
            var owner = new FightBattleUnitState { Id = 1, Side = 1, Hp = 20000, MaxHp = 20000, LastMaxHp = 20000 };
            owner.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = owner.MaxHp });
            owner.Turn.Id = owner.Id;
            owner.Turn.Hp = owner.Hp;
            checkpoint.Units.Add(owner);
            var listener = catalog.CreateStatus(25043, 1, 1, 725043, 25043, 10573, rank, level, 8);
            owner.Statuses.Add(listener);
            var random = new FightRandomStream(55);
            var stats = checkpoint.Statistics;

            Check(FightStatusTriggerRuntime.ApplyAccumulatedDamage(owner, 2999, catalog, random, statistics: stats) == 0
                && owner.Statuses.All(status => status.ClassId != 24240), "a sub-threshold loss granted a shield");
            Check(FightStatusTriggerRuntime.ApplyAccumulatedDamage(owner, 0, catalog, random, statistics: stats) == 0
                && listener.HpAccDamageAccumulated == 2999, "zero HP loss changed the accumulator");
            Check(FightStatusTriggerRuntime.ApplyAccumulatedDamage(owner, 1, catalog, random, statistics: stats) == 1,
                "the fifteen-percent threshold did not invoke the shield");
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, catalog, formula);
            stats.RecordPendingShieldGrants(checkpoint.Units);
            var shield = owner.Statuses.Single(status => status.ClassId == 24240);
            var properties = catalog.CalculateSkillProps(24240, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value);
            long expected = (long)(owner.MaxHp * formula.ReadProperty(properties, formula.PropertyId("ShieldByTargetHp")))
                + (long)formula.ReadProperty(properties, formula.PropertyId("SkillFixedShield"));
            Check(expected > 0 && shield.ShieldRemainValue == expected && shield.DurationRound == 2
                && shield.CreatorId == owner.Id && shield.SourceSkillRank == rank && shield.SourceSkillLevel == level
                && !shield.CanDispersed, $"the cultivated maximum-HP shield lost its value or origin (expected {expected}, remaining {shield.ShieldRemainValue}, duration {shield.DurationRound}, source {shield.CreatorId}, rank {shield.SourceSkillRank}, level {shield.SourceSkillLevel})");

            long remaining = Math.Max(1, expected / 2);
            shield.ShieldRemainValue = remaining;
            Check(FightStatusTriggerRuntime.ApplyAccumulatedDamage(owner, 6500, catalog, random, statistics: stats) == 2
                && listener.HpAccDamageAccumulated == 500 && owner.Statuses.Count(status => status.ClassId == 24240) == 1
                && shield.ShieldRemainValue == remaining, "multiple thresholds did not preserve the confirmed shield refresh rule");
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, catalog, formula);
            stats.RecordPendingShieldGrants(checkpoint.Units);
            for (int cast = 0; cast < 4; cast++) stats.RecordCast(owner.Id, 25043, 10573);
            var report = FightBattleReportBuilder.Build(checkpoint).Skills.Single(skill => skill.SkillItemId == 10573);
            Check(report.Casts == 3 && report.ShieldGranted == expected,
                "the configured trigger count did not replace inferred casts or counted a refresh as a new shield");

            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var restoredOwner = restored.Units.Single();
            var restoredListener = restoredOwner.Statuses.Single(status => status.ClassId == 25043);
            Check(restoredListener.HpAccDamageAccumulated == 500 && restored.Statistics.Skills.Single().StatusTriggerCount == 3,
                "the pending HP loss or battle-report trigger count did not survive restore");
            FightStatusLifecycle.TickTurn(restoredOwner, restored.Units, 1, FightStatusLifecycle.TimingEnd);
            Check(restoredListener.HpAccDamageAccumulated == 0, "the personal round end did not clear the accumulated loss");
            cases++;
        }
        VerifyActualHit(catalog, formula);
        return $"Overload protection passed: {cases} cultivation cases, actual HP loss and shield start, threshold, refresh, round reset, report count and restore.";
    }

    private static void VerifyActualHit(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 55,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var protectedUnit = Fighter(1, 1, 5, 100, 1000, formula);
        var attacker = Fighter(2, 2, 6, 1000, 8000, formula);
        protectedUnit.Statuses.Add(catalog.CreateStatus(25043, 1, 1, 725043, 25043, 10573, 4, 2, 8));
        var skill = catalog.CreateSkill(10600, 0, 10008, 1, 1, 1,
            catalog.CalculateSkillProps(10600, 1, 1, 1).Select(pair =>
                new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        skill.ResetCooldownAtStart = true;
        attacker.Skills.Add(skill);
        attacker.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        checkpoint.Units.AddRange(new[] { protectedUnit, attacker });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(attacker.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog);
        var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var hit = step.DamageEvents.Single(value => value.TargetId == protectedUnit.Id);
        Check(hit.BeforeHp - hit.AfterHp >= 3000 && hit.AfterHp > 0, "the original hit did not cross the HP-loss threshold");
        var shield = protectedUnit.Statuses.Single(status => status.ClassId == 24240);
        var start = step.StatusViewEvents.Single(value => value.Status.ClassId == 24240
            && value.Kind == FightBattleStatusViewEventKind.Start);
        Check(start.Status.CreatorId == protectedUnit.Id && shield.ShieldValue > 0
            && start.Status.ShieldMaximum == shield.ShieldValue,
            $"the shield start published {start.Status.ShieldMaximum} before its cultivated value {shield.ShieldValue}");
        var report = FightBattleReportBuilder.Build(checkpoint).Skills.Single(value => value.SkillItemId == 10573);
        Check(report.ShieldGranted == shield.ShieldValue && report.Casts > 0,
            "an actual settled hit did not record the shield and trigger count");
    }

    private static FightBattleUnitState Fighter(int id, int side, int x, int speed, int attack, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = 5, CharacterType = 1,
            Level = 70, SubRank = 8, Hp = 20000, MaxHp = 20000, LastMaxHp = 20000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = speed;
        foreach (var value in new[] { ("MaxHp", 20000), ("Speed", speed), ("Attack", attack), ("Defence", 10),
            ("BaseBlockPercentValue", 10000), ("BaseBlockAvoidPercentValue", 10000), ("BaseCritRatePercentValue", 10000),
            ("BaseCritAvoidPercentValue", 10000), ("CritAvoidPercent", 10000) })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(value.Item1), Value = value.Item2 });
        return unit;
    }

    private static void Check(bool condition, string details)
    {
        if (!condition) throw new InvalidOperationException("Overload protection: " + details);
    }
}
