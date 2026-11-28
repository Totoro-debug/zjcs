using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleDoomLinkHarness
{
    static void Need(bool value, string message)
    { if (!value) throw new InvalidOperationException("Doom link: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        Need(catalog.TryReadTargetStatusReaction(25073, 24474, out int status, out float rate, out int limit)
            && status == 24472 && Math.Abs(rate - .6f) < .000001f && limit == 1,
            "the original erosion-tag filter, 60% rate or per-trigger limit changed");
        Need(catalog.TryReadTargetStatusReaction(25073, 24472, out status, out rate, out limit)
            && !catalog.TryReadTargetStatusReaction(25073, 24040, out status, out rate, out limit),
            "the additional erosion must match the same tag filter; weakness must not");
        int seed = 104729;
        while (!Verify(catalog, formula, items, 3, 2, false, false, seed, false, false, false))
        { seed++; Need(seed < 104761, "no successful original 60% roll was observed"); }
        int cases = 0;
        foreach (int rank in new[] { 1, 3, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (bool large in new[] { false, true })
        foreach (bool pvp in new[] { false, true })
        {
            Need(Verify(catalog, formula, items, rank, level, large, pvp, seed, false, false, true),
                "the verified successful seed changed before the first erosion reaction");
            cases++;
        }
        int successes = 0;
        for (int sample = 1; sample <= 32; sample++)
            if (Verify(catalog, formula, items, 3, 2, false, false, sample, false, false, false)) successes++;
        Need(successes > 0 && successes < 32, "the original probability became always-on or always-off");
        Need(!Verify(catalog, formula, items, 3, 2, true, false, seed, true, false, false)
            && !Verify(catalog, formula, items, 3, 2, true, false, seed, false, true, false),
            "a forbidden listener or resisted primary status applied additional erosion");
        return "Doom link passed: " + cases + " PVE/PVP cultivated erosion cases, per-application recursion limit, body refresh, merged ticks, ownership and restore; "
            + successes + "/32 original probability rolls.";
    }

    static bool Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, bool large, bool pvp,
        int seed, bool forbidden, bool resist, bool followTicks)
    {
        var actor = Unit(1, 1, 7, 7, formula);
        var target = Unit(2, 2, 7, 10, formula);
        if (large) target.BodyOffsets.Add(new FightGridPoint(1, 0));
        actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = resist ? -10000 : 10000 });
        catalog.AssembleUnit(actor, null, catalog.CreatePassiveStatusBindings(actor, items, new[] {
            new FightOfficialPassiveSkillBinding { ItemId = 10585, Rank = rank, Level = level, SubRank = 5 }
        }));
        var listener = actor.Statuses.Single(value => value.ClassId == 25073);
        if (forbidden) listener.ForbiddenType = 1;
        var active = catalog.CreateSkill(11390, 0, 10064, 3, 2, 5,
            catalog.CalculateSkillProps(11390, 3, 2, 5).Select(pair =>
                new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        active.ResetCooldownAtStart = true; active.LimitedTimes = 1;
        actor.Skills.Add(active); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(active, 0));
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 30, IsPvp = pvp, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        state.Units.AddRange(new[] { actor, target });
        var clone = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var battle = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var resumed = FightBattleExecutor.Begin(clone, formula, new FightDamageSettlement(), catalog, items);
        var step = battle.Step(0, 1).Event;
        resumed.Step(0, 1); Same(state, clone);
        Need(step.SkillCasts.Any(value => value.SkillEntityId == 11390), "the actual parent Mana Burst did not execute");
        int applications = step.StatusViewEvents.Count(view => view.Status.ClassId == 24472
            && (view.Kind == FightBattleStatusViewEventKind.Start || view.Kind == FightBattleStatusViewEventKind.Update));
        int primary = step.StatusViewEvents.Count(view => view.Status.ClassId == 24474
            && (view.Kind == FightBattleStatusViewEventKind.Start || view.Kind == FightBattleStatusViewEventKind.Update));
        Need(applications <= primary && primary <= (large ? 2 : 1),
            "additional erosion recursively exceeded the original per-application target limit");
        var extra = target.Statuses.SingleOrDefault(value => value.ClassId == 24472);
        Need((extra != null) == (applications > 0), "status receipts and the actual extra erosion disagree");
        if (extra == null) return false;
        Need(!forbidden && !resist && extra.CreatorId == actor.Id && extra.OwnerId == target.Id
            && extra.ApplyingSkillItemId == 10585 && extra.RootSkillId == 25073
            && extra.SourceSkillRank == rank && extra.SourceSkillLevel == level && extra.SourceSubRank == 5
            && extra.DurationRound == 3 && extra.RoundTarget == 2 && extra.RoundUpdateTiming == 1
            && extra.ActionType == 61 && extra.CanDispersed
            && !extra.IsOpenStack && extra.IsAddDurationRound && extra.RoundStartMaxCount == 3
            && extra.RoundStartMergeMaxCountOnStackRound && extra.CurDurationRound == applications * 3
            && FightStatusLifecycle.RoundStartRemainingCount(extra) == applications * 3,
            "the extra erosion lost its listener cultivation, source, lifetime or merged budget");
        if (!followTicks) return true;
        int ticks = 0;
        var props = actor.Properties.ToDictionary(pair => pair.Id, pair => pair.Value);
        foreach (var pair in catalog.CalculateSkillPropsOnBattle(12540, rank, level, 5, pvp)) props[pair.Key] = pair.Value;
        long expected = formula.Calculate(new FightDamageFormulaRequest { SourceProps = props,
            TargetProps = target.Properties.ToDictionary(pair => pair.Id, pair => pair.Value),
            SkillDamageBasicPropType = 1, SkillDamagePropType = 200, SkillDamageFixedPropType = 190,
            SourceType = 1, TargetType = 1, SourceLevel = 50, TargetLevel = 50,
            SourceSubRank = 5, TargetSubRank = 5, SourceElementType = 104, IsPvp = pvp },
            new FightDamageTypeData()).Damage;
        for (int index = 0; index < 40 && target.Statuses.Contains(extra); index++)
        {
            var next = battle.Step(state.Sequence, state.Counter + 1).Event;
            resumed.Step(clone.Sequence, clone.Counter + 1); Same(state, clone);
            foreach (var child in next.SkillCasts.Where(value => value.SkillEntityId == 12540))
            {
                var hit = next.DamageEvents.Single(value => child.DamageEventIds.Contains(value.DamageId));
                Need(next.ActorId == target.Id && child.ActorId == actor.Id && child.SkillItemId == 10585
                    && child.Slot < 0 && hit.TargetId == target.Id && hit.SourceId == actor.Id
                    && hit.HitEntityId == 12541 && hit.DamageEntityId == 10027 && hit.DamageType == 1
                    && hit.ElementType == 104 && !hit.TrueDamage && !hit.IsCure && hit.Damage == expected,
                    "the original target-turn child lost its cultivated ordinary dark damage");
                ticks++;
            }
        }
        Need(ticks == applications * 3 && !target.Statuses.Contains(extra), "the merged extra erosion did not exhaust its exact tick budget");
        return true;
    }

    static void Same(FightBattleCheckpoint left, FightBattleCheckpoint right) =>
        Need(FightBattleCheckpointSerializer.Save(left) == FightBattleCheckpointSerializer.Save(right),
            "cold continuation changed reactions, random state, erosion or damage");

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y, CharacterType = 1,
            Profession = 16, Level = 50, SubRank = 5, Hp = 1000000000, MaxHp = 1000000000,
            LastMaxHp = 1000000000, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 1000;
        var values = new Dictionary<string, long> { { "MaxHp", unit.MaxHp }, { "Attack", 1000 },
            { "Defence", 10 }, { "Speed", 1000 }, { "CritAvoidPercent", 10000 } };
        foreach (string name in new[] { "BaseCritRatePercentValue", "BaseCritAvoidPercentValue", "BaseBlockPercentValue",
            "BaseBlockAvoidPercentValue", "BaseElementAdd", "BaseElementReduce", "BaseElementMaster",
            "BaseElementResistance", "BaseKongFuMaster", "BaseKongFuResistance", "BaseEffectRate", "BaseEffectDodge" })
            values[name] = 10000;
        foreach (var value in values) unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(value.Key), Value = value.Value });
        return unit;
    }
}
