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
        foreach (long hp in new long[] { 1, 100, 101, 1000 })
            CheckOpening(catalog, formula, items, hp);
        return "Soul guard opening, victory return, and checkpoint cases passed.";
    }

    private static void CheckOpening(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, long hp)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        var owner = Unit(1, 1, 4, hp, formula);
        var enemy = Unit(2, 2, 20, 1000, formula);
        var passive = catalog.CreateStatus(25042, 1, 1, 1, 25042, 10575, 1, 1, 8, owner.Properties);
        var previousShield = catalog.CreateStatus(24180, 1, 1, 2, 0, 0, 1, 1, 8, owner.Properties, 300);
        previousShield.ShieldUsesCustomValue = true;
        owner.Statuses.Add(previousShield);
        owner.Statuses.Add(passive);
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
