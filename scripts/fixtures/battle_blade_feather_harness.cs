using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBladeFeatherHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Blade feather: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Check(items.Required(10595).PassiveStatusIds.SequenceEqual(new[] { 25096 }), "the original passive binding changed");
        int cases = 0, rolls = 0, generated = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (string mode in new[] { "normal", "forbidden", "no-child" })
        foreach (int seed in Enumerable.Range(1, mode == "normal" ? 48 : 8))
        {
            string originalState, restoredState;
            int original = Exercise(catalog, formula, items, rank, level, seed, mode, false, out originalState);
            int restored = Exercise(catalog, formula, items, rank, level, seed, mode, true, out restoredState);
            Check(original == restored && originalState == restoredState, "restoration changed the chance, children or counters");
            Check(original >= 0 && original <= (mode == "normal" ? 3 : 0), "a forbidden/nontriggering hit generated wind orbs");
            if (mode == "normal") { rolls += 3; generated += original; }
            cases++;
        }
        Check(generated > rolls * .3 && generated < rolls * .5, "the configured 40% chance became unconditional or stopped triggering: " + generated + "/" + rolls);
        foreach (int target in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }) VerifyTargets(catalog, formula, items, target);
        return "Blade feather passed: " + cases + " cultivated three-hit chance, forbidden, child-policy, attribution, before-end consumption and restoration cases; "
            + generated + "/" + rolls + " successful original 40% rolls; nine production status-target routes.";
    }

    private static int Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int seed, string mode, bool restore, out string finalState)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 8,
                Level = 70, SubRank = 8, X = 5, Y = id + 4, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            foreach (var pair in new[] { new[] { formula.PropertyId("Attack"), 1000 }, new[] { formula.PropertyId("Defence"), 10 },
                new[] { formula.PropertyId("MaxHp"), 1000000 }, new[] { formula.PropertyId("Speed"), id == 1 ? 1000 : 900 } })
                unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
            if (id == 1)
            {
                var skill = catalog.CreateSkill(11910, 0, 10088, 4, 2, 8, catalog.CalculateSkillProps(11910, 4, 2, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                Check(skill.CanTriggerChild, "the parent lost its original child policy");
                if (mode == "no-child") { skill.CanTriggerChild = false; skill.CanTriggerStatusEntityIds.Clear(); }
                unit.Skills.Add(skill); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var passive = catalog.CreateStatus(25096, 1, 1, 725096, 25096, 10595, rank, level, 8);
                Check(Math.Abs(passive.Components.HitSkill.Rate - .4f) < .000001f, "the original generation chance changed");
                if (mode == "forbidden") passive.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
                unit.Statuses.Add(passive);
            }
            state.Units.Add(unit);
        }
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        if (restore)
        {
            state = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            executor = FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
        }
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        Check(step.ActorId == 1 && step.DamageEvents.Count(hit => hit.SkillEntityId == 11910 && hit.Damage < 0) == 3,
            "the actual parent did not execute its three original wind hits: " + rank + "/" + level + "/" + seed + "/" + mode
                + " casts=" + string.Join(",",step.SkillCasts.Select(cast => cast.SkillEntityId))
                + " hits=" + string.Join(",",step.DamageEvents.Select(hit => hit.SkillEntityId + ":" + hit.Damage)));
        var orbs = state.Units[0].Statuses.Where(status => status.ClassId == 24385).ToArray();
        foreach (var orb in orbs)
            Check(orb.CreatorId == 1 && orb.OwnerId == 1 && orb.ApplyingSkillItemId == 10595 && orb.SourceSkillRank == rank
                && orb.SourceSkillLevel == level && orb.SourceSubRank == 8 && orb.StackCount == 0,
                "a generated orb lost its secret cultivation or remained after a successful before-end cast");
        int casts = step.SkillCasts.Count(cast => cast.SkillEntityId == 12030 && cast.SkillItemId == 10595);
        Check(step.DamageEvents.Count(hit => hit.SkillEntityId == 12030 && hit.ElementType == 100 && hit.Damage < 0) == casts,
            "the secret-generated children lost their original wind damage");
        finalState = FightBattleCheckpointSerializer.Save(state);
        Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == finalState,
            "receipt replay repeated chance rolls, children or consumed stacks");
        return casts;
    }

    private static void VerifyTargets(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int targetType)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 18, Height = 18, DefaultGroundType = 2 } };
        int[] xs = { 5, 5, 12, 13, 6, 11 }, ys = { 5, 6, 12, 13, 6, 11 };
        for (int id = 1; id <= 6; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2, CharacterType = 1, Profession = 8,
                Level = 70, SubRank = 8, X = xs[id - 1], Y = ys[id - 1],
                Hp = id == 6 ? 100000 : 1000000, MaxHp = 1000000, LastMaxHp = 1000000, MasterId = id == 2 || id == 3 ? 4 : 0 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
            unit.Turn.MasterId = unit.MasterId;
            foreach (var pair in new[] { new[] { formula.PropertyId("Attack"), 1000 }, new[] { formula.PropertyId("Defence"), 10 },
                new[] { formula.PropertyId("MaxHp"), 1000000 }, new[] { formula.PropertyId("Speed"), id == 1 ? 1000 : 100 } })
                unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
            if (id == 1)
            {
                var skill = catalog.CreateSkill(11910, 0, 10088, 4, 2, 8, catalog.CalculateSkillProps(11910, 4, 2, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                unit.Skills.Add(skill); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            }
            if (id == 2)
            {
                var passive = catalog.CreateStatus(25096, 2, 3, 725096, 25096, 10595, 4, 2, 8);
                passive.Components.HitSkill.HitSourceType = 0;
                passive.Components.HitSkill.Rate = 1;
                passive.Components.HitSkill.StatusTriggers.Single().ApplyTarget = targetType;
                unit.Statuses.Add(passive);
            }
            state.Units.Add(unit);
        }
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4, 5, 6 }); state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        Check(step.DamageEvents.Count(hit => hit.SkillEntityId == 11910 && hit.TargetId == 2 && hit.Damage < 0) == 3,
            "the controlled status-target hit did not execute");
        int expected = targetType == 1 ? 1 : targetType == 3 ? 3 : targetType == 4 ? 5
            : targetType == 7 ? 6 : targetType == 8 ? 4 : targetType == 6 ? 0 : 2;
        var applied = state.Units.SelectMany(unit => unit.Statuses.Where(status => status.ClassId == 24385)
            .Select(status => new { Unit = unit.Id, Status = status })).ToArray();
        Check(expected == 0 ? applied.Length == 0 : applied.Length == 1 && applied[0].Unit == expected
            && applied[0].Status.CreatorId == 3 && applied[0].Status.OwnerId == expected
            && applied[0].Status.StackCount == (expected == 1 ? 0 : 3),
            "the trigger enum was confused with lifetime targets: " + targetType + " expected=" + expected
                + " actual=" + string.Join(",", applied.Select(value => value.Unit)));
    }
}
