using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWindFeatherHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Wind feather: " + message); }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 10,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 },
            new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        foreach (bool multiple in new[] { false, true })
        foreach (int seed in new[] { 31, 1012348 })
        {
            int dx = direction[0], dy = direction[1], sx = -dy, sy = dx;
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, 10, formula); actor.Direction = direction[2];
            var first = Unit(2, 2, 10 + 3 * dx, 10 + 3 * dy, formula);
            cp.Units.Add(actor); cp.Units.Add(first);
            if (multiple) cp.Units.Add(Unit(3, 2, first.X + sx, first.Y + sy, formula));
            var skill = catalog.CreateSkill(10910, 0, 10036, 2, 20, 1,
                catalog.CalculateSkillProps(10910, 2, 20, 1).Select(value =>
                    new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(value => value.Side == 2).Select(value => value.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; restored.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                "Reload changed the random centres, children or falloff.");
            Check(step.SkillEntityId == 10910 && step.SkillCasts[0].SkillEntityId == 10910,
                "The parent skill must be recorded before its wind-blade children.");
            var parents = step.DamageEvents.Where(value => value.HitEntityId == 10912).ToArray();
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11050 && value.HitEntityId == 11051).ToArray();
            var children = step.SkillCasts.Where(value => value.SkillEntityId == 11050).ToArray();
            int targetCount = multiple ? 2 : 1;
            Check(parents.Length == 7 * targetCount && hits.Length == 7 * targetCount
                && children.Length == 7 * targetCount,
                "Each of seven waves must select one centre and affect each nearby target once.");
            for (int row = 0; row < 7; row++)
            {
                float expected = .37f + row * .1f;
                Check(parents.Count(value => Math.Abs(value.Delay - expected) < .0001f) == targetCount
                    && children.Count(value => Math.Abs(value.Delay - expected) < .0001f) == targetCount,
                    "Each child cast must begin at the actual parent-hit boundary.");
            }
            foreach (var child in children)
            {
                var hit = hits.Single(value => child.DamageEventIds.Contains(value.DamageId));
                Check(hit.TargetId == child.Presentation.TargetId && hit.Delay > child.Delay,
                    "Each original projectile must retain its parent target and travel after launch.");
            }
            foreach (var group in hits.GroupBy(value => value.TargetId))
            {
                var targetHits = group.ToArray(); long baseDamage = Math.Abs(targetHits[0].Damage);
                Check(targetHits.Length == 7 && baseDamage > 0, "Every nearby target must receive seven wind hits.");
                double retained = 1f - (float).35f;
                for (int i = 1; i < targetHits.Length; i++)
                    Check(Math.Abs(targetHits[i].Damage) == (long)(baseDamage * Math.Pow(retained, i)),
                        "Repeated hits must apply the 35-percent falloff independently to each target.");
            }
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated the wind-blade children.");
            cases++;
        }
        return "Wind feather random centres, child timing, falloff and restore passed: " + cases;
    }
}
