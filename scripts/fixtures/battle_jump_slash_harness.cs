using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleJumpSlashHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Jump slash: " + message); }

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
        int cases = 0, applied = 0, resisted = 0;
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        foreach (int distance in new[] { 1, 2, 3, 4 })
        foreach (string mode in new[] { "baseline", "boosted", "suppressed" })
        foreach (int seed in Enumerable.Range(1, 8))
        {
            int dx = direction[0], dy = direction[1];
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = seed * 1000003 + 12345,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction[2];
            var target = Unit(2, 2, 7 + dx * distance, 7 + dy * distance, formula);
            if (mode == "boosted") actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
            if (mode == "suppressed") target.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = 20000 });
            var skill = catalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                catalog.CalculateSkillProps(10670, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10671);
            var application = hit.AppliedStatuses.Single(value => value.StatusId == 24113);
            Check(application.TargetType == 2 && application.AffectedByProp && Math.Abs(application.Chance - .5f) < .0001f
                && hit.DamageId == 10673 && hit.ScopeEntries.Count == 4 && skill.SkillTargetType == 2,
                "Original jump hit or armor-break configuration changed");
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            double chance = formula.CalculateStatusEffectRate(actor.Properties, target.Properties, application.Chance, 0);
            Check(mode == "baseline" ? chance > 0 && chance < 1 : mode == "boosted" ? chance > 1 : chance < 0,
                "Fixture lost the original effect-rate branches");
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resumed.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Cold restore changed the jump or status roll");
            var cast = step.SkillCasts.Single(value => value.SkillItemId == 10015);
            var move = step.Movements.Single(value => value.UnitId == actor.Id && value.MoveBehaviourId == 10672);
            var damage = step.DamageEvents.Single(value => value.SkillItemId == 10015);
            Check(move.Path.Count == 2 && move.Path[0].X == 7 && move.Path[0].Y == 7
                && move.Path[move.Path.Count - 1].X == actor.X && move.Path[move.Path.Count - 1].Y == actor.Y
                && (actor.X != target.X || actor.Y != target.Y)
                && skill.Range.Any(p => p.X == actor.X - 7 && p.Y == actor.Y - 7)
                && Math.Abs(move.Delay - cast.Delay - .1f) < .0001f && Math.Abs(move.MoveDuration - .2f) < .0001f,
                "Original 10672 jump differs: dir=" + dx + "," + dy + " distance=" + distance + " mode=" + mode + " actor=" + actor.X + "," + actor.Y + " target=" + target.X + "," + target.Y + " path=" + string.Join("/", move.Path.Select(p => p.X + "," + p.Y)) + " moveDelay=" + move.Delay + " castDelay=" + cast.Delay + " duration=" + move.MoveDuration);
            Check(cast.Presentation.TargetX == actor.X && cast.Presentation.TargetY == actor.Y
                && damage.TargetId == target.Id && damage.HitEntityId == 10671 && damage.DamageEntityId == 10673
                && damage.ElementType == 0 && !damage.TrueDamage && damage.Damage < 0
                && Math.Abs(damage.Delay - cast.Delay - .65f) < .0001f
                && Math.Abs(target.X - actor.X) + Math.Abs(target.Y - actor.Y) == 1,
                "Original area damage was not centered at the actual landing");
            var status = target.Statuses.SingleOrDefault(value => value.ClassId == 24113);
            if (status != null)
            {
                Check(mode != "suppressed" && status.CreatorId == actor.Id && status.ApplyingSkillItemId == 10015
                    && status.DurationRound == 2 && status.RoundTarget == 2 && status.RoundUpdateTiming == 1
                    && status.ResolvedProperties.Single(value => value.Id == 252).Value == -3000,
                    "Original armor-break source, duration or -30% defense changed");
                Check(step.StatusViewEvents.Any(value => value.Status.ClassId == 24113
                    && value.Kind == FightBattleStatusViewEventKind.Start && Math.Abs(value.Delay - damage.Delay) < .0001f),
                    "Original armor-break presentation did not start at the hit");
                applied++;
            }
            else { Check(mode != "boosted", "Guaranteed armor break did not apply"); resisted++; }
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated the jump, hit or status");
            cases++;
        }
        // The confirmed offline rule jumps directly across intervening units and blocked cells.
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 7, 11, formula);
            var ally = Unit(3, 1, 7, 8, formula);
            for (int x = 0; x < cp.Grid.Width; x++) cp.Grid.BlockedPositions.Add(new FightGridPoint(x, 9));
            var skill = catalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                catalog.CalculateSkillProps(10670, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target, ally }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(0, 1).Event; resumed.Step(0, 1);
            var move = step.Movements.Single(value => value.MoveBehaviourId == 10672 && value.UnitId == actor.Id);
            Check(move.Path.Count == 2 && move.Path[0].X == 7 && move.Path[0].Y == 7
                && move.Path[1].X == 7 && move.Path[1].Y == 10 && actor.X == 7 && actor.Y == 10
                && step.DamageEvents.Any(value => value.TargetId == target.Id && value.DamageEntityId == 10673),
                "The confirmed jump did not cross the intervening ally and blocked row directly to the available landing");
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                "Jump across obstacles changed after cold restore");
        }
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 7, 9, formula);
            var skill = catalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                catalog.CalculateSkillProps(10670, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            foreach (var offset in skill.Range)
            {
                var point = new FightGridPoint(actor.X + offset.X, actor.Y + offset.Y);
                if ((point.X != actor.X || point.Y != actor.Y) && (point.X != target.X || point.Y != target.Y))
                    cp.Grid.BlockedPositions.Add(point);
            }
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var step = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items).Step(0, 1).Event;
            Check(step.SkillCasts.All(value => value.SkillItemId != 10015) && actor.SkillState.Slots[0].ReleasedTimes == 0
                && !step.Movements.Any(value => value.MoveBehaviourId == 10672) && target.Hp == target.MaxHp,
                "A fully blocked set of landing cells must not consume or execute the jump");
        }
        // Natural expiry follows the hit target's personal rounds across save/reload.
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 7, 9, formula);
            target.Turn.Speed = 900; target.Properties.Single(value => value.Id == 4).Value = 900;
            actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
            var skill = catalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                catalog.CalculateSkillProps(10670, 2, 20, 1).Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            executor.Step(0, 1);
            int ended = 0;
            for (int i = 0; i < 8; i++)
            {
                var status = target.Statuses.SingleOrDefault(value => value.ClassId == 24113);
                Check((status == null) == (target.SkillState.Round >= 2), "Armor break expired on the wrong personal round");
                Check(target.Properties.Single(value => value.Id == 2).Value == (status == null ? 10 : 7),
                    "Armor break did not reduce defense by 30% or restore it on expiry");
                string before = FightBattleCheckpointSerializer.Save(cp);
                var restored = FightBattleCheckpointSerializer.Load(before);
                var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
                var step = executor.Step(cp.Sequence, cp.Counter + 1).Event;
                resumed.Step(restored.Sequence, restored.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(restored),
                    "Armor-break expiry diverged after reload");
                ended += step.StatusViewEvents.Count(value => value.Status.ClassId == 24113
                    && value.Kind == FightBattleStatusViewEventKind.End);
            }
            Check(ended == 1 && target.SkillState.Round >= 2, "Armor-break expiry emitted the wrong number of end events");
        }
        Check(applied > 0 && resisted > 0, "The original chance never produced both outcomes");
        return "Jump slash passed: " + cases + " direction, reach, position, property-adjusted status and cold/replay cases; applied="
            + applied + ", resisted=" + resisted;
    }
}
