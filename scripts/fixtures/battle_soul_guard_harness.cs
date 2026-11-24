using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSoulGuardHarness
{
    private sealed class ConversionOrderObserver : IFightBattleImpactGate,
        IFightBattleImpactSettlementObserver
    {
        private readonly FightBattleUnitState owner;
        public bool SawCost;
        public bool ShieldWasAbsent;

        public ConversionOrderObserver(FightBattleUnitState owner) { this.owner = owner; }
        public void AwaitImpact(FightBattlePendingImpact impact) { }
        public void ImpactSettled(FightBattlePendingImpact impact, FightBattleDamageEvent damage)
        {
            if (damage.SkillEntityId != 10160) return;
            SawCost = true;
            ShieldWasAbsent = owner.Statuses.Where(status => status.ClassId == 24239 || status.ClassId == 24489)
                .All(status => status.ShieldRemainValue == 0);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Soul guard: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var source = new FightBattleSkillDefinition { CanTriggerChild = false };
        var status = new FightBattleStatusState();
        Check(!FightSkillTriggerPolicy.AllowsStatus(source, status), "an ordinary child bypassed its trigger switch");
        source.ShieldConversionCost = true;
        Check(FightSkillTriggerPolicy.AllowsStatus(source, status), "conversion cost could not notify hurt passives");
        source.ShieldConversionCost = false;
        source.ShieldConversionReturn = true;
        Check(!FightSkillTriggerPolicy.AllowsStatus(source, status), "conversion return bypassed its trigger switch");
        foreach (int rank in new[] { 1, 2, 4 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (long hp in new long[] { 1, 2, 3, 99, 100, 101, 999, 1000 })
            CheckOpening(catalog, formula, items, hp, rank, level);
        foreach (long remaining in new long[] { 0, 1, 250, 500 })
        foreach (int outcome in new[] { 0, 1, 2 })
            CheckTerminal(catalog, formula, items, remaining, outcome);
        return "Soul guard passed: 72 cultivated opening/restore cases, HP-loss passive, and 12 victory/exhaustion/defeat/death cases.";
    }

    private static void CheckOpening(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, long hp, int rank, int level)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        var owner = Unit(1, 1, 4, hp, formula);
        var enemy = Unit(2, 2, 20, 1000, formula);
        var passive = catalog.CreateStatus(25042, 1, 1, 1, 25042, 10575, rank, level, 8, owner.Properties);
        var previousShield = catalog.CreateStatus(24180, 1, 1, 2, 0, 0, 1, 1, 8, owner.Properties, 300);
        previousShield.ShieldUsesCustomValue = true;
        owner.Statuses.Add(previousShield);
        owner.Statuses.Add(passive);
        // Exercise the shared HP-loss listener with a real official accumulated-damage secret.
        if (hp == 999)
            owner.Statuses.Add(catalog.CreateStatus(25043, 1, 1, 3, 25043, 10573, 4, 2, 8));
        state.Units.AddRange(new[] { owner, enemy });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(2);
        state.Waves.Add(wave);

        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var conversionOrder = new ConversionOrderObserver(owner);
        executor.ImpactGate = conversionOrder;
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        restored.Step(copy.Sequence, copy.Counter + 1);
        Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
            "opening changes did not survive checkpoint restore");
        long converted = hp > 1 ? hp / 2 : 0;
        Check(owner.Hp == hp - converted, "opening did not consume half the current HP");
        Check(previousShield.ShieldRemainValue == 300, "HP conversion consumed an existing shield");
        var convertedShield = owner.Statuses.Single(status => status.ClassId == 24239);
        Check(convertedShield.ShieldRemainValue == converted, "converted shield value is incorrect");
        Check(owner.Statuses.Any(status => status.ClassId == 24489), "cultivated second shield is missing");
        if (hp == 999)
        {
            var hurt = owner.Statuses.Single(status => status.ClassId == 25043);
            Check(hurt.HpAccTriggeredCount == 3 && hurt.HpAccDamageAccumulated == 49
                && owner.Statuses.Any(status => status.ClassId == 24240),
                "the conversion HP loss could not trigger the official hurt passive");
        }
        if (converted > 0)
            Check(step.DamageEvents.Any(hit => hit.SkillEntityId == 10160 && !hit.IsCure
                && hit.BeforeHp - hit.AfterHp == converted), "conversion did not emit its original HP-loss hit");
        if (converted > 0)
            Check(conversionOrder.SawCost && conversionOrder.ShieldWasAbsent,
                "the conversion shield was present before its HP cost settled");
        if (hp != 1000) return;

        convertedShield.ShieldRemainValue = 250;
        owner.BaseProperties.Add(new FightPropertyValue { Id = formula.PropertyId("CureAddPercent"), Value = 5000 });
        owner.Statuses.Add(new FightBattleStatusState { ClassId = 999999, InstanceId = 99,
            OwnerId = 1, CreatorId = 1, StackCount = 1,
            Components = new FightBattleStatusComponentState { CureReduce = new FightBattleStatusCureReduceState {
                DefinitionResolved = true, ReduceCurePer = 1f } } });
        enemy.Hp = enemy.Turn.Hp = 0;
        long beforeVictory = owner.Hp;
        var victory = executor.Step(state.Sequence, state.Counter + 1).Event;
        Check(state.Outcome.Won, "the fight did not settle as a victory");
        Check(owner.Hp == beforeVictory + 375,
            "victory return did not preserve the cure bonus or bypass the cure restriction");
        Check(victory.DamageEvents.Any(hit => hit.SkillEntityId == 10163 && hit.IsCure
            && hit.AfterHp - hit.BeforeHp == 375), "victory return did not emit the original cure hit");
    }

    private static void CheckTerminal(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, long remaining, int outcome)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 2, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        var owner = Unit(1, 1, 4, 1000, formula);
        var enemy = Unit(2, 2, 20, 1000, formula);
        if (outcome == 2) state.ResultRules = new FightBattleResultRules { Kind = FightBattleResultRules.Duel };
        owner.Statuses.Add(catalog.CreateStatus(25042, 1, 1, 1, 25042, 10575, 4, 2, 8, owner.Properties));
        state.Units.AddRange(new[] { owner, enemy, Unit(3, 1, 3, 1000, formula) });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(2);
        state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        executor.Step(state.Sequence, state.Counter + 1);
        owner.Statuses.Single(status => status.ClassId == 24239).ShieldRemainValue = remaining;
        owner.BaseProperties.Add(new FightPropertyValue { Id = formula.PropertyId("CureAddPercent"), Value = 5000 });
        if (outcome != 1) enemy.Hp = enemy.Turn.Hp = 0;
        if (outcome == 2) owner.Hp = owner.Turn.Hp = 0;
        string pending = FightBattleCheckpointSerializer.Save(state);
        var copy = FightBattleCheckpointSerializer.Load(pending);
        var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        var events = new List<FightBattleStepEvent>();
        for (int i = 0; i < 20 && !state.Finished; i++)
        {
            events.Add(executor.Step(state.Sequence, state.Counter + 1).Event);
            restored.Step(copy.Sequence, copy.Counter + 1);
        }
        Check(state.Finished && state.Outcome.Won == (outcome != 1),
            $"unexpected terminal outcome (case {outcome}, remaining {remaining}, finished {state.Finished}, won {state.Outcome.Won}, reason {state.Outcome.Reason})");
        Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
            "terminal shield conversion changed after restore");
        long expected = outcome == 2 ? 0 : outcome == 1 ? 500
            : Math.Min(1000, 500 + (long)(remaining * 1.5f));
        Check(owner.Hp == expected, "terminal return ignored the shield ratio, HP cap, victory, or survival");
        int cures = events.Sum(step => step.DamageEvents.Count(hit => hit.SkillEntityId == 10163));
        Check(cures == (outcome == 0 && remaining > 0 ? 1 : 0), "terminal return emitted an unexpected cure");
        string settled = FightBattleCheckpointSerializer.Save(state);
        var settledCopy = FightBattleCheckpointSerializer.Load(settled);
        var settledExecutor = FightBattleExecutor.Restore(settledCopy, formula,
            new FightDamageSettlement(), catalog, items);
        var settledReplay = settledExecutor.Step(settledCopy.Sequence, settledCopy.Counter);
        Check(owner.Turn.Hp == owner.Hp && settledReplay.Replayed
            && FightBattleCheckpointSerializer.Save(settledCopy) == settled,
            "restoring the settled victory changed its HP, turn queue or return receipt");
        var replay = executor.Step(state.Sequence, state.Counter);
        Check(replay.Replayed && FightBattleCheckpointSerializer.Save(state) == settled,
            "replaying the terminal receipt settled the return twice");
    }

    private static FightBattleUnitState Unit(int id, int side, int x, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 4,
            Level = 70, SubRank = 8, X = x, Y = 4, Hp = hp, MaxHp = 1000, LastMaxHp = 1000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = hp;
        unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", 1000 }, { "Speed", unit.Turn.Speed }, { "BaseCureAdd", 10000 }, { "BaseBeCureAdd", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
