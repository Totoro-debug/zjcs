using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSageOverflowShieldHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (int remaining in new[] { 1, -1, -2 }) Verify(catalog, formula, items, remaining);
        return "Overflow shield passed: cultivated cure, overflow, exact cutoff, two-round shield and replay.";
    }

    private static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int remaining)
    {
        var caster = Unit(1, 1, 4, 1100, formula);
        var ally = Unit(2, 1, 6, 900, formula);
        var enemy = Unit(3, 2, 10, 600, formula);
        var recipient = ally;
        const int rank = 2, level = 2;
        var activeProps = catalog.CalculateSkillProps(12020, rank, level, caster.SubRank);
        var skill = catalog.CreateSkill(12020, 0, 10096, rank, level, caster.SubRank, activeProps.Select(value =>
            new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.ResetCooldownAtStart = true;
        skill.LimitedTimes = 1;
        caster.Skills.Add(skill);
        caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var item = items.Required(10601);
        var passiveProps = catalog.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
            item.PassiveRankFactorId, item.PassivePropFactors, rank, level, caster.SubRank);
        var source = caster.Properties.ToDictionary(value => value.Id, value => value.Value);
        foreach (var prop in activeProps) source[prop.Key] = prop.Value;
        var target = recipient.Properties.ToDictionary(value => value.Id, value => value.Value);
        var hit = skill.HitGraphEntities.Single(value => value.EntityId == 12021);
        long ordinary = formula.CalculateCure(source, target, hit.DamageProperty, hit.FixedDamageProperty).Cure;
        foreach (var prop in passiveProps)
        {
            caster.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
            source.TryGetValue(prop.Key, out long old);
            source[prop.Key] = old + prop.Value;
        }
        long offered = formula.CalculateCure(source, target, hit.DamageProperty, hit.FixedDamageProperty).Cure;
        Check(ordinary > 0 && offered > ordinary, $"the passive changed cure from {ordinary} to {offered}");
        long missing = remaining switch { -1 => offered, -2 => offered + 1, _ => remaining };
        long excess = Math.Max(0, offered - missing);
        recipient.Hp -= missing;
        recipient.Turn.Hp = recipient.Hp;
        var bindings = catalog.CreatePassiveStatusBindings(caster, items, new[] {
            new FightOfficialPassiveSkillBinding { ItemId = 10601, Rank = rank, Level = level, SubRank = caster.SubRank }
        });
        catalog.AssembleUnit(caster, null, bindings);

        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 723004,
            Grid = new FightBattleGrid { Width = 14, Height = 12, DefaultGroundType = 2 } };
        checkpoint.Units.AddRange(new[] { caster, ally, enemy });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(enemy.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var cure = step.DamageEvents.SingleOrDefault(value => value.SkillItemId == 10096);
        Check(cure != null, $"missing acquired cure for capacity {remaining}");
        Check(cure.TargetId == recipient.Id && cure.IsCure && cure.Damage == Math.Min(offered, missing)
            && cure.DamageValue == offered, $"the selected cure settled {cure.Damage}/{cure.DamageValue}, expected {missing}/{offered}");
        var shield = recipient.Statuses.SingleOrDefault(status => status.ClassId == 24174);
        var passive = caster.Statuses.Single(status => status.ClassId == 25106);
        if (excess > 0)
        {
            Check(shield != null && shield.ShieldRemainValue == excess, $"overflow {excess}, shield {shield?.ShieldRemainValue}");
            Check(shield.DurationRound == 2 && shield.SourceSkillRank == rank && shield.SourceSkillLevel == level
                && shield.ApplyingSkillItemId == 10601 && shield.CreatorId == caster.Id && !shield.CanDispersed
                && passive.Components.HitSkill.InvokeCount == 1, "the shield lost its duration, source, rank or invocation");
            var start = step.StatusViewEvents.SingleOrDefault(view => view.Kind == FightBattleStatusViewEventKind.Start
                && view.Status.ClassId == 24174);
            Check(start != null && start.OwnerId == recipient.Id && start.Status.ShieldRemaining == excess,
                "shield playback started with a different owner or amount");
        }
        else Check(shield == null && passive.Components.HitSkill.InvokeCount == 0, "a cure at or below capacity triggered the passive");
        Check(enemy.Statuses.Count == 0 && checkpoint.Units.Where(unit => unit.Id != recipient.Id)
            .All(unit => unit.Statuses.All(status => status.ClassId != 24174)), "overflow applied to another unit");
        string stored = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(stored);
        var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(replay.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == stored, "replaying the cure changed its settled shield");
        if (shield == null) return;
        int recipientTurns = 0;
        bool ended = false;
        for (int count = 0; count < 12 && !ended && !checkpoint.Finished; count++)
        {
            var next = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            replay.Step(restored.Sequence, restored.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(restored) == FightBattleCheckpointSerializer.Save(checkpoint),
                "resuming the shield changed its next state");
            if (next.ActorId == recipient.Id) recipientTurns++;
            ended = next.StatusViewEvents.Any(view => view.OwnerId == recipient.Id
                && view.Kind == FightBattleStatusViewEventKind.End && view.Status.ClassId == 24174);
        }
        Check(ended && recipientTurns == 2 && !recipient.Statuses.Contains(shield),
            $"the overflow shield ended after {recipientTurns} recipient turns (ended={ended})");
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 17,
            Level = 70, SubRank = 8, X = x, Y = 5, MoveMask = 5, Hp = 100000, MaxHp = 100000, LastMaxHp = 100000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = speed;
        foreach (var prop in new[] { ("MaxHp", (int)unit.MaxHp), ("Attack", 2400), ("Speed", speed), ("Defence", 0),
            ("BaseCritRatePercentValue", 10000), ("BaseCritAvoidPercentValue", 10000) })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(prop.Item1), Value = prop.Item2 });
        return unit;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Overflow shield: " + message);
    }
}
