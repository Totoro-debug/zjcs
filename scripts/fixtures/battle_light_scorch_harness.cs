using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleLightScorchHarness
{
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Light scorch: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, procs = 0, noProcs = 0, dazzles = 0;
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int subRank in new[] { 8, 40 })
        foreach (int seed in Enumerable.Range(1, 32).Select(value => value * 104729))
        {
            var cp = Battle(formula, seed, subRank);
            var actor = cp.Units[0]; var target = cp.Units[1];
            Equip(catalog, actor, 12070, 10098, 3, 1, subRank);
            var listener = catalog.CreateStatus(25046, 1, 1, 1, applyingSkillItemId: 10577,
                sourceSkillRank: rank, sourceSkillLevel: level, sourceSubRank: subRank);
            actor.Statuses.Add(listener);
            var info = listener.Components.HitSkill;
            Check(info.Rate == .5f && info.Damage && !info.Cure && !info.IsApplyOneHit
                && info.SkillTriggers.Count == 1 && info.SkillTriggers[0].EntityId == 11680,
                "the original per-hit half-chance listener changed");
            var childProps = catalog.CalculateSkillProps(11680, rank, level, subRank);
            // Original level -> rank -> entity factors, truncating each scale.
            long levelFixed = subRank == 8 ? level == 1 ? 99 : level == 2 ? 198 : 2040
                : level == 1 ? 417421 : level == 2 ? 417811 : 424005;
            long percentage = (long)(6200 * (rank == 3 ? .56 : .7));
            long fixedValue = (long)((long)(levelFixed * (rank == 3 ? .95 : rank == 6 ? 1.5 : 1.8)) * .62);
            Check(childProps[200] == percentage && childProps[190] == fixedValue,
                "child percentage/fixed cultivation does not follow the listener's rank and level: "
                + rank + "/" + level + "/" + subRank + " actual " + childProps[200] + "/" + childProps[190]
                + " expected " + percentage + "/" + fixedValue);
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            double dazzleChance = catalog.CalculateStatusApplicationRate(actor, target, 24234, .3f, true, formula);
            Check(!double.IsNaN(dazzleChance) && Math.Abs(dazzleChance - .3f) < .0000001,
                "equal neutral effect attributes changed the original dazzle base probability: " + dazzleChance);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(saved.Sequence, saved.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved),
                "restoring before a proc changes probability, damage or status application");
            var primary = step.SkillCasts.Single(cast => cast.SkillEntityId == 12070);
            var parentHit = step.DamageEvents.Single(hit => hit.SkillEntityId == 12070);
            Check(parentHit.Damage < 0 && !parentHit.IsCure, "the eligible primary hit never dealt damage: "
                + rank + "/" + level + "/" + subRank + "/" + seed + " damage=" + parentHit.Damage
                + " dodge=" + parentHit.Dodge + " blind=" + parentHit.Blinding + " immunity=" + parentHit.Immunity);
            var casts = step.SkillCasts.Where(cast => cast.SkillEntityId == 11680).ToArray();
            Check(casts.Length <= 1 && info.InvokeCount == casts.Length,
                "one primary hit repeated its proc or a child recursively triggered it");
            if (casts.Length == 0) noProcs++;
            else
            {
                procs++;
                var cast = casts[0];
                var hit = step.DamageEvents.Single(value => value.SkillEntityId == 11680);
                long expected = -(long)((fixedValue + 1000m * percentage / 10000m) * 1000m / 1010m * 1.2m);
                Check(cast.ActorId == 1 && cast.SkillItemId == 10577
                    && hit.SourceId == 1 && hit.TargetId == 2 && hit.SkillItemId == 10577
                    && hit.HitEntityId == 11681 && hit.DamageEntityId == 11682
                    && hit.ElementType == 103 && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure
                    && hit.Damage == expected, "the child lost its source, target, light affinity or cultivated damage");
                Check(Math.Abs(cast.Delay - parentHit.Delay) < .0001f
                    && Math.Abs(hit.Delay - cast.Delay - .1f) < .0001f,
                    "the child lost the source impact or its authored hit delay: parentCast=" + primary.Delay
                    + " parentHit=" + parentHit.Delay + " childCast=" + cast.Delay + " childHit=" + hit.Delay);
                var dazzle = target.Statuses.SingleOrDefault(status => status.ClassId == 24234);
                if (dazzle != null)
                {
                    dazzles++;
                    Check(dazzle.ApplyingSkillItemId == 10577 && dazzle.CreatorId == 1
                        && dazzle.SourceSkillRank == rank && dazzle.SourceSkillLevel == level
                        && dazzle.Components.HitDmgAddPer.DurationSkillCount == 1,
                        "dazzle lost its source, cultivation or next-skill lifetime");
                }
            }
            Check(actor.SkillState.Slots.Single().ReleasedTimes == 1
                && actor.Statuses.Count == 1 && actor.Statuses[0].ClassId == 25046,
                "the child consumed another active slot or added an unconfigured owner effect");
            string after = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(cp.Sequence, cp.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(cp) == after, "replay repeated a proc");
            cases++;
        }
        Check(procs > 0 && noProcs > 0 && dazzles > 0,
            "fixed seeds did not cover successful/failed procs and actual dazzle application: "
            + procs + "/" + noProcs + "/" + dazzles);
        foreach (int mode in new[] { 1, 2, 3 })
        foreach (int seed in new[] { 7, 37, 1234567, 987654321 })
        {
            var cp = Battle(formula, seed, 8); var actor = cp.Units[0];
            Equip(catalog, actor, 12070, 10098, 7, 2, 8);
            var listener = catalog.CreateStatus(25046, 1, 1, 1, applyingSkillItemId: 10577,
                sourceSkillRank: 7, sourceSkillLevel: 2, sourceSubRank: 8);
            actor.Statuses.Add(listener);
            if (mode == 1) FightStatusLifecycle.SetForbidden(actor, listener,
                FightStatusLifecycle.ForbiddenStatusTrigger, true);
            if (mode == 2) actor.Skills[0].CanTriggerChild = false;
            if (mode == 3)
            {
                actor.Properties.Add(new FightPropertyValue { Id = 72, Value = 10000 });
                actor.BaseProperties.Add(new FightPropertyValue { Id = 72, Value = 10000 });
            }
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            Check(step.SkillCasts.All(cast => cast.SkillEntityId != 11680)
                && listener.Components.HitSkill.InvokeCount == 0,
                "forbidden, child-disabled or blinded damage triggered the listener: " + mode);
            cases++;
        }
        int blinded = 0, hitNormally = 0;
        foreach (bool ordinaryAction in new[] { false, true })
        foreach (int seed in new[] { 7, 37, 12345, 1234567, 7919, 65537, 104729, 4444, 92821, 999999, 123456789, 987654321 })
        {
            var cp = Battle(formula, seed, 8); var actor = cp.Units[0];
            Equip(catalog, actor, ordinaryAction ? 12070 : 11680, ordinaryAction ? 10098 : 10577, 7, 2, 8);
            actor.Skills[0].CanTriggerChild = false;
            var dazzle = catalog.CreateStatus(24234, 1, 2, 1, 11680, 10577, 7, 2, 8);
            actor.Statuses.Add(dazzle);
            Check(catalog.CalculateSkillProps(24234, 7, 2, 8)[72] == 5000,
                "the original dazzle blinding percentage changed");
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            var hit = step.DamageEvents.Single();
            Check(actor.Statuses.Any(status => status.ClassId == 24234) == !ordinaryAction,
                "dazzle did not persist through an automatic child or expire on the next damaging active");
            Check(actor.Properties.All(property => property.Id != 72 || property.Value == 0),
                "conditional blinding leaked into persistent owner properties");
            if (ordinaryAction)
            {
                if (hit.Blinding) { blinded++; Check(hit.Damage == 0, "a blinded hit still dealt damage"); }
                else { hitNormally++; Check(hit.Damage < 0, "a non-blinded active never dealt damage"); }
            }
            else Check(!hit.Blinding && hit.Damage < 0, "dazzle incorrectly modified the automatic child action");
            cases++;
        }
        Check(blinded > 0 && hitNormally > 0, "fixed seeds did not cover both dazzle outcomes");
        return "Light scorch passed: " + cases + " cultivation, proc/child gates, light damage, dazzle consumption and checkpoint cases.";
    }

    static FightBattleCheckpoint Battle(FightDamageFormula formula, int seed, int subRank)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        cp.Units.Add(Unit(1, 1, 10, formula, subRank)); cp.Units.Add(Unit(2, 2, 12, formula, subRank));
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        return cp;
    }
    static void Equip(FightOfficialCombatCatalog catalog, FightBattleUnitState actor,
        int entity, int item, int rank, int level, int subRank)
    {
        var skill = catalog.CreateSkill(entity, 0, item, rank, level, subRank,
            catalog.CalculateSkillProps(entity, rank, level, subRank)
                .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
    }
    static FightBattleUnitState Unit(int id, int side, int x, FightDamageFormula formula, int subRank)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 12,
            Level = 70, SubRank = subRank, X = x, Y = 10, Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        if (id == 1) unit.Properties.Add(new FightPropertyValue { Id = 413, Value = 2000 });
        unit.BaseProperties = unit.Properties.Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList();
        return unit;
    }
}
