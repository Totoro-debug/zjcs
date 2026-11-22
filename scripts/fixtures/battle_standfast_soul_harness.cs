using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStandfastSoulHarness
{
    private sealed class Observed { public int Source; public long Damage; public bool Protected; public long Reduction, FixedReduction; }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Standfast soul: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        var item = items.Required(10608);
        Check(item.PassiveStatusIds.SequenceEqual(new[] { 25117 }), "the original passive binding changed");
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (string mode in new[] { "normal", "forbidden", "foreign-source" })
        {
            string baselineState, restoredState;
            var baseline = Exercise(catalog, formula, items, rank, level, "none", false, out baselineState);
            var actual = Exercise(catalog, formula, items, rank, level, mode, false, out baselineState);
            var restored = Exercise(catalog, formula, items, rank, level, mode, true, out restoredState);
            Check(baselineState == restoredState && actual.Count == restored.Count, "checkpoint restoration changed the battle");
            Check(actual.Count == 8 && baseline.Count == actual.Count, "the controlled self/hostile hits were not executed");
            for (int i = 0; i < actual.Count; i++)
            {
                var value = actual[i];
                bool protectedTurn = value.Source == 1 && mode != "forbidden";
                Check(value.Protected == protectedTurn, "the status escaped its owner's turn or failed during that turn");
                // The original formula subtracts FixedStatusDmgReduce before
                // dividing by (1 + StatusDmgReducePer), then truncates once.
                long expected = protectedTurn ? -(long)((1000 * (1000d / 1010d) - value.FixedReduction)
                    / (1 + value.Reduction / 10000d)) : baseline[i].Damage;
                Check(value.Damage == expected && value.Damage < 0, "the original cultivated reduction changed actual damage: "
                    + rank + "/" + level + "/" + mode + "/" + i + " actual=" + value.Damage + " expected=" + expected
                    + " baseline=" + baseline[i].Damage + " reduction=" + value.Reduction);
                Check(value.Source == restored[i].Source && value.Damage == restored[i].Damage
                    && value.Protected == restored[i].Protected && value.Reduction == restored[i].Reduction
                    && value.FixedReduction == restored[i].FixedReduction,
                    "restoration changed protection at the damage boundary");
            }
            cases++;
        }
        return "Standfast soul passed: " + cases + " cultivation, owner-turn damage, other-turn exclusion, forbidden, foreign creator, expiry and restoration cases.";
    }

    private static List<Observed> Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, string mode, bool restore, out string finalState)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 6, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 8,
                Level = 70, SubRank = 8, X = 5, Y = id + 4, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            foreach (var pair in new[] { new[] { formula.PropertyId("Attack"), 1000 }, new[] { formula.PropertyId("Defence"), 10 },
                new[] { formula.PropertyId("MaxHp"), 1000000 }, new[] { formula.PropertyId("Speed"), id == 1 ? 1000 : 900 } })
                unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
            var skill = new FightBattleSkillDefinition { EntityId = 990610 + id, Slot = 0, TargetType = id == 1 ? 0 : 1,
                SkillType = 0, ActionType = 2, ActionDuration = 1, Duration = 1 };
            skill.Range.Add(new FightGridPoint(0, id == 1 ? 0 : -1));
            skill.Properties.Add(new FightPropertyValue { Id = 200, Value = 10000 });
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990620 + id, DamageId = 10005,
                DamageType = 1, BasicProperty = 1, DamageProperty = 200, FixedDamageProperty = 190, ActionType = 4, Delay = .1f });
            unit.Skills.Add(skill); unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            state.Units.Add(unit);
        }
        var owner = state.Units[0];
        if (mode != "none")
        {
            var passive = catalog.CreateStatus(25117, 1, mode == "foreign-source" ? 2 : 1,
                725117, 25117, 10608, rank, level, 8);
            if (mode == "forbidden") passive.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
            owner.Statuses.Add(passive);
        }
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
        var observations = new List<Observed>();
        var settlement = new FightDamageSettlement();
        settlement.SourceHit += hit => {
            if (hit.TargetId != 1) return;
            var status = owner.Statuses.SingleOrDefault(value => value.ClassId == 24392);
            if (status != null)
                Check(status.OwnerId == 1 && status.CreatorId == (mode == "foreign-source" ? 2 : 1) && status.SourceSkillRank == rank
                    && status.SourceSkillLevel == level && status.SourceSubRank == 8
                    && status.ApplyingSkillItemId == 10608 && status.DurationRound == 0 && !status.CanDispersed,
                    "the turn-bound status lost its source, cultivation or duration: " + mode + " owner=" + status.OwnerId
                    + " creator=" + status.CreatorId + " rank=" + status.SourceSkillRank + " level=" + status.SourceSkillLevel
                    + " subrank=" + status.SourceSubRank + " item=" + status.ApplyingSkillItemId
                    + " duration=" + status.DurationRound + " dispersible=" + status.CanDispersed);
            observations.Add(new Observed { Source = hit.SourceId, Damage = hit.DamageValue, Protected = status != null,
                Reduction = owner.Properties.FirstOrDefault(value => value.Id == formula.PropertyId("StatusDmgReducePer"))?.Value ?? 0,
                FixedReduction = owner.Properties.FirstOrDefault(value => value.Id == formula.PropertyId("FixedStatusDmgReduce"))?.Value ?? 0 });
        };
        var executor = FightBattleExecutor.Begin(state, formula, settlement, catalog, items);
        for (int i = 0; i < 8; i++)
        {
            var step = executor.Step(state.Sequence, state.Counter + 1).Event;
            Check(step.ActorId == (i % 2 == 0 ? 1 : 2), "the controlled turn order changed");
            Check(!owner.Statuses.Any(value => value.ClassId == 24392), "zero-round protection survived its owner's turn end");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
                "receipt replay repeated damage or protection");
            if (restore)
            {
                state = FightBattleCheckpointSerializer.Load(saved); owner = state.Units[0];
                executor = FightBattleExecutor.Restore(state, formula, settlement, catalog, items);
            }
        }
        finalState = FightBattleCheckpointSerializer.Save(state);
        return observations;
    }
}
