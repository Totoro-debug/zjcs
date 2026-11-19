using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleHealingSkillHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Healing: " + message); }

    private static FightBattleUnitState Unit(int id, int side, int x, long hp, long maxHp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
            Level = 50, SubRank = 5, X = x, Y = 5, Hp = hp, MaxHp = maxHp, LastMaxHp = maxHp,
            MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, maxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge", "BaseCureAdd", "BaseBeCureAdd" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        for (int direction = 0; direction < 4; direction++)
        for (int seed = 1; seed <= 8; seed++)
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 5, 1000000, 1000000, formula); actor.Direction = direction;
            var nearer = Unit(2, 1, 6, 150000, 1000000, formula);
            var target = Unit(3, 1, 9, 200000, 2000000, formula);
            var enemy = Unit(4, 2, 11, 1, 1000000, formula);
            foreach (var pair in new[] { new[] { 24020, 101 }, new[] { 24215, 102 } })
            {
                var status = catalog.CreateStatus(pair[0], target.Id, enemy.Id, pair[1], 0, 0, 2, 20, 1);
                status.DurationRound = 5; target.Statuses.Add(status);
            }
            var skill = catalog.CreateSkill(10720, 0, 10020, 2, 20, 1,
                catalog.CalculateSkillProps(10720, 2, 20, 1).Select(value =>
                    new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10721);
            Check(skill.TargetType == 2 && hit.DamageId == 10722 && hit.ActionType == 5
                && hit.DisperseCount == 1 && hit.DisperseRandom && hit.DisperseStatusTypes.SequenceEqual(new[] { 2, 3 }),
                "The original healing or random-dispel configuration changed.");
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, nearer, target, enemy });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resumed.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Reload changed the target, recovery or random dispel.");
            var receipt = step.DamageEvents.Single(value => value.SkillEntityId == 10720);
            Check(step.SkillEntityId == 10720 && receipt.TargetId == target.Id && receipt.IsCure
                && receipt.HitEntityId == 10721 && receipt.DamageEntityId == 10722 && target.Hp > 200000
                && nearer.Hp == 150000 && enemy.Hp == 1,
                "The lowest health percentage among eligible allies must receive the cure.");
            Check(target.Statuses.Count(status => status.ClassId == 24020 || status.ClassId == 24215) == 1
                && receipt.HasDisperseNotification && step.DispersedUnitIds.SequenceEqual(new[] { target.Id }),
                "Exactly one eligible negative status must be removed and reported.");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated healing or dispel.");
            cases++;
        }
        var source = Unit(1, 1, 5, long.MaxValue, long.MaxValue, formula);
        var near = Unit(2, 1, 6, long.MaxValue - 2, long.MaxValue, formula);
        var lower = Unit(3, 1, 7, long.MaxValue - 3, long.MaxValue, formula);
        var outside = Unit(4, 1, 13, 1, long.MaxValue, formula);
        var selectionSkill = catalog.CreateSkill(10720, 0, 10020, 2, 20, 1,
            catalog.CalculateSkillProps(10720, 2, 20, 1).Select(value =>
                new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        var selection = FightBattleTargeting.Select(source, new[] { source, near, lower, outside }, 2,
            skill: selectionSkill);
        Check(selection.Count == 1 && selection[0].Id == lower.Id,
            "Fraction comparison must remain exact for large HP values and respect the seven-cell range.");
        return "Healing target, cure and random dispel passed: " + cases;
    }
}
