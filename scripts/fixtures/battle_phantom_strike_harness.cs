using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePhantomStrikeHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Phantom strike: " + message); }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var property in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = property.Key, Value = property.Value });
        foreach (string name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (var direction in new[] { new[] { 1, 0, 3 }, new[] { -1, 0, 1 }, new[] { 0, 1, 2 }, new[] { 0, -1, 0 } })
        foreach (int rank in new[] { 1, 2 })
        {
            int dx = direction[0], dy = direction[1];
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction[2];
            var target = Unit(2, 2, 7 + dx, 7 + dy, formula);
            var skill = catalog.CreateSkill(11270, 0, 10054, rank, 20, 1,
                catalog.CalculateSkillProps(11270, rank, 20, 1)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            Check(skill.SourceMoves.Count == 1 && skill.SourceMoves[0].MoveBehaviourId == 11272
                && skill.SourceMoves[0].Distance == 3 && !skill.SourceMoves[0].TowardsTargetPos,
                "Original retreat configuration changed");
            checkpoint.Units.Add(actor); checkpoint.Units.Add(target);
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resume.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
                "Cold continuation changed the result");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10054);
            var damage = step.DamageEvents.Single(value => value.SkillItemId == 10054);
            Check(cast.SkillEntityId == 11270 && damage.HitEntityId == 11271 && damage.DamageEntityId == 11273
                && damage.TargetId == 2 && damage.ElementType == 0 && damage.Damage < 0
                && target.Hp == target.MaxHp + damage.Damage && target.X == 7 + dx && target.Y == 7 + dy,
                "The original physical hit or target changed");
            var movement = step.Movements.Single(value => value.UnitId == 1);
            Check(movement.MoveBehaviourId == 11272 && movement.Path.Count == 4
                && Enumerable.Range(0, 4).All(index => movement.Path[index].X == 7 - dx * index
                    && movement.Path[index].Y == 7 - dy * index)
                && actor.X == 7 - 3 * dx && actor.Y == 7 - 3 * dy,
                "The source did not retreat three unobstructed cells");
            var saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(checkpoint) == saved,
                "Receipt replay repeated the hit or retreat");
            cases++;
        }
        return "Phantom strike: " + cases + " directions and ranks passed.";
    }
}
