using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWindOrbHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Wind orb: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int count in new[] { 0, 1, 3, 10 })
        foreach (string mode in new[] { "normal", "forbidden", "outside" })
        {
            string originalState, restoredState;
            int original = Exercise(catalog, formula, items, rank, level, count, mode, false, out originalState);
            int restored = Exercise(catalog, formula, items, rank, level, count, mode, true, out restoredState);
            Check(original == (mode == "normal" ? count : 0) && restored == original && originalState == restoredState,
                "actual casts, failure retention or restoration changed: " + rank + "/" + level + "/" + count + "/" + mode
                    + " original=" + original + " restored=" + restored + " sameState=" + (originalState == restoredState));
            cases++;
        }
        return "Wind orb passed: " + cases + " cultivated before-end casts, successful stack consumption, 0.1 second intervals, body deduplication, forbidden, out-of-range retention and restoration cases.";
    }

    private static int Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int count, string mode, bool restore, out string finalState)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 8,
                Level = 70, SubRank = 8, X = id == 2 && mode == "outside" ? 13 : 5,
                Y = id == 2 && mode == "outside" ? 13 : id == 1 ? 5 : 7,
                Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            foreach (var pair in new[] { new[] { formula.PropertyId("Attack"), 1000 }, new[] { formula.PropertyId("Defence"), 10 },
                new[] { formula.PropertyId("MaxHp"), 1000000 }, new[] { formula.PropertyId("Speed"), id == 1 ? 1000 : 900 } })
                unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
            if (id == 1)
            {
                var status = catalog.CreateStatus(24385, 1, 1, 724385, 25096, 10595, rank, level, 8);
                Check(status.Components.RoundBeforeEnd.RulesSupported && status.Components.RoundBeforeEnd.Skills.Count == 10,
                    "the original before-end definition was not resolved");
                status.StackCount = count;
                if (mode == "forbidden") status.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
                unit.Statuses.Add(status);
            }
            else unit.BodyOffsets.Add(new FightGridPoint(1, 0));
            state.Units.Add(unit);
        }
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        if (restore)
        {
            state = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            if (count == 1) state.Units[0].Statuses[0].Components = null;
            else state.Units[0].Statuses[0].Components.RoundBeforeEnd = null;
            executor = FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
        }
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        foreach (var view in step.StatusViewEvents.Where(value => value.Status.ClassId == 24385))
            Check(view.Status.HasSharedStackCount && view.Status.SharedStackCount == view.Status.StackCount,
                "the native display did not receive the shared total at this exact transition");
        var casts = step.SkillCasts.Where(cast => cast.SkillEntityId == 12030).ToArray();
        var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 12030).ToArray();
        var attached = step.SkillCasts.Where(cast => cast.SkillEntityId == 13170).ToArray();
        Check(attached.Length == casts.Length && attached.All(cast => cast.ActorId == 1 && cast.SkillItemId == 10595
            && cast.TargetIds.SequenceEqual(new[] { 1 })), "the original attached self child was lost");
        Check(step.ActorId == 1 && hits.Length == casts.Length && hits.All(hit => hit.SourceId == 1 && hit.TargetId == 2
            && hit.SkillItemId == 10595 && hit.HitEntityId == 12031 && hit.DamageEntityId == 12032
            && hit.ElementType == 100 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure),
            "the child lost its cultivated source or original wind damage");
        var cultivated = catalog.CalculateSkillProps(12030, rank, level, 8);
        long expectedDamage = (long)((1000m * cultivated[200] / 10000m + cultivated[190]) * 1000m / 1010m);
        Check(hits.All(hit => hit.Damage == -expectedDamage),
            "the original fixed-plus-percentage wind value changed: " + rank + "/" + level + " expected=" + expectedDamage
                + " actual=" + string.Join(",", hits.Select(hit => hit.Damage)));
        for (int i = 1; i < casts.Length; i++)
            Check(Math.Abs(casts[i].Delay - casts[i - 1].Delay - .1f) < .0001f, "the original trigger interval changed");
        Check(state.Units[0].Statuses.Single(status => status.ClassId == 24385).StackCount == count - casts.Length,
            "the original successful invocation did not consume exactly one stack");
        finalState = FightBattleCheckpointSerializer.Save(state);
        Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == finalState,
            "receipt replay repeated wind damage or stack consumption");
        return casts.Length;
    }
}
