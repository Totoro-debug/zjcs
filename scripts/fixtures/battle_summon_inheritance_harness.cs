using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleSummonInheritanceHarness
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        // Pinned 854 tables: rock spirit, flower and mapping substitute, skill rank 5/level 20.
        // Primary coefficients differ by both summon and skill rank; secondary raw values include
        // the crit denominator, rather than turning the owner's displayed chance into a new stat.
        var source = new Dictionary<int, long> { {1, 10001}, {2, 10003}, {3, 100007},
            {4, 101}, {58, 2000}, {75, 77}, {79, 231} };
        var rock = catalog.CalculateSummonInheritance(3181, 20, 5, 1, source);
        Require(rock.Fixed[3] == 559 && rock.Summoner[1] == 7000
            && rock.Summoner[2] == 8002 && rock.Summoner[3] == 10800
            && rock.Summoner[4] == 70, "Rock spirit fixed/rank inheritance differs from pinned tables.");
        var flower = catalog.CalculateSummonInheritance(3182, 20, 5, 1, source);
        Require(flower.Fixed[1] == 507 && flower.Summoner[1] == 6000
            && flower.Summoner[2] == 6802 && flower.Summoner[3] == 8000
            && flower.Summoner[4] == 111, "Flower used another summon's inheritance or rounded each factor.");
        var doll = catalog.CalculateSummonInheritance(4600, 20, 5, 1, source);
        Require(doll.Fixed[3] == 3900 && doll.Summoner[2] == 6001
            && doll.Summoner[3] == 12688 && doll.Summoner[4] == 101
            && !doll.Summoner.ContainsKey(1), "Substitute inheritance lost its 60% defence/no attack rule.");
        foreach (var values in new[] {rock, flower, doll})
            Require(values.Summoner[58] == 2000 && values.Summoner[75] == 77
                && values.Summoner[79] == 231, "Crit percentage/value/base were not inherited together.");
        var invalidRank = catalog.CalculateSummonInheritance(3181, 20, 9999, 1, source);
        Require(invalidRank.Summoner[1] == 0, "Missing rank invented a fallback inheritance factor.");

        var player = new FightBattleUnitState { Id = 1, Side = 1, CharacterType = 1,
            Level = 150, SubRank = 1, X = 5, Y = 5, Hp = 100007, MaxHp = 100007 };
        foreach (var value in source) player.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        player.Turn = new FightTurnUnit { Id = 1, Hp = player.Hp, Speed = 101 };
        var skill = catalog.CreateSkill(10710, 0, 10001, 5, 20, 1, null);
        player.Skills.Add(skill);
        player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var enemy = new FightBattleUnitState { Id = 2, Side = 2, CharacterType = 2,
            X = 9, Y = 9, Hp = 1000, MaxHp = 1000,
            Turn = new FightTurnUnit { Id = 2, Hp = 1000, Speed = 1 } };
        var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 13 };
        state.Units.Add(player); state.Units.Add(enemy);
        var executor = FightBattleExecutor.Begin(state, formula,
            new FightDamageSettlement(), catalog, items);
        executor.Step(0, 1);
        var summon = state.Units.Find(value => value.MonsterId == 3181);
        Require(summon != null && summon.Level == 20 && summon.MasterId == 1
            && summon.MaxHp == 11360 && summon.Hp == 11360,
            "Actual skill 10710 did not spawn a living spirit at skill level with fixed + owner HP: "
            + (summon == null ? "missing; units=" + state.Units.Count : "level=" + summon.Level
                + ", master=" + summon.MasterId + ", max=" + summon.MaxHp + ", hp=" + summon.Hp));
        Require(summon.Properties.Find(value => value.Id == 1).Value == 7001,
            "Actual executor did not consume inherited attack.");
        string saved = FightBattleCheckpointSerializer.Save(state);
        var loaded = FightBattleCheckpointSerializer.Load(saved);
        Require(loaded.Units.Find(value => value.MonsterId == 3181).MaxHp == 11360,
            "Summon inheritance changed during checkpoint restore.");
    }
}
