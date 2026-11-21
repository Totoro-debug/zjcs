using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleColdGlintHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Cold glint: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        var passive = items.Required(10606);
        Check(passive.PassiveStatusIds.SequenceEqual(new[] { 25115 }), "original passive status changed");
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (int item in new[] { 10047, 10084 })
        foreach (int resistance in new[] { -1, 0, 1 })
        foreach (string mode in new[] { "normal", "shield", "dodge", "blind", "forbidden", "cap" })
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        {
            var actor = Unit(1, 6, 6, formula);
            var target = Unit(2, 6 + direction[0], 6 + direction[1], formula);
            target.BodyOffsets.Add(new FightGridPoint(-direction[1], direction[0]));
            var ally = Unit(3, 6 - direction[1], 6 + direction[0], formula); ally.Side = 1;
            Set(target, formula, "EffectDodgeValue", resistance * 100000);
            if (mode == "dodge") Set(target, formula, "DodgePercent", 10000);
            if (mode == "blind") Set(actor, formula, "BlindingPercent", 10000);
            if (mode == "shield") target.Statuses.Add(catalog.CreateStatus(24377, 2, 2, 724377, customShieldValue: 100000000));
            if (mode == "cap")
            {
                var cold = catalog.CreateStatus(24266, 2, 1, 724266, 25115, 10606, rank, level, 8);
                cold.StackCount = 9; target.Statuses.Add(cold);
            }
            var bonus = catalog.CalculatePassiveSkillProps(passive.PassiveRankPropId,
                passive.PassiveGroupLevelPropId, passive.PassiveRankFactorId,
                passive.PassivePropFactors, rank, level, 8);
            int affinity = formula.PropertyId("WaterDamageAdd");
            Check(bonus.TryGetValue(affinity, out long affinityValue) && affinityValue > 0,
                "the original water-affinity contribution is missing");
            foreach (var value in bonus) actor.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
            var listener = catalog.CreateStatus(25115, 1, 1, 725115, 25115, 10606, rank, level, 8);
            actor.Statuses.Add(listener);
            int entity = item == 10047 ? 10880 : 11890;
            var skill = catalog.CreateSkill(entity, 0, item, 4, 2, 8,
                catalog.CalculateSkillProps(entity, 4, 2, 8)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            state.Units.AddRange(new[] { actor, target, ally });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            if (mode == "forbidden") FightStatusLifecycle.SetForbidden(actor, listener, FightStatusLifecycle.ForbiddenStatusTrigger, true);
            string initial = FightBattleCheckpointSerializer.Save(state);
            var copy = FightBattleCheckpointSerializer.Load(initial);
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var control = FightBattleCheckpointSerializer.Load(initial);
            var plainActor = control.Units.Single(unit => unit.Id == 1);
            plainActor.Statuses.RemoveAll(status => status.ClassId == 25115);
            plainActor.BaseProperties.RemoveAll(property => bonus.ContainsKey(property.Id));
            var plain = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null, baseline = null;
            while (state.Counter < 8 && !state.Finished)
            {
                step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                baseline = plain.Step(control.Sequence, control.Counter + 1).Event;
                Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                    "restoration changed water affinity, resistance, cold stacks or counters");
                if (step.SkillCasts.Any(cast => cast.ActorId == 1 && cast.SkillItemId == item)) break;
            }
            var hits = step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.TargetId == 2 && hit.SkillItemId == item).ToArray();
            Check(hits.Length > 0 && (item != 10047 || hits.Length == 1),
                "the original area hit or once-per-target large-body policy changed");
            bool eligible = item == 10047 && mode != "dodge" && mode != "blind" && mode != "forbidden";
            double rate = catalog.CalculateStatusApplicationRate(actor, target, 24266, 1, true, formula);
            Check(resistance == 0 ? Math.Abs(rate - 1) < .0001 : resistance < 0 ? rate >= 1 : rate <= 0,
                "the fixture did not exercise the intended status probability: " + rate);
            int initialStacks = mode == "cap" ? 9 : 0;
            int expected = eligible && resistance <= 0 ? Math.Min(10, initialStacks + 1) : initialStacks;
            var coldStates = target.Statuses.Where(status => status.ClassId == 24266).ToArray();
            Check(coldStates.Sum(status => status.StackCount) == expected
                && coldStates.All(status => status.CreatorId == 1 && status.ApplyingSkillItemId == 10606
                    && status.SourceSkillRank == rank && status.SourceSkillLevel == level
                    && status.DurationRound == 2 && status.MaxStackedCount == 10 && status.CanDispersed),
                "cold chance, cultivation, two-round duration or ten-stack limit differs: " + item + "/" + mode + "/" + resistance);
            Check(actor.Statuses.All(status => status.ClassId != 24266) && ally.Statuses.All(status => status.ClassId != 24266),
                "an enemy-only water hit applied cold to the caster or ally");
            Check(actor.Properties.Single(property => property.Id == affinity).Value == affinityValue,
                "the passive affinity was omitted or contributed more than once");
            var ordinary = baseline.DamageEvents.Where(hit => hit.SourceId == 1 && hit.TargetId == 2 && hit.SkillItemId == item).ToArray();
            if (mode == "normal" || mode == "cap" || mode == "forbidden")
                Check(item == 10047 ? hits.Sum(hit => -hit.Damage) > ordinary.Sum(hit => -hit.Damage)
                    : hits.Sum(hit => -hit.Damage) == ordinary.Sum(hit => -hit.Damage),
                    "water affinity did not increase only the original water damage: " + item + "/" + mode
                    + "/" + resistance + "/" + rank + "/" + level + ", affinity=" + affinityValue
                    + ", damage=" + hits.Sum(hit => -hit.Damage) + ", control=" + ordinary.Sum(hit => -hit.Damage));
            if (mode == "shield") Check(hits.All(hit => hit.Damage == 0 && hit.ShieldDamage > 0),
                "the full-shield fixture did not absorb the damage before the hit listener");
            if (mode == "dodge") Check(hits.All(hit => hit.Dodge && hit.Damage == 0), "the dodge guard was not exercised");
            if (mode == "blind") Check(hits.All(hit => hit.Blinding && hit.Damage == 0), "the blinding guard was not exercised");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
                "receipt replay duplicated cold or consumed another chance roll");
            var expiry = FightBattleCheckpointSerializer.Load(saved);
            var expiryTarget = expiry.Units.Single(unit => unit.Id == 2);
            FightStatusPropertyRuntime.RebuildAll(expiry.Units, catalog, formula);
            int vulnerable = formula.PropertyId("CookFinalWaterDmgReducePercent");
            Check(expiryTarget.Properties.Where(property => property.Id == vulnerable).Sum(property => property.Value) == -500L * expected,
                "cold omitted or duplicated its original per-stack water damage modifier");
            foreach (var cold in expiryTarget.Statuses.Where(status => status.ClassId == 24266).ToArray())
            {
                int start = cold.StartRound;
                Check(start >= 0 && FightStatusLifecycle.LeftRound(cold, start + 1) == 1,
                    "cold did not retain its second target turn");
                FightStatusLifecycle.TickTurn(expiryTarget, expiry.Units, start + 1, FightStatusLifecycle.TimingEnd);
                Check(expiryTarget.Statuses.Contains(cold), "cold expired before two target turns");
                FightStatusLifecycle.TickTurn(expiryTarget, expiry.Units, start + 2, FightStatusLifecycle.TimingEnd);
                Check(!expiryTarget.Statuses.Contains(cold), "cold survived its second target turn end");
            }
            cases++;
        }
        return "Cold glint passed: " + cases + " affinity, water/light, large-body, resistance, shield, miss, stack and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 4,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 10000 : 100;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", unit.Turn.Speed }, { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseElementAdd", 1000 }, { "BaseElementReduce", 1000 } })
            Set(unit, formula, pair.Key, pair.Value);
        return unit;
    }

    private static void Set(FightBattleUnitState unit, FightDamageFormula formula, string name, long value)
    {
        int id = formula.PropertyId(name); Check(id > 0, "unknown original property: " + name);
        unit.Properties.RemoveAll(property => property.Id == id);
        unit.Properties.Add(new FightPropertyValue { Id = id, Value = value });
    }
}
