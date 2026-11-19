using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWaterDragonHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Water dragon: " + message); }

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
        {
            int dx = direction[0], dy = direction[1], sx = -dy, sy = dx;
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, 10, formula); actor.Direction = direction[2];
            var small = Unit(2, 2, 10 + dx, 10 + dy, formula);
            var large = Unit(3, 2, 10 + 2 * dx, 10 + 2 * dy, formula);
            large.BodyOffsets.AddRange(new[] { new FightGridPoint(0, 0), new FightGridPoint(sx, sy),
                new FightGridPoint(dx, dy), new FightGridPoint(dx + sx, dy + sy) });
            var outside = Unit(4, 2, 10 + 5 * dx, 10 + 5 * dy, formula);
            var skill = catalog.CreateSkill(10550, 0, 10003, 2, 20, 1,
                catalog.CalculateSkillProps(10550, 2, 20, 1).Select(value =>
                    new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            Check(skill.HitGraphRoots.Count == 4 && skill.HitGraphRoots.Select(value => value.EntityId)
                .SequenceEqual(new[] { 10551, 10552, 10553, 10554 }), "The four original hit entities changed.");
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, small, large, outside });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4 }); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; restored.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                "Reload changed the hit sequence or its per-target falloff.");
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 10550 && value.TargetId == large.Id).ToArray();
            Check(hits.Length == 4 && hits.Count(value => value.HitEntityId == 10552) == 2
                && hits.Count(value => value.HitEntityId == 10553) == 2
                && step.DamageEvents.Count(value => value.TargetId == small.Id && value.SkillEntityId == 10550) == 1
                && step.DamageEvents.All(value => value.TargetId != outside.Id),
                "The original 3-by-4 range must count each struck body cell and exclude outside targets.");
            long first = Math.Abs(hits[0].Damage);
            Check(first > 0, "The initial water hit must cause damage.");
            for (int i = 1; i < hits.Length; i++)
                Check(Math.Abs(hits[i].Damage) == (long)(first * Math.Pow(.75, i)),
                    "Each repeated hit must retain 75 percent of the preceding falloff level.");
            Check(Math.Abs(step.DamageEvents.Single(value => value.TargetId == small.Id && value.SkillEntityId == 10550).Damage) == first,
                "Damage to another target must not inherit the large target's falloff.");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated the four waves.");
            cases++;
        }
        var source = Unit(1, 1, 7, 5, formula);
        var target = Unit(2, 2, 7, 7, formula);
        var firstSkill = catalog.CreateSkill(10550, 0, 10003, 2, 20, 1,
            catalog.CalculateSkillProps(10550, 2, 20, 1).Select(value =>
                new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        int serial = 0; FightDamageFalloff.BeginCast(firstSkill, ref serial);
        var firstHit = FightBattleHitCollector.CollectAll(firstSkill, includeRootViews: true)
            .Single(value => value.EntityId == 10551);
        var falloff = catalog.CreateStatus(24103, target.Id, source.Id, 1, 10550, 10003, 2, 20, 1);
        var damage = new FightBattleDamageEvent { SourceId = source.Id, TargetId = target.Id, Dodge = true };
        FightDamageFalloff.RecordApplyingHit(source, target, firstSkill, firstHit, falloff, damage);
        Check(target.FalloffCounts.Count == 0, "A dodged hit must not seed the falloff counter.");
        damage.Dodge = false;
        FightDamageFalloff.RecordApplyingHit(source, target, firstSkill, firstHit, falloff, damage);
        FightDamageFalloff.RecordApplyingHit(source, target, firstSkill, firstHit, falloff, damage);
        Check(target.FalloffCounts.Count == 1 && target.FalloffCounts[0].Count == 1,
            "The applying hit must seed its own target exactly once.");
        FightDamageFalloff.EndCast(new[] { source, target }, firstSkill);
        Check(target.FalloffCounts.Count == 0, "A completed cast must clear its per-target falloff counter.");
        return "Water dragon body-cell hits, falloff and restore passed: " + cases;
    }
}
