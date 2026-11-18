using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattlePullSkillHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Pull: " + message); }

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
        foreach (int distance in new[] { 2, 3, 4, 5, 6, 7 })
        foreach (bool blocked in new[] { false, true })
        {
            int dx=direction[0], dy=direction[1], tx=7+dx*distance, ty=7+dy*distance;
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction[2];
            var target = Unit(2, 2, tx, ty, formula);
            if (blocked) foreach (var offset in new[] { new[] {-1,0}, new[] {1,0}, new[] {0,-1}, new[] {0,1} })
                cp.Grid.BlockedPositions.Add(new FightGridPoint(tx+offset[0],ty+offset[1]));
            var skill = catalog.CreateSkill(10700, 0, 10018, 2, 20, 1,
                catalog.CalculateSkillProps(10700, 2, 20, 1).Select(value => new FightPropertyValue { Id=value.Key, Value=value.Value }).ToList());
            var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10701);
            Check(skill.SkillTargetType == 0 && skill.ElementType == 100 && hit.DamageId == 10702
                && hit.ScopeEntries.Count == 9 && hit.HitMove != null && hit.HitMove.MoveBehaviourId == 10703
                && hit.HitMove.Distance == -1 && hit.HitMove.TowardsTargetPos && Math.Abs(hit.HitMove.Delay - .35f) < .0001f,
                "Original pull damage or movement configuration changed");
            skill.LimitedTimes=1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[] { actor,target }); var wave=new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event; resumed.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored), "Cold restore changed hit or pull");
            var cast=step.SkillCasts.Single(value => value.SkillItemId == 10018);
            var damage=step.DamageEvents.Single(value => value.SkillItemId == 10018);
            Check(cast.SkillEntityId==10700 && cast.Presentation.TargetX==tx && cast.Presentation.TargetY==ty
                && damage.TargetId==target.Id && damage.HitEntityId==10701 && damage.DamageEntityId==10702
                && damage.ElementType==100 && !damage.TrueDamage && damage.Damage<0
                && Math.Abs(damage.Delay-cast.Delay-.15f)<.0001f, "Original wind damage, target point or timing changed");
            var moves=step.Movements.Where(value => value.UnitId==target.Id && value.MoveBehaviourId==10703).ToArray();
            if (blocked) Check(moves.Length==0 && target.X==tx && target.Y==ty, "Pull moved an enclosed target");
            else
            {
                Check(moves.Length==1 && moves[0].Path.Count>=2
                    && Math.Abs(target.X-actor.X)+Math.Abs(target.Y-actor.Y)==1
                    && moves[0].Path[0].X==tx && moves[0].Path[0].Y==ty
                    && moves[0].Path[moves[0].Path.Count-1].X==target.X && moves[0].Path[moves[0].Path.Count-1].Y==target.Y
                    && Math.Abs(moves[0].Delay-damage.Delay-.35f)<.0001f && Math.Abs(moves[0].MoveDuration-.15f)<.0001f,
                    "Original pull failed to reach the nearest available tile after the actual hit");
            }
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed && FightBattleCheckpointSerializer.Save(cp)==saved, "Receipt replay repeated hit or pull");
            cases++;
        }
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        {
            int dx = direction[0], dy = direction[1], tx = 10 + 3 * dx, ty = 10 + 3 * dy;
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, 10, formula); actor.Direction = direction[2];
            var skill = catalog.CreateSkill(10700, 0, 10018, 2, 20, 1,
                catalog.CalculateSkillProps(10700, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.Add(actor); int next = 2;
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                cp.Units.Add(Unit(next++, 2, tx + x, ty + y, formula));
            cp.Units.Add(Unit(next, 2, tx + 3 * dy, ty + 3 * dx, formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(value => value.Side == 2).Select(value => value.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resumed.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Cold restore changed multi-target pulls");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10018);
            var hits = step.DamageEvents.Where(value => value.SkillItemId == 10018).ToArray();
            Check(cast.Presentation.TargetX == tx && cast.Presentation.TargetY == ty && hits.Length == 9
                && hits.Select(value => value.TargetId).Distinct().Count() == 9
                && hits.All(value => value.TargetId != next && Math.Abs(value.Delay - cast.Delay - .15f) < .0001f),
                "Original square did not damage each of nine targets exactly once");
            foreach (var move in step.Movements.Where(value => value.MoveBehaviourId == 10703))
                Check(hits.Any(value => value.TargetId == move.UnitId && Math.Abs(move.Delay - value.Delay - .35f) < .0001f),
                    "A multi-target pull did not follow its own damage occurrence");
            cases++;
        }
        return "Pull passed: " + cases + " four-direction, range, nine-target, blocked path, authored timing and cold/replay cases.";
    }
}
