using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSoulBreakerHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Soul breaker: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Check(items.Required(10594).PassiveStatusIds.SequenceEqual(new[] { 25095 }), "the original passive binding changed");
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int missingHp in new[] { 0, 149999, 150000, 150001, 299999, 300000, 300001,
            449999, 450000, 450001, 599999, 600000, 600001, 749999, 750000, 750001, 900000 })
        foreach (bool forbidden in new[] { false, true })
        {
            string originalState, restoredState;
            long original = Exercise(catalog, formula, items, rank, level, missingHp, forbidden, false, out originalState);
            long restored = Exercise(catalog, formula, items, rank, level, missingHp, forbidden, true, out restoredState);
            long perStack = catalog.CalculateSkillProps(25095, rank, level, 8)[formula.PropertyId("StatusDmgAddPer")];
            long fixedPerStack = catalog.CalculateSkillProps(25095, rank, level, 8)[formula.PropertyId("FixedStatusDmgAdd")];
            int stacks = forbidden ? 0 : Math.Min(5, missingHp / 150000);
            long expected = -(long)((1000 * (1000d / 1010d) + fixedPerStack * stacks) * (1 + perStack * stacks / 10000d));
            Check(original == expected, "actual cultivated damage changed at the target HP boundary: "
                + rank + "/" + level + "/" + missingHp + "/" + forbidden + " actual=" + original + " expected=" + expected);
            Check(restored == original && restoredState == originalState, "restoration changed damage, status or counters");
            cases++;
        }
        return "Soul breaker passed: " + cases + " cultivated actual damage, exact 15% boundaries, five-stack cap, forbidden and restoration cases.";
    }

    private static long Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int missingHp, bool forbidden, bool restore, out string finalState)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 8,
                Level = 70, SubRank = 8, X = 5, Y = id + 4, Hp = 1000000 - (id == 2 ? missingHp : 0),
                MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            foreach (var pair in new[] { new[] { formula.PropertyId("Attack"), 1000 }, new[] { formula.PropertyId("Defence"), 10 },
                new[] { formula.PropertyId("MaxHp"), 1000000 }, new[] { formula.PropertyId("Speed"), id == 1 ? 1000 : 900 } })
                unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
            if (id == 1)
            {
                var skill = new FightBattleSkillDefinition { EntityId = 990940, ItemId = 10088, Slot = 0, TargetType = 1,
                    SkillType = 0, ActionType = 2, ActionDuration = 1, Duration = 1 };
                skill.Range.Add(new FightGridPoint(0, 1));
                skill.Properties.Add(new FightPropertyValue { Id = 200, Value = 10000 });
                skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990941, DamageId = 10005,
                    DamageType = 1, BasicProperty = 1, DamageProperty = 200, FixedDamageProperty = 190, ActionType = 4, Delay = .1f });
                unit.Skills.Add(skill); unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
                var status = catalog.CreateStatus(25095, 1, 1, 725095, 25095, 10594, rank, level, 8);
                Check(status.Components.HitApplyDamage.ScaleByHpType == 5
                    && status.Components.HitApplyDamage.MaxHpScale == 5 && status.DurationRound == -1,
                    "the original target HP condition or limit changed");
                if (forbidden) status.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
                unit.Statuses.Add(status);
            }
            state.Units.Add(unit);
        }
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
        long damage = 0;
        var settlement = new FightDamageSettlement();
        settlement.SourceHit += hit => { if (hit.SourceId == 1 && hit.TargetId == 2) damage = hit.DamageValue; };
        var executor = FightBattleExecutor.Begin(state, formula, settlement, catalog, items);
        if (restore)
        {
            state = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            executor = FightBattleExecutor.Restore(state, formula, settlement, catalog, items);
        }
        var result = executor.Step(state.Sequence, state.Counter + 1);
        Check(result.Event.ActorId == 1 && result.Event.DamageEvents.Count == 1 && damage < 0, "the actual controlled hit was not executed");
        finalState = FightBattleCheckpointSerializer.Save(state);
        Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == finalState,
            "receipt replay repeated damage or consumed the permanent passive");
        return damage;
    }
}
