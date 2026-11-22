using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleProtectiveRunesHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Protective runes: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int sourceType in new[] { 1, 2 })
        foreach (int skillType in new[] { 0, 1 })
        foreach (int damageType in new[] { 1, 8 })
        foreach (bool shielded in new[] { false, true })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 37,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 1, 10, formula);
            var enemy = Unit(2, 2, sourceType, 11, formula);
            var active = catalog.CreateSkill(12110, 0, 10102, rank, level, 8,
                catalog.CalculateSkillProps(12110, rank, level, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            actor.Skills.Add(active); var ownSlot = catalog.CreateSkillSlot(active, 0); ownSlot.LimitedTimes = 1;
            actor.SkillState.Slots.Add(ownSlot);
            if (shielded) actor.Statuses.Add(new FightBattleStatusState { ClassId = 99001, InstanceId = 99001,
                OwnerId = 1, CreatorId = 1, DurationRound = 99, CurDurationRound = 99, StackCount = 1,
                ShieldKind = FightBattleShieldKind.Persistent, ShieldInitialized = true, ShieldRemainValue = 2000000 });
            var incoming = new FightBattleSkillDefinition { EntityId = 99010, Slot = 0, SkillType = skillType,
                ActionType = 2, TargetType = 1, ElementType = 102,
                Properties = new List<FightPropertyValue> { new FightPropertyValue { Id = 200, Value = 10000 } } };
            incoming.Range.Add(new FightGridPoint(-1, 0));
            var hit = new FightBattleHitDefinition { EntityId = 99011, DamageId = 99012,
                ActionType = 4, DamageType = damageType, BasicProperty = 1, DamageProperty = 200,
                HasCustomDamage = damageType == 8, CustomDamage = 1000000 };
            hit.Scope.Add(new FightGridPoint(0, 0)); incoming.Hits.Add(hit);
            enemy.Skills.Add(incoming); enemy.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
            cp.Units.AddRange(new[] { actor, enemy }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var cast = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            Check(cast.ActorId == 1 && cast.SkillCasts.Any(value => value.SkillEntityId == 12110), "initial cooldown did not allow the original self buff");
            var status = actor.Statuses.Single(value => value.ClassId == 24201);
            Check(status.DurationRound == 3 && status.RoundTarget == 2 && status.RoundUpdateTiming == 1
                && status.DamageReducePer.Supported && status.CanDispersed, "the original three-round positive status changed");
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(saved.Sequence, saved.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved), "conditional reduction or shield diverged after restore");
            var damage = step.DamageEvents.Single(value => value.SkillEntityId == 99010);
            var props = catalog.CalculateSkillProps(24201, rank, level, 8);
            double factor = skillType == 0 ? 1.0 : (double)1.2f;
            double percent = (long)(props[232] * factor) / 10000.0;
            double fixedValue = (long)(props[236] * factor);
            long expected = (long)(Math.Max(0.0, 1000000 - fixedValue) / (1.0 + percent));
            Check(shielded ? damage.Damage == 0 && damage.ShieldDamage == expected : damage.Damage == -expected,
                "cultivated fixed/percentage reduction or the extra 20% did not precede shields: "
                    + rank + "/" + level + "/" + skillType + "/" + damageType + ": " + damage.Damage + "/" + damage.ShieldDamage + " expected -" + expected);
            string after = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == after,
                "replay consumed the conditional reduction twice");
            for (int i = 0; i < 10 && !cp.Finished; i++) runner.Step(cp.Sequence, cp.Counter + 1);
            Check(actor.Statuses.All(value => value.ClassId != 24201), "the three-round self protection did not expire");
            cases++;
        }
        cases += SharedComponents(catalog, formula, items);
        return "Protective runes passed: " + cases + " cultivation, direct/other damage, fixed-first reduction, extra 20%, shields, conditional ownership, expiry, replay and restore cases.";
    }

    static int SharedComponents(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int id in new[] { 24033, 26100, 26004 })
        foreach (int rank in new[] { 1, 7 })
        foreach (int level in new[] { 1, 2 })
        {
            var target = Unit(1, 1, 1, 10, formula); var source = Unit(2, 2, 2, 11, formula);
            source.Statuses.Add(new FightBattleStatusState { ClassId = 99031, InstanceId = 99031,
                OwnerId = 2, CreatorId = 2, ActionType = 64, IsOpenStack = true, StackCount = 2 });
            source.Statuses.Add(new FightBattleStatusState { ClassId = 99032, InstanceId = 99032,
                OwnerId = 2, CreatorId = 2, ActionType = 64, IsOpenStack = true, StackCount = 3 });
            source.Statuses.Add(new FightBattleStatusState { ClassId = 99033, InstanceId = 99033,
                OwnerId = 2, CreatorId = 2, ActionType = 99, IsOpenStack = true, StackCount = 999 });
            var status = catalog.CreateStatus(id, 1, 1, id, 0, 0, rank, level, 8);
            target.Statuses.Add(status);
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 37, MaxRound = 8 };
            cp.Units.AddRange(new[] { target, source });
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var skill = new FightBattleSkillDefinition { SkillType = 0, ActionType = 2, TargetType = 1 };
            var actual = FightBattleStatusDamageReducePerRuntime.Resolve(source, target, skill,
                new FightDamageTypeData(), false, new FightRandomStream(37), formula);
            var props = catalog.CalculateSkillProps(id, rank, level, 8);
            double scale = id == 24033 ? 1.0 + (double)0.2f * 5 : 1.0;
            long rawPercent = props.TryGetValue(232, out long p) ? p : 0;
            long rawFixed = props.TryGetValue(236, out long f) ? f : 0;
            Check(actual.Percent == (long)(rawPercent * scale) / 10000.0
                && actual.Fixed == (long)(rawFixed * scale),
                "a shared component ignored its cultivated pair, source status stacks or absent reduction properties: " + id);
            Check((target.Properties.FirstOrDefault(value => value.Id == 232)?.Value ?? 0) == 0
                && (target.Properties.FirstOrDefault(value => value.Id == 236)?.Value ?? 0) == 0,
                "a conditional pair also entered unconditional unit properties: " + id);
            if (id == 26100)
            {
                // Probe property ownership independently of this affix's rank tables.
                status.ResolvedProperties = new List<FightPropertyValue> {
                    new FightPropertyValue { Id = 232, Value = rawPercent },
                    new FightPropertyValue { Id = 236, Value = rawFixed },
                    new FightPropertyValue { Id = 1, Value = 1234 } };
                FightStatusPropertyRuntime.RebuildAll(cp.Units, catalog, formula);
                Check(target.Properties.Single(value => value.Id == 1).Value == 1001234,
                    "excluding the conditional pair removed an unrelated status property");
            }
            int triggered = status.DamageReducePer.TriggeredCount;
            status.ForbiddenType = 1;
            var disabled = FightBattleStatusDamageReducePerRuntime.Resolve(source, target, skill,
                new FightDamageTypeData(), false, new FightRandomStream(37), formula);
            Check(disabled.Percent == 0 && disabled.Fixed == 0 && status.DamageReducePer.TriggeredCount == triggered,
                "a forbidden conditional component changed damage or consumed its trigger");
            cases++;
        }
        const long precise = 9007199254740993L;
        Check(default(FightBattleDamageReduction).Apply(precise) == precise,
            "a missing conditional effect rounded an unchanged large integer");
        Check(new FightBattleDamageReduction { Fixed = 20, Percent = .2 }.Apply(-10) == 0,
            "a fixed reduction larger than damage created healing");
        return cases;
    }

    static FightBattleUnitState Unit(int id, int side, int type, int x, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = type, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = 10, Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 999;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000000 }, { "MaxHp", 1000000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
