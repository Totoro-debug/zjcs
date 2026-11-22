using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleAssaultPiercingHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Assault piercing: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Check(items.Required(10568).PassiveStatusIds.SequenceEqual(new[] { 25081 }), "the original passive binding changed");
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int buffCount in new[] { 0, 1, 2, 3, 4, 5 })
        foreach (bool forbidden in new[] { false, true })
        {
            string originalState, restoredState;
            long original = Exercise(catalog, formula, items, rank, level, buffCount, forbidden, false, out originalState);
            long restored = Exercise(catalog, formula, items, rank, level, buffCount, forbidden, true, out restoredState);
            var properties = catalog.CalculateSkillProps(25081, rank, level, 8);
            double scale = forbidden ? 0 : 1 + Math.Min(3, buffCount) * (double).1f;
            long percent = (long)(properties[formula.PropertyId("StatusIgnoreDefence")] * scale);
            long fixedValue = (long)(properties[formula.PropertyId("FixedStatusIgnoreDefence")] * scale);
            double defence = Math.Max(0, 1000 - 1000 * percent / 10000d - fixedValue);
            long expected = -(long)(1000 * (1000 / (1000 + defence)));
            Check(original == expected, "actual cultivated defence ignore changed with owner buffs: "
                + rank + "/" + level + "/" + buffCount + "/" + forbidden + " actual=" + original + " expected=" + expected);
            Check(restored == original && restoredState == originalState, "restoration changed damage, status or counters");
            cases++;
        }
        return "Assault piercing passed: " + cases + " cultivated actual defence ignore, owner buff counts, three-count cap, stack/excluded-status filtering, forbidden and restoration cases.";
    }

    private static long Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int buffCount, bool forbidden, bool restore, out string finalState)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 8,
                Level = 70, SubRank = 8, X = 5, Y = id + 4, Hp = 1000000,
                MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            foreach (var pair in new[] { new[] { formula.PropertyId("Attack"), 1000 }, new[] { formula.PropertyId("Defence"), 1000 },
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
                var status = catalog.CreateStatus(25081, 1, 1, 725081, 25081, 10568, rank, level, 8);
                Check(status.Components.HitDmgAddPer.ScalePropByStatusTypeCount
                    && status.Components.HitDmgAddPer.StatusFilter.MaxStatusCount == 3 && status.DurationRound == -1,
                    "the original owner buff condition or limit changed");
                for (int i = 0; i < buffCount; i++)
                    unit.Statuses.Add(new FightBattleStatusState { InstanceId = 991000 + i, ClassId = 991000,
                        OwnerId = 1, CreatorId = 1, StatusType = 1, DurationRound = -1, StackCount = 20 });
                foreach (int excludedType in new[] { 0, 2, 3 })
                    unit.Statuses.Add(new FightBattleStatusState { InstanceId = 992000 + excludedType, ClassId = 992000 + excludedType,
                        OwnerId = 1, CreatorId = 1, StatusType = excludedType, DurationRound = -1, StackCount = 1 });
                unit.Statuses.Add(new FightBattleStatusState { InstanceId = 993000, ClassId = 993000, OwnerId = 1,
                    CreatorId = 1, StatusType = 1, DurationRound = -1, StackCount = 1, ForbiddenType = FightStatusLifecycle.ForbiddenMutex });
                if (forbidden) status.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
                unit.Statuses.Add(status);
            }
            if (id == 2) unit.Statuses.Add(new FightBattleStatusState { InstanceId = 994000, ClassId = 994000,
                OwnerId = 2, CreatorId = 2, StatusType = 1, DurationRound = -1, StackCount = 1 });
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
