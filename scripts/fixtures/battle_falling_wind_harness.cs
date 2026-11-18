using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFallingWindHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Falling wind: " + message); }

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
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        foreach (int distance in new[] { 1, 2, 3, 4 })
        {
            int tx = 7 + direction[0] * distance, ty = 7 + direction[1] * distance;
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction[2];
            var target = Unit(2, 2, tx, ty, formula);
            var skill = catalog.CreateSkill(10590, 0, 10043, 2, 20, 1,
                catalog.CalculateSkillProps(10590, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10591);
            Check(skill.SkillTargetType == 0 && skill.ElementType == 100 && hit.DamageId == 10592
                && hit.ScopeEntries.Count == 5 && skill.Range.Count == 41,
                "Original point target, wind damage or scope changed");
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resumed.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored), "Cold restore changed the hit");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10043);
            var damage = step.DamageEvents.Single(value => value.SkillItemId == 10043);
            Check(cast.SkillEntityId == 10590 && cast.Presentation.TargetX == tx && cast.Presentation.TargetY == ty
                && damage.TargetId == target.Id && damage.HitEntityId == 10591 && damage.DamageEntityId == 10592
                && damage.ElementType == 100 && !damage.TrueDamage && damage.Damage < 0
                && Math.Abs(damage.Delay - cast.Delay - .45f) < .0001f,
                "Original wind damage, target point or center timing changed");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved, "Receipt replay repeated the hit");
            cases++;
        }
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        {
            int tx = 10 + 3 * direction[0], ty = 10 + 3 * direction[1];
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, 10, formula); actor.Direction = direction[2];
            var skill = catalog.CreateSkill(10590, 0, 10043, 2, 20, 1,
                catalog.CalculateSkillProps(10590, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.Add(actor); int next = 2;
            foreach (var offset in new[] { new[] {0,0}, new[] {-1,0}, new[] {1,0}, new[] {0,-1}, new[] {0,1} })
                cp.Units.Add(Unit(next++, 2, tx + offset[0], ty + offset[1], formula));
            cp.Units.Add(Unit(next, 2, tx + 3 * direction[1], ty + 3 * direction[0], formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(value => value.Side == 2).Select(value => value.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event;
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10043);
            var hits = step.DamageEvents.Where(value => value.SkillItemId == 10043).ToArray();
            Check(cast.Presentation.TargetX == tx && cast.Presentation.TargetY == ty && hits.Length == 5
                && hits.Select(value => value.TargetId).Distinct().Count() == 5 && hits.All(value => value.TargetId != next),
                "Original cross did not damage each of five targets exactly once");
            foreach (var damage in hits)
            {
                bool center = damage.HitGridX == tx && damage.HitGridY == ty;
                Check(Math.Abs(damage.Delay - cast.Delay - (center ? .45f : .55f)) < .0001f,
                    "Center and outer cells lost their original staged timing");
            }
            cases++;
        }
        return "Falling wind passed: " + cases + " four-direction, reach, cross-scope, staged timing and cold/replay cases.";
    }
}
