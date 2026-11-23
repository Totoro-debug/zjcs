using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleVitalitySpringHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Vitality spring: " + message);
    }

    private static FightBattleUnitState Unit(int id, int side, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1,
            Profession = 3, Level = 70, SubRank = 8, X = id + 2, Y = 5,
            Hp = hp, MaxHp = 1000000, LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var pair in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, 1000000 }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = pair.Key, Value = pair.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge", "BaseCureAdd", "BaseBeCureAdd" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4 })
        foreach (int level in new[] { 1, 2, 20 })
        {
            var actor = Unit(1, 1, 1000, formula);
            var enemy = Unit(2, 2, 1000000, formula);
            var status = catalog.CreateStatus(25040, 1, 1, 725040, 25040, 10572, rank, level, 8);
            actor.Statuses.Add(status);
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 7,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            cp.Units.Add(actor); cp.Units.Add(enemy);
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event;
            resumed.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "restoring before the turn changed the result");
            var receipt = step.DamageEvents.Single(value => value.SkillEntityId == 11560);
            var props = catalog.CalculateSkillProps(11560, rank, level, 8).ToDictionary(value => value.Key, value => value.Value);
            long expected = (long)(1000000 * formula.ReadProperty(props, 210) + formula.ReadProperty(props, 170));
            Check(receipt.SourceId == 1 && receipt.TargetId == 1 && receipt.SkillItemId == 10572
                && receipt.HitEntityId == 11561 && receipt.DamageEntityId == 11562 && receipt.IsCure,
                "the original self-cure graph or source was lost");
            Check(expected > 0 && receipt.Damage == expected && actor.Hp == 1000 + expected,
                "the recovery does not match the cultivated maximum HP and fixed cure values: expected=" + expected
                + ", receipt=" + receipt.Damage + ", hp=" + actor.Hp + ", rank=" + rank + ", level=" + level);
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "receipt replay repeated the cure");
            bool nextTurn = false;
            for (int count = 0; count < 10 && !cp.Finished; count++)
            {
                var next = executor.Step(cp.Sequence, cp.Counter + 1).Event;
                resumed.Step(restored.Sequence, restored.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                    "restoring changed a later turn");
                var cures = next.DamageEvents.Where(value => value.SkillEntityId == 11560).ToArray();
                if (next.ActorId != 1)
                    Check(cures.Length == 0, "the cure triggered on another unit's turn");
                else if (next.ActorRound > 1)
                {
                    Check(cures.Length == 1 && cures[0].Damage == expected,
                        "the next personal turn did not repeat the cultivated cure");
                    nextTurn = true;
                    break;
                }
            }
            Check(nextTurn, "the next personal turn was not reached");
            cases++;
        }
        return "Vitality spring passed: " + cases + " cultivation, self-cure and checkpoint cases.";
    }
}
