using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleResoluteArmorHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Resolute armor: " + message);
    }

    private static FightBattleUnitState Unit(int id, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
            Level = 50, SubRank = 1, X = 4 + id, Y = 4, Hp = hp, MaxHp = 1000, LastMaxHp = 1000,
            Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = id == 1 ? 1000 : 10 } };
        foreach (var entry in new Dictionary<int, long> { { 1, 100 }, { 2, 100 }, { 3, 1000 }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        foreach (long damage in new long[] { 499, 501, 1000 })
        {
            var actor = Unit(1, 1000, formula);
            var target = Unit(2, 1000, formula);
            var properties = target.Properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList();
            var passive = catalog.CreateStatus(25019, 2, 2, 1, 25019, 10539, 2, 20, 1, properties);
            Check(passive.HpLessThanPercentage == .5f && !passive.HpLessThanTryAtStart
                && !passive.HpLessThanInvokeOnDie && passive.HpLessThanMaxCount == -1,
                "configured threshold or survival condition differs");
            target.Statuses.Add(passive);
            var skill = new FightBattleSkillDefinition { EntityId = 990601, Slot = 0, TargetType = 1, ActionType = 2 };
            skill.Range.Add(new FightGridPoint(1, 0));
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990602, ActionType = 4,
                DamageType = 8, HasCustomDamage = true, CustomDamage = damage });
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 12, Height = 12 } };
            cp.Units.AddRange(new[] { actor, target });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(0, 1).Event; restored.Step(0, 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved),
                "checkpoint continuation differs");
            var hit = step.DamageEvents.Single(x => x.SkillEntityId == 990601);
            Check(hit.BeforeHp - hit.AfterHp == damage, "controlled damage did not settle");
            var shield = target.Statuses.SingleOrDefault(x => x.ClassId == 24211);
            bool expected = damage == 501;
            Check((shield != null) == expected && passive.HpLessThanTriggeredCount == (expected ? 1 : 0),
                "shield threshold or lethal exclusion differs for damage " + damage);
            if (expected)
            {
                var shieldProps = catalog.CalculateSkillProps(24211, 2, 20, 1)
                    .ToDictionary(value => value.Key, value => value.Value);
                long expectedShield = (long)formula.ReadProperty(shieldProps, formula.PropertyId("SkillFixedShield"))
                    + (long)(100 * formula.ReadProperty(shieldProps, formula.PropertyId("ShieldByDefence")));
                Check(shield.ShieldInitialized && shield.ShieldRemainValue > 0 && shield.ApplyingSkillItemId == 10539
                    && shield.CreatorId == 2 && shield.SourceSkillRank == 2 && shield.SourceSkillLevel == 20,
                    "shield lost its value, owner, passive source or cultivation");
                Check(shield.ShieldRemainValue == expectedShield, "shield did not use configured defence scaling");
            }
        }
        var low = Unit(3, 499, formula);
        low.Statuses.Add(catalog.CreateStatus(25019, 3, 3, 1, 25019, 10539, 2, 20, 1));
        FightStatusTriggerRuntime.Apply(low, catalog, true, units: new[] { low });
        Check(!low.Statuses.Any(x => x.ClassId == 24211), "battle-start exclusion ignored");
        return "Resolute armor passed: HP threshold, survival, source, shield and checkpoint.";
    }
}
