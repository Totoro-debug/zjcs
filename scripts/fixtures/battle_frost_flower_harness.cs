using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFrostFlowerHarness
{
    static void Check(bool value, string text) { if (!value) throw new InvalidOperationException("Frost flower: " + text); }
    public static string Run(FightOfficialCombatCatalog c, FightDamageFormula f, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int seed in new[] { 1, 17, 50000 })
        foreach (bool friendly in new[] { false, true })
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var actor = Unit(1, 10, 10, 1); cp.Units.Add(actor);
            var skill = c.CreateSkill(10750, 0, 10044, 2, 20, 5, c.CalculateSkillProps(10750, 2, 20, 5)
                .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            var definition = FightBattleHitCollector.CollectAll(skill).Single();
            Check(definition.EntityId == 10751 && definition.DamageId == 10752 && definition.ActionType == 4,
                "official hit graph differs");
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            var wave = new FightBattleWaveState();
            int id = 2;
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                var unit = Unit(id++, 10 + x, 10 + y, friendly && x == -1 && y == -1 ? 1 : 2);
                cp.Units.Add(unit); if (unit.Side == 2) wave.UnitIds.Add(unit.Id);
            }
            var outside = Unit(id, 12, 10, 2); cp.Units.Add(outside); wave.UnitIds.Add(outside.Id); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, f, new FightDamageSettlement(), c, items);
            var first = executor.Step(cp.Sequence, cp.Counter + 1).Event;
            Check(first.SkillCasts.Count(x => x.SkillEntityId == 10750 && x.ActorId == 1) == 1,
                "original skill did not cast");
            var hits = first.DamageEvents.Where(x => x.SkillEntityId == 10750).ToArray();
            var expected = cp.Units.Where(x => x.Side == 2 && x.Id != outside.Id).Select(x => x.Id).OrderBy(x => x).ToArray();
            Check(hits.Length == expected.Length && hits.Select(x => x.TargetId).OrderBy(x => x).SequenceEqual(expected)
                && hits.All(x => x.SourceId == 1 && x.Damage < 0 && x.ElementType == 101),
                "water hit targets differ from the original square or include a friendly/outside unit");
            string json = FightBattleCheckpointSerializer.Save(cp);
            var copy = FightBattleCheckpointSerializer.Load(json);
            var resume = FightBattleExecutor.Restore(copy, f, new FightDamageSettlement(), c, items);
            executor.Step(cp.Sequence, cp.Counter + 1); resume.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                "restored square-area fight diverged");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "receipt replay repeated the area hit");
            cases++;
        }
        return "Frost flower passed: " + cases + " original area/friendly/water/restoration/replay cases.";
    }
    static FightBattleUnitState Unit(int id, int x, int y, int side)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 2,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
        foreach (var p in new Dictionary<int, long> { {1, 1000}, {2, 10}, {3, 1000000}, {4, unit.Turn.Speed} })
            unit.Properties.Add(new FightPropertyValue { Id = p.Key, Value = p.Value });
        return unit;
    }
}
