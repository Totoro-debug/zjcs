using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleVoidBlessingHarness
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Void blessing: " + message);
    }

    private static FightBattleUnitState Unit(int id, int side, int x, long maximum, int hpPercent)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1,
            X = x, Y = 5, Hp = maximum * hpPercent / 100, MaxHp = maximum, LastMaxHp = maximum,
            Level = 40, SubRank = 2, Turn = new FightTurnUnit { Id = id, Speed = id == 1 ? 100 : 1 } };
        unit.Turn.Hp = unit.Hp;
        foreach (var entry in new Dictionary<int, long> { { 1, 100 }, { 2, 100 }, { 3, maximum },
            { 32, 100 }, { 33, 100 } })
            unit.Properties.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int itemId in new[] { 10060, 10127 })
        foreach (int[] growth in new[] { new[] { 1, 1, 1 }, new[] { 2, 20, 1 }, new[] { 6, 40, 2 } })
        foreach (int hpPercent in new[] { 20, 50 })
        {
            var actor = Unit(1, 1, 5, 1000000, 100);
            var ally = Unit(2, 1, 6, 2000000, hpPercent);
            var enemy = Unit(3, 2, 8, 3000000, 100);
            var item = items.Required(itemId);
            var skill = catalog.CreateSkill(item.EntityId, 0, itemId, growth[0], growth[1], growth[2],
                catalog.CalculateSkillProps(item.EntityId, growth[0], growth[1], growth[2])
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = 71,
                Grid = new FightBattleGrid { Width = 14, Height = 12 } };
            checkpoint.Units.AddRange(new[] { actor, ally, enemy });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); checkpoint.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            string before = FightBattleCheckpointSerializer.Save(checkpoint);
            var step = runner.Step(0, 1).Event;
            var statusProps = catalog.CalculateSkillProps(24190, growth[0], growth[1], growth[2])
                .ToDictionary(p => p.Key, p => p.Value);
            long bonus = (long)(1000000L * formula.ReadProperty(statusProps, 2000)
                + formula.ReadProperty(statusProps, 2011));
            string label = itemId + "/" + string.Join("-", growth) + "/" + hpPercent;
            Require(bonus > 0 && actor.MaxHp == 1000000 + bonus && ally.MaxHp == 2000000 + bonus,
                "Original caster maximum-HP contribution differs: " + label
                    + " bonus=" + bonus + " actor=" + actor.MaxHp + " ally=" + ally.MaxHp);
            Require(enemy.MaxHp == 3000000 && enemy.Hp == 3000000 && enemy.Statuses.Count == 0,
                "Friendly blessing changed an enemy: " + label);
            var applied = ally.Statuses.Single(status => status.ClassId == 24190);
            Require(applied.DurationRound == 3 && applied.SourceSkillRank == growth[0]
                && applied.SourceSkillLevel == growth[1] && applied.SourceSubRank == growth[2]
                && applied.CreatorId == actor.Id && applied.ApplyingSkillItemId == itemId,
                "Original duration or cultivation/source was lost: " + label);
            int child = itemId == 10060 ? 11620 : 12733;
            var heals = step.DamageEvents.Where(hit => hit.IsCure && hit.SkillEntityId == child).ToArray();
            long expectedHp = (long)Math.Ceiling(ally.MaxHp * hpPercent / 100d);
            if (hpPercent == 20)
            {
                Require(heals.Length == 1 && heals[0].SourceId == actor.Id && heals[0].TargetId == ally.Id,
                    "Low-health child healing changed source or target: " + label);
                // Original DamageProp=210 is SkillCureByHp (source), not
                // SkillCureByTargetHp=212. The child has its own non-fixed source.
                var childProperties = catalog.CalculateSkillProps(child, growth[0], growth[1], growth[2])
                    .ToDictionary(p => p.Key, p => p.Value);
                Require(formula.ReadProperty(childProperties, 210) == 0.05,
                    "Original five-percent source-maximum coefficient changed: " + label);
                long expectedCure = (long)(actor.MaxHp * formula.ReadProperty(childProperties, 210));
                Require(heals[0].AfterHp - heals[0].BeforeHp == expectedCure,
                    "Original five-percent source-maximum healing differs: " + label);
                expectedHp += expectedCure;
            }
            else Require(heals.Length == 0, "Healthy ally received the low-health child: " + label);
            Require(ally.Hp == expectedHp, "Maximum-HP ratio preservation or healing order differs: " + label);
            var restored = FightBattleCheckpointSerializer.Load(before);
            FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            Require(FightBattleCheckpointSerializer.Save(restored) == FightBattleCheckpointSerializer.Save(checkpoint),
                "Complete blessing effects changed after cold restoration: " + label);
            Require(runner.Step(0, 1).Replayed && ally.Hp == expectedHp,
                "Replayed blessing applied the effect twice: " + label);
            actor.BaseProperties.Single(value => value.Id == 3).Value = 3000000;
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, catalog, formula);
            Require(ally.MaxHp == 2000000 + bonus && actor.MaxHp == 3000000 + bonus,
                "Fixed blessing followed later source-property changes: " + label);
            cases++;
        }
        return "Void blessing passed: " + cases + " original cultivation, maximum-HP, low-health healing and checkpoint cases.";
    }

    public static string RunBoundary(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int itemId in new[] { 10060, 10127 })
        foreach (var hpCase in new[] { (29, 0), (30, -1), (30, 0), (30, 1), (31, 0) })
        {
            int hpPercent = hpCase.Item1;
            // The original rank-one bonus yields a final maximum divisible by ten,
            // both before and after fixing the separate per-cast snapshot issue.
            var actor = Unit(1, 1, 5, 1000357, 100);
            var ally = Unit(2, 1, 6, 2000000, hpPercent);
            ally.Hp += hpCase.Item2; ally.Turn.Hp = ally.Hp;
            var enemy = Unit(3, 2, 8, 3000000, 100);
            var item = items.Required(itemId);
            var skill = catalog.CreateSkill(item.EntityId, 0, itemId, 1, 1, 1,
                catalog.CalculateSkillProps(item.EntityId, 1, 1, 1)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = 71,
                Grid = new FightBattleGrid { Width = 14, Height = 12 } };
            checkpoint.Units.AddRange(new[] { actor, ally, enemy });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); checkpoint.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            string before = FightBattleCheckpointSerializer.Save(checkpoint);
            var step = runner.Step(0, 1).Event;
            int child = itemId == 10060 ? 11620 : 12733;
            var heals = step.DamageEvents.Where(hit => hit.IsCure && hit.SkillEntityId == child).ToArray();
            string label = itemId + "/" + hpPercent + "/" + hpCase.Item2;
            Require(heals.Length == (hpPercent < 30 || hpPercent == 30 && hpCase.Item2 < 0 ? 1 : 0),
                "Strictly-below-30% child boundary differs: " + label);
            if (hpPercent == 30 && hpCase.Item2 == 0)
                Require(ally.MaxHp % 10 == 0 && ally.Hp * 10 == ally.MaxHp * 3,
                    "Boundary fixture did not remain at exactly 30%: " + label);
            var restored = FightBattleCheckpointSerializer.Load(before);
            FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            Require(FightBattleCheckpointSerializer.Save(restored) == FightBattleCheckpointSerializer.Save(checkpoint),
                "Strict child boundary changed after cold restoration: " + label);
            Require(runner.Step(0, 1).Replayed, "Boundary cast did not replay: " + label);
            cases++;
        }
        return "Void blessing boundary passed: " + cases + " exact 29%, 30%, 31%, adjacent-HP and cold-restoration cases.";
    }

    public static string RunUnique(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int itemId in new[] { 10060, 10127 })
        foreach (long sourceMaximum in new[] { 500000L, 1000000L, 2000000L })
        foreach (bool sameSource in new[] { false, true })
        {
            var actor = Unit(1, 1, 5, sourceMaximum, 100);
            var ally = Unit(2, 1, 6, 2000000, 50);
            var oldSource = Unit(7, 1, 1, 1000000, 100);
            var enemy = Unit(3, 2, 10, 3000000, 100);
            var item = items.Required(itemId);
            var skill = catalog.CreateSkill(item.EntityId, 0, itemId, 1, 1, 1,
                catalog.CalculateSkillProps(item.EntityId, 1, 1, 1)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var previous = catalog.CreateStatus(24190, ally.Id, sameSource ? actor.Id : oldSource.Id,
                1, sameSource ? item.EntityId : 11180, sameSource ? itemId : 10060,
                1, 1, 1, oldSource.Properties);
            previous.StartRound = 0; previous.CurDurationRound = 1;
            ally.Statuses.Add(previous);
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = 71,
                Grid = new FightBattleGrid { Width = 14, Height = 12 } };
            checkpoint.Units.AddRange(new[] { actor, ally, enemy, oldSource });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); checkpoint.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            string before = FightBattleCheckpointSerializer.Save(checkpoint);
            runner.Step(0, 1);
            string after = FightBattleCheckpointSerializer.Save(checkpoint);
            var winner = ally.Statuses.Single(status => status.ClassId == 24190);
            string label = itemId + "/" + sourceMaximum + "/" + sameSource;
            Require(winner.CreatorId == (sourceMaximum < 1000000 && !sameSource ? oldSource.Id : actor.Id),
                "Unique effect selected the wrong source: " + label);
            Require(winner.CurDurationRound == (sourceMaximum < 1000000 ? 1 : 3),
                "Weaker effect refreshed, or equal/stronger effect failed to refresh: " + label);
            var statusProps = catalog.CalculateSkillProps(24190, 1, 1, 1).ToDictionary(p => p.Key, p => p.Value);
            long bonus = (long)(Math.Max(1000000L, sourceMaximum) * formula.ReadProperty(statusProps, 2000)
                + formula.ReadProperty(statusProps, 2011));
            Require(ally.MaxHp == 2000000 + bonus, "Unique effects stacked or used cultivation as strength: " + label);
            Require(winner.SourceProperties.Single(value => value.Id == 3).Value == Math.Max(1000000L, sourceMaximum),
                "Unique replacement retained stale source properties: " + label);
            var restored = FightBattleCheckpointSerializer.Load(before);
            FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            Require(FightBattleCheckpointSerializer.Save(restored) == after,
                "Unique replacement changed after cold restoration: " + label);
            Require(runner.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(checkpoint) == after,
                "Replay renewed or replaced the winner twice: " + label);
            FightStatusLifecycle.End(ally, winner);
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, catalog, formula);
            Require(!ally.Statuses.Any(status => status.ClassId == 24190) && ally.MaxHp == 2000000,
                "Ignored/replaced weaker effect returned after the winner ended: " + label);
            cases++;
        }
        return "Void blessing unique effects passed: " + cases + " weaker, equal, stronger and checkpoint cases.";
    }

    public static string RunFortress(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int itemId in new[] { 10571, 10128 })
        foreach (long sourceDefense in new[] { 50L, 100L, 200L })
        {
            var actor = Unit(1, 1, 5, 1000000, 100);
            actor.Properties.Single(value => value.Id == 2).Value = sourceDefense;
            var ally = Unit(2, 1, 6, 2000000, 50);
            var oldSource = Unit(7, 1, 1, 1000000, 100);
            var enemy = Unit(3, 2, 10, 3000000, 100);
            int initial = itemId == 10571 ? 25041 : 25160;
            actor.Statuses.Add(catalog.CreateStatus(initial, actor.Id, actor.Id, 1, initial,
                itemId, 2, 20, 1, actor.Properties));
            var previous = catalog.CreateStatus(24200, ally.Id, oldSource.Id, 1, 11190, 10571,
                2, 20, 1, oldSource.Properties);
            previous.StartRound = 0; previous.CurDurationRound = previous.DurationRound;
            ally.Statuses.Add(previous);
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = 71,
                Grid = new FightBattleGrid { Width = 14, Height = 12 } };
            checkpoint.Units.AddRange(new[] { actor, ally, enemy, oldSource });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); checkpoint.Waves.Add(wave);
            var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            string before = FightBattleCheckpointSerializer.Save(checkpoint);
            runner.Step(0, 1);
            var winner = ally.Statuses.Single(status => status.ClassId == 24200);
            string label = itemId + "/" + sourceDefense;
            Require(winner.CreatorId == (sourceDefense < 100 ? oldSource.Id : actor.Id),
                "Golden Fortress selected the wrong unique source: " + label);
            var statusProps = catalog.CalculateSkillProps(24200, 2, 20, 1).ToDictionary(p => p.Key, p => p.Value);
            long bonus = (long)(Math.Max(100L, sourceDefense) * formula.ReadProperty(statusProps, 2000)
                + formula.ReadProperty(statusProps, 2011));
            Require(ally.Properties.Single(value => value.Id == 2).Value == 100 + bonus,
                "Golden Fortress unique bonuses stacked: " + label);
            var restored = FightBattleCheckpointSerializer.Load(before);
            FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            Require(FightBattleCheckpointSerializer.Save(restored) == FightBattleCheckpointSerializer.Save(checkpoint),
                "Golden Fortress unique source changed after restoration: " + label);
            cases++;
        }
        return "Golden Fortress unique effects passed: " + cases + " original passive-source and strength cases.";
    }
}
