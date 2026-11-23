using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleAlienationHarness
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Alienation: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int statusType in new[] { 0, 2, 3 })
        foreach (int count in new[] { 0, 1, 2 })
        foreach (bool onAttacker in new[] { false, true })
        {
            var target = Unit(1, 1);
            var source = Unit(2, 2);
            var passive = catalog.CreateStatus(25048, 1, 1, 25048, 25048, 10579, rank, level, 8);
            target.Statuses.Add(passive);
            for (int i = 0; i < count; i++)
                (onAttacker ? source : target).Statuses.Add(new FightBattleStatusState {
                    ClassId = 90000 + i, InstanceId = 90000 + i, StatusType = statusType,
                    OwnerId = onAttacker ? 2 : 1, CreatorId = 2, DurationRound = -1 });
            var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 37 };
            checkpoint.Units.AddRange(new[] { target, source });
            FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var skill = new FightBattleSkillDefinition { EntityId = 10600, TargetType = 1 };
            var actual = FightBattleStatusDamageReducePerRuntime.Resolve(source, target, skill,
                new FightDamageTypeData(), false, new FightRandomStream(37), formula);
            var replay = FightBattleStatusDamageReducePerRuntime.Resolve(restored.Units[1], restored.Units[0], skill,
                new FightDamageTypeData(), false, new FightRandomStream(37), formula);
            var properties = catalog.CalculateSkillProps(25048, rank, level, 8);
            bool applies = onAttacker && count > 0 && (statusType == 2 || statusType == 3);
            double percent = applies && properties.TryGetValue(232, out long p) ? p / 10000.0 : 0;
            double fixedValue = applies && properties.TryGetValue(236, out long f) ? f : 0;
            Check(passive.DamageReducePer != null && passive.DamageReducePer.Supported,
                "the configured conditional reduction is unavailable");
            Check(actual.Percent == percent && actual.Fixed == fixedValue
                && replay.Percent == percent && replay.Fixed == fixedValue,
                "attacker filtering, capped status count, cultivation or checkpoint restore changed");
            Check(target.Properties.All(value => value.Id != 232 && value.Id != 236),
                "conditional reduction was added unconditionally");
            var hit = new FightBattleHitDefinition { ActionType = 4, DamageType = 8, DamageParam = 1000 };
            var executor = new FightBattleHitExecutor(formula, new FightDamageSettlement());
            var settled = executor.Apply(new FightRandomStream(37), 1, source, target, skill, hit, 1, false);
            long expectedDamage = (long)(Math.Max(0, 1000 - fixedValue) / (1 + percent));
            Check(settled.Settlement.RealDamageValue == -expectedDamage,
                "conditional reduction did not reach hit settlement");
            cases++;
        }
        return "Alienation passed: " + cases + " attacker status, cultivation and checkpoint cases.";
    }

    static FightBattleUnitState Unit(int id, int side)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1,
            Level = 70, SubRank = 8, Hp = 10000, MaxHp = 10000, LastMaxHp = 10000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = 100;
        unit.Properties.Add(new FightPropertyValue { Id = 1, Value = 10000 });
        return unit;
    }
}
