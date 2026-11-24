using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFireLeapHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Fire leap: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int count = 0;
        foreach (int rank in new[] { 4, 5 })
        foreach (int level in new[] { 1, 2, 10 })
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (string mode in new[] { "clear", "adjacent-target", "below-blocked", "intermediate-blocked", "only-side-open", "blocked-landings", "out-of-range" })
        {
            int distance = mode == "out-of-range" ? 3 : mode == "adjacent-target" ? 1 : 2;
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 78013,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula);
            var target = Unit(2, 2, 7 + direction[0] * distance, 7 + direction[1] * distance, formula);
            var targetBefore = new FightGridPoint(target.X, target.Y);
            var skill = catalog.CreateSkill(12100, 0, 10101, rank, level, 8,
                catalog.CalculateSkillProps(12100, rank, level, 8).Select(pair =>
                    new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            state.Units.AddRange(new[] { actor, target });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(target.Id); state.Waves.Add(wave);
            if (mode == "blocked-landings")
                foreach (var offset in new[] { new FightGridPoint(0, 1), new FightGridPoint(0, -1),
                    new FightGridPoint(1, 0), new FightGridPoint(-1, 0) })
                    state.Grid.BlockedPositions.Add(new FightGridPoint(target.X + offset.X, target.Y + offset.Y));
            if (mode == "below-blocked")
                state.Grid.BlockedPositions.Add(new FightGridPoint(target.X, target.Y - 1));
            if (mode == "intermediate-blocked")
                foreach (var offset in new[] { new FightGridPoint(0, 1), new FightGridPoint(0, -1),
                    new FightGridPoint(1, 0), new FightGridPoint(-1, 0) })
                    state.Grid.BlockedPositions.Add(new FightGridPoint(actor.X + offset.X, actor.Y + offset.Y));
            if (mode == "only-side-open")
                foreach (var offset in new[] { new FightGridPoint(0, 1), new FightGridPoint(0, -1), new FightGridPoint(-1, 0) })
                    state.Grid.BlockedPositions.Add(new FightGridPoint(target.X + offset.X, target.Y + offset.Y));
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            int initialLastRound = actor.SkillState.Slots[0].LastRound;
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(state.Sequence, state.Counter + 1).Event;
            restored.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "restoring changed the initial cooldown, landing, cultivated impact or random stream");
            if (mode == "blocked-landings" || mode == "out-of-range")
            {
                Check(!step.SkillCasts.Any(cast => cast.SkillItemId == 10101)
                    && !step.Movements.Any(move => move.MoveBehaviourId == 12103)
                    && actor.SkillState.Slots[0].ReleasedTimes == 0
                    && new FightSkillState(actor.SkillState).LeftCooldown(0) <= 0
                    && actor.SkillState.Slots[0].LastRound == initialLastRound && target.Hp == target.MaxHp,
                    $"an unavailable landing or target consumed the actual skill: mode={mode}, dir={direction[0]},{direction[1]}, initialTarget={targetBefore.X},{targetBefore.Y}, actor={actor.X},{actor.Y}, released={actor.SkillState.Slots[0].ReleasedTimes}, casts={step.SkillCasts.Count}, moves={step.Movements.Count}, hp={target.Hp}");
            }
            else
            {
                Check(step.SkillCasts.Count(value => value.SkillItemId == 10101) == 1,
                    $"expected actual jump: mode={mode}, dir={direction[0]},{direction[1]}, range={string.Join(";", skill.Range.Select(v => v.X + "," + v.Y))}, casts={step.SkillCasts.Count}, moves={step.Movements.Count}, released={actor.SkillState.Slots[0].ReleasedTimes}");
                var cast = step.SkillCasts.Single(value => value.SkillItemId == 10101);
                Check(step.Movements.Count(value => value.MoveBehaviourId == 12103 && value.UnitId == actor.Id) == 1,
                    $"missing source jump: mode={mode}, dir={direction[0]},{direction[1]}, actor={actor.X},{actor.Y}, moves={step.Movements.Count}");
                var move = step.Movements.Single(value => value.MoveBehaviourId == 12103 && value.UnitId == actor.Id);
                var hit = step.DamageEvents.Single(value => value.SkillItemId == 10101);
                Check(cast.SkillEntityId == 12100 && actor.SkillState.Slots[0].ReleasedTimes == 1
                    && move.Path.Count == 2 && move.Path[0].Equals(new FightGridPoint(7, 7))
                    && !state.Grid.BlockedPositions.Contains(move.Path.Last())
                    && move.Path.Last().X == actor.X && move.Path.Last().Y == actor.Y
                    && Math.Abs(actor.X - targetBefore.X) + Math.Abs(actor.Y - targetBefore.Y) == 1
                    && Math.Abs(move.Delay - cast.Delay - .45f) < .0001f
                    && Math.Abs(move.MoveDuration - .05f) < .0001f,
                    $"the actual first-turn skill did not land beside its target at the original time: rank={rank}, level={level}, dir={direction[0]},{direction[1]}, actor={actor.X},{actor.Y}, target={target.X},{target.Y}, released={actor.SkillState.Slots[0].ReleasedTimes}, cast={cast.Delay}, move={move.Delay}/{move.MoveDuration}, path={string.Join(";", move.Path.Select(point => point.X + "," + point.Y))}");
                Check(hit.SourceId == actor.Id && hit.TargetId == target.Id && hit.HitEntityId == 12102
                    && hit.DamageEntityId == 12104 && hit.DamageType == 1 && hit.ElementType == 102
                    && hit.Damage < 0 && !hit.IsCure && !hit.TrueDamage
                    && !hit.Dodge && !hit.Blinding && !hit.Immunity
                    && Math.Abs(hit.Delay - cast.Delay - .5f) < .0001f,
                    "the cultivated first-turn landing lost its original single fire impact");
                if (mode == "only-side-open") Check(actor.X == targetBefore.X + 1 && actor.Y == targetBefore.Y,
                    "the jump was still restricted to the target's lower cell");
                var push = step.Movements.SingleOrDefault(value => value.UnitId == target.Id);
                var destination = new FightGridPoint(targetBefore.X + Math.Sign(targetBefore.X - actor.X),
                    targetBefore.Y + Math.Sign(targetBefore.Y - actor.Y));
                if (state.Grid.BlockedPositions.Contains(destination))
                    Check(push == null, "the original outward impact crossed a blocked cell");
                else Check(push != null && push.MoveBehaviourId == 0 && push.Path.Count == 2
                    && push.Path[0].Equals(targetBefore) && push.Path[1].Equals(destination)
                    && Math.Abs(push.Delay - hit.Delay) < .0001f,
                    "the original damage configuration lost its one-cell outward displacement");
            }
            string settled = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(state) == settled,
                "replaying the receipt repeated a jump or impact");
            count++;
        }
        count += CheckBodyAndOccupancy(catalog, formula, items);
        return $"Fire leap passed: {count} cultivated direction, initial-use, blocked landing, range, restore and receipt replay cases.";
    }

    private static int CheckBodyAndOccupancy(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        string[] modes = { "diagonal", "large-target", "full-body-blocked", "occupied-landings",
            "intermediate-units", "blocked-first-target", "legacy-range" };
        foreach (string mode in modes)
        {
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 78013,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula);
            var target = Unit(2, 2, 7, 9, formula);
            if (mode == "diagonal") { target.X = 8; target.Y = 8; }
            if (mode == "large-target") {
                target.X = 10; target.Y = 7; target.BodyOffsets.Add(new FightGridPoint(-1, 0));
            }
            var skill = catalog.CreateSkill(12100, 0, 10101, 4, 2, 8,
                catalog.CalculateSkillProps(12100, 4, 2, 8).Select(pair =>
                    new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            state.Units.AddRange(new[] { actor, target });
            var offsets = new[] { new FightGridPoint(0, -1), new FightGridPoint(-1, 0),
                new FightGridPoint(1, 0), new FightGridPoint(0, 1) };
            if (mode == "full-body-blocked") {
                actor.BodyOffsets.Add(new FightGridPoint(1, 0));
                foreach (var offset in offsets.Skip(1)) state.Grid.BlockedPositions.Add(new FightGridPoint(target.X, target.Y) + offset);
                state.Grid.BlockedPositions.Add(new FightGridPoint(8, 8));
            }
            if (mode == "occupied-landings" || mode == "intermediate-units") {
                var center = mode == "occupied-landings" ? target : actor;
                foreach (var offset in offsets) {
                    var friend = Unit(state.Units.Count + 1, 1, center.X + offset.X, center.Y + offset.Y, formula);
                    state.Units.Add(friend);
                }
            }
            int expectedTarget = 2;
            if (mode == "blocked-first-target") {
                foreach (var offset in offsets) state.Grid.BlockedPositions.Add(new FightGridPoint(target.X, target.Y) + offset);
                target.Hp = target.Turn.Hp = target.MaxHp / 2;
                state.Units.Add(Unit(3, 2, 9, 7, formula)); expectedTarget = 3;
            }
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(state.Units.Where(unit => unit.Side == 2).Select(unit => unit.Id)); state.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            int initialLastRound = actor.SkillState.Slots[0].LastRound;
            Check(skill.Range.Count == 12 && skill.Range.Contains(new FightGridPoint(1, 1))
                && !skill.Range.Contains(new FightGridPoint(3, 0)), "the confirmed target range was not installed");
            string saved = FightBattleCheckpointSerializer.Save(state);
            var copy = FightBattleCheckpointSerializer.Load(saved);
            if (mode == "legacy-range") copy.Units[0].Skills[0].Range = new List<FightGridPoint> { new FightGridPoint(0, 3) };
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            Check(FightBattleCheckpointSerializer.Save(copy) == saved,
                "the legacy server range did not restore the confirmed local selector");
            var step = executor.Step(state.Sequence, state.Counter + 1).Event;
            restored.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "body or occupancy decisions changed after restore: " + mode);
            bool blocked = mode == "full-body-blocked" || mode == "occupied-landings";
            if (blocked) Check(step.SkillCasts.Count == 0 && actor.SkillState.Slots[0].ReleasedTimes == 0
                && new FightSkillState(actor.SkillState).LeftCooldown(0) <= 0
                    && actor.SkillState.Slots[0].LastRound == initialLastRound, "unavailable body landing consumed the skill: " + mode);
            else {
                var cast = step.SkillCasts.Single(value => value.SkillItemId == 10101);
                var move = step.Movements.Single(value => value.UnitId == 1 && value.MoveBehaviourId == 12103);
                Check(cast.TargetIds.SequenceEqual(new[] { expectedTarget }) && move.Path.Count == 2
                    && step.DamageEvents.Any(hit => hit.TargetId == expectedTarget && hit.ElementType == 102)
                    && !state.Units.Any(unit => unit.Id != actor.Id && FightBattleTargeting.IsSelectable(unit)
                        && unit.BodyOffsets.Any(offset => unit.X + offset.X == actor.X && unit.Y + offset.Y == actor.Y)),
                    "the standable jump target or direct landing changed: " + mode);
            }
        }
        return modes.Length;
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = side, Profession = 8,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", unit.Turn.Speed }, { "CritAvoidPercent", 10000 },
            { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
