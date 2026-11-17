using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWindBladeHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Wind blade: " + message); }

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
        int cases = 0, moved = 0, resisted = 0, firstUpMoveSeed = 0;
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        foreach (int distance in new[] { 1, 2, 3, 4 })
        foreach (string mode in new[] { "baseline", "boosted", "suppressed", "blocked" })
        foreach (int seed in Enumerable.Range(1, 8))
        {
            int dx = direction[0], dy = direction[1];
            int initialSeed = seed * 1000003 + 12345;
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = initialSeed,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction[2];
            var target = Unit(2, 2, 7 + dx * distance, 7 + dy * distance, formula);
            if (mode == "boosted" || mode == "blocked")
                actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
            if (mode == "suppressed")
                target.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = 20000 });
            if (mode == "blocked")
                cp.Grid.BlockedPositions.Add(new FightGridPoint(7 + dx * (distance + 1), 7 + dy * (distance + 1)));
            var skill = catalog.CreateSkill(10640, 0, 10012, 2, 20, 1,
                catalog.CalculateSkillProps(10640, 2, 20, 1)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10641);
            Check(hit.HitMove != null && hit.HitMove.MoveBehaviourId == 10643 && hit.HitMove.Distance == 1
                && hit.HitMove.Direction == 0 && Math.Abs(hit.HitMove.BasePercent - .5f) < .00001f
                && hit.HitMove.AffectedByProp && !hit.HitMove.TowardsTargetPos,
                "Original 10642 knockback configuration changed");
            Check(hit.ScopeEntries.Count == 4 && skill.ElementType == 100, "Original wind strip or element changed");
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(target.Id); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            double chance = formula.CalculateStatusEffectRate(actor.Properties, target.Properties, hit.HitMove.BasePercent, 0);
            Check(!double.IsNaN(chance) && (mode == "baseline" ? chance > 0 && chance < 1
                    : mode == "suppressed" ? chance < 0 : chance > 1) && catalog.CanStartEntityAction(target, 10643),
                "Fixture does not retain its original property-adjusted knockback roll: chance=" + chance
                    + ", allowed=" + catalog.CanStartEntityAction(target, 10643));
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resume.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Cold continuation changed the hit or random knockback");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10012);
            var damage = step.DamageEvents.Single(value => value.SkillItemId == 10012);
            Check(cast.SkillEntityId == 10640 && damage.HitEntityId == 10641 && damage.DamageEntityId == 10642
                && damage.TargetId == target.Id && damage.ElementType == 100 && !damage.TrueDamage && damage.Damage < 0
                && Math.Abs(damage.Delay - cast.Delay - .6f - hit.ScopeEntries[distance - 1].Delay) < .0001f,
                "Original wind hit, target, element or cell delay changed at distance " + distance);
            var moves = step.Movements.Where(value => value.UnitId == target.Id).ToArray();
            Check(moves.Length <= 1, "One hit produced more than one target displacement");
            if (moves.Length == 1)
            {
                Check(target.X == 7 + dx * (distance + 1) && target.Y == 7 + dy * (distance + 1)
                    && moves[0].MoveBehaviourId == 10643 && moves[0].Path.Count == 2,
                    "Original knockback did not move exactly one cell away from the caster: direction="
                        + dx + "," + dy + ", distance=" + distance + ", target=" + target.X + "," + target.Y
                        + ", move entity=" + moves[0].MoveBehaviourId + ", path length=" + moves[0].Path.Count);
                Check(mode != "suppressed" && mode != "blocked", "Suppressed or blocked knockback moved the target");
                if (mode == "baseline" && dx == 0 && dy == 1 && distance == 1 && firstUpMoveSeed == 0)
                    firstUpMoveSeed = initialSeed;
                moved++;
            }
            else
            {
                Check(target.X == 7 + dx * distance && target.Y == 7 + dy * distance,
                    "A failed knockback still moved the target");
                Check(mode != "boosted", "Effect-rate boost did not guarantee the original knockback");
                resisted++;
            }
            var saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated the hit or displacement");
            cases++;
        }
        Check(moved > 0 && resisted > 0, "The seeded original probability never produced both outcomes: moved="
            + moved + ", no movement=" + resisted);
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        {
            int dx = direction[0], dy = direction[1];
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction[2];
            var skill = catalog.CreateSkill(10640, 0, 10012, 2, 20, 1,
                catalog.CalculateSkillProps(10640, 2, 20, 1)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.Add(actor);
            for (int distance = 1; distance <= 4; distance++)
                cp.Units.Add(Unit(distance + 1, 2, 7 + dx * distance, 7 + dy * distance, formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(unit => unit.Side == 2).Select(unit => unit.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resume.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Cold continuation changed the four-target hit");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10012);
            var hits = step.DamageEvents.Where(value => value.SkillItemId == 10012).ToArray();
            var scope = skill.HitGraphEntities.Single(value => value.EntityId == 10641).ScopeEntries;
            Check(hits.Length == 4 && hits.Select(value => value.TargetId).Distinct().Count() == 4,
                "The original wind strip did not hit all four enemies once");
            for (int distance = 1; distance <= 4; distance++)
            {
                var hit = hits.Single(value => value.TargetId == distance + 1);
                Check(hit.HitEntityId == 10641 && hit.DamageEntityId == 10642 && hit.ElementType == 100
                    && hit.Damage < 0 && Math.Abs(hit.Delay - cast.Delay - .6f - scope[distance - 1].Delay) < .0001f,
                    "The original multi-target hit or cell delay changed at distance " + distance);
            }
            var saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Replaying the four-target receipt repeated damage or movement");
            cases++;
        }
        return "Wind blade passed: " + cases + " four-direction/four-cell, multi-target, property-adjusted and blocked knockback, cold/replay cases; moved="
            + moved + ", no movement=" + resisted + ", sample up seed=" + firstUpMoveSeed + ".";
    }
}
