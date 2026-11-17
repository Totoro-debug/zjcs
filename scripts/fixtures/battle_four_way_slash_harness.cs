using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFourWaySlashHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Four-way slash: " + message); }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
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
        foreach (int facing in new[] { 0, 1, 2, 3 })
        foreach (var growth in new[] { new[] { 2, 20, 1 }, new[] { 7, 30, 5 } })
        foreach (string mode in new[] { "single", "four-targets", "four-body-cells", "outside" })
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = facing;
            var target = Unit(2, 2, 7, 8, formula);
            if (mode == "four-body-cells")
                foreach (var offset in new[] { new[] { -1, -1 }, new[] { 1, -1 }, new[] { 0, -2 } })
                    target.BodyOffsets.Add(new FightGridPoint(offset[0], offset[1]));
            var skill = catalog.CreateSkill(10650, 0, 10013, growth[0], growth[1], growth[2],
                catalog.CalculateSkillProps(10650, growth[0], growth[1], growth[2])
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10651);
            Check(skill.HitGraphRoots.Count == 1 && hit.ScopeEntries.Count == 4 && hit.DamageId == 10022
                && !hit.TrueDamage && skill.ElementType == 0, "Original area hit configuration changed");
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target });
            if (mode == "four-targets")
                foreach (var extra in new[] { new[] { 3, 7, 6 }, new[] { 4, 6, 7 }, new[] { 5, 8, 7 } })
                    cp.Units.Add(Unit(extra[0], 2, extra[1], extra[2], formula));
            if (mode == "outside") cp.Units.Add(Unit(3, 2, 8, 8, formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(unit => unit.Side == 2).Select(unit => unit.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resume.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Cold continuation changed the original area attack");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10013);
            var hits = step.DamageEvents.Where(value => value.SkillItemId == 10013).ToArray();
            int expected = mode == "four-targets" || mode == "four-body-cells" ? 4 : 1;
            Check(hits.Length == expected && hits.All(value => value.HitEntityId == 10651
                && value.DamageEntityId == 10022 && value.ElementType == 0 && !value.TrueDamage && value.Damage < 0
                && Math.Abs(value.Delay - cast.Delay - skill.HitGraphRoots[0].Delay) < .0001f),
                "Original physical area hit count, identity or timing changed: " + mode + ", hits=" + hits.Length);
            if (mode == "four-targets") Check(hits.Select(value => value.TargetId).Distinct().Count() == 4,
                "The four adjacent targets were not hit once each");
            if (mode == "four-body-cells") Check(hits.All(value => value.TargetId == target.Id)
                && hits.Select(value => value.HitGridX + ":" + value.HitGridY).Distinct().Count() == 4,
                "The original large target did not retain independent covered cells");
            if (mode == "outside") Check(hits.All(value => value.TargetId != 3), "A diagonal target outside the original scope was hit");
            var saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated the area attack");
            cases++;
        }
        return "Four-way slash passed: " + cases + " original facing, growth, four-target, body-cell, outside-scope and cold/replay cases.";
    }
}
