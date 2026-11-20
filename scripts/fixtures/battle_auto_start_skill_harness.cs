using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleAutoStartSkillHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Opening skill: " + message);
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int speed)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1,
            X = x, Y = 5, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000, Level = 50, SubRank = 1,
            Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = speed } };
        foreach (var entry in new Dictionary<int, long> { { 1, 100 }, { 2, 100 }, { 3, 1000000 }, { 4, speed } })
            unit.Properties.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
        return unit;
    }

    private static FightBattleCheckpoint Setup(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        int enemyX, bool forbidFast)
    {
        var slow = Unit(1, 1, 5, 100);
        var enemy = Unit(2, 2, enemyX, 500);
        var fast = Unit(3, 1, 5, 300); fast.Y = 6;
        enemy.Statuses.Add(catalog.CreateStatus(25006, enemy.Id, enemy.Id, enemy.Id,
            25006, 10513, 2, 20, 1, enemy.Properties));
        foreach (var owner in new[] { slow, fast })
        {
            var passive = catalog.CreateStatus(25006, owner.Id, owner.Id, owner.Id, 25006, 10513, 2, 20, 1, owner.Properties);
            owner.Statuses.Add(passive);
            var skill = catalog.CreateSkill(11100, 0, 10049, 2, 20, 1,
                catalog.CalculateSkillProps(11100, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            owner.Skills.Add(skill); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 4));
        }
        var second = catalog.CreateSkill(11100, 1, 10049, 2, 20, 1,
            catalog.CalculateSkillProps(11100, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
        fast.Skills.Add(second); fast.SkillState.Slots.Add(catalog.CreateSkillSlot(second, 4));
        if (forbidFast) fast.Statuses.Add(catalog.CreateStatus(24010, fast.Id, fast.Id, 9924010));
        var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 20, Height = 20 } };
        cp.Units.AddRange(new[] { slow, enemy, fast });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        return cp;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var cp = Setup(catalog, items, 6, false);
        var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var resume = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
        var startup = runner.Step(0, 1).Event; resume.Step(0, 1);
        var casts = startup.SkillCasts.Where(cast => cast.SkillEntityId == 11100).ToArray();
        Check(casts.Length == 3 && casts[0].ActorId == 3 && casts[0].Slot == 0
            && casts[1].ActorId == 3 && casts[1].Slot == 1 && casts[2].ActorId == 1 && casts[2].Slot == 0,
            "Skills did not execute in speed and slot order before the first turn.");
        Check(casts[0].Delay <= casts[1].Delay && casts[1].Delay <= casts[2].Delay,
            "Opening cast playback timing did not follow the confirmed order.");
        Check(startup.DamageEvents.Any(hit => hit.SkillEntityId == 11100 && hit.TargetId == 2
            && hit.ShieldDamage > 0), "Opening attacks ran before the target's opening passive shield.");
        Check(cp.Units.Where(unit => unit.Side == 1).All(unit => unit.SkillState.Round == 0
            && unit.Statuses.Any(status => status.ClassId == 24175)
            && unit.SkillState.Slots.All(slot => slot.ReleasedTimes == 1 && slot.AutoStartChecked
                && new FightSkillState(unit.SkillState).LeftCooldown(slot.Slot) > 0)),
            "Opening shields, personal round, release count or cooldown differs.");
        Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved),
            "Cold startup continuation differs.");
        Check(runner.Step(0, 1).Replayed && cp.Units.Single(unit => unit.Id == 1).SkillState.Slots.Single().ReleasedTimes == 1,
            "Receipt replay repeated an opening skill.");
        string legacy = FightBattleCheckpointSerializer.Save(cp).Replace(",\"AutoStartChecked\":true", "");
        var legacyCp = FightBattleCheckpointSerializer.Load(legacy);
        var legacyRunner = FightBattleExecutor.Restore(legacyCp, formula, new FightDamageSettlement(), catalog, items);
        var legacyStep = legacyRunner.Step(0, legacyCp.Counter + 1).Event;
        Check(!legacyStep.SkillCasts.Any(cast => cast.SkillEntityId == 11100)
            && legacyCp.Units.Where(unit => unit.Side == 1).All(unit => unit.SkillState.Slots.All(slot => slot.ReleasedTimes == 1)),
            "A legacy in-progress battle repeated its opening casts.");

        cp = Setup(catalog, items, 6, false);
        var tied = cp.Units.Single(unit => unit.Id == 3);
        tied.Turn.Speed = 100; tied.Properties.Single(property => property.Id == 4).Value = 100;
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        casts = runner.Step(0, 1).Event.SkillCasts.Where(cast => cast.SkillEntityId == 11100).ToArray();
        Check(casts.Length == 3 && casts[0].ActorId == 1 && casts[1].ActorId == 3 && casts[2].ActorId == 3,
            "Equal-speed opening skills did not retain roster and slot order.");

        cp = Setup(catalog, items, 15, false);
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        startup = runner.Step(0, 1).Event;
        Check(!startup.SkillCasts.Any(cast => cast.SkillEntityId == 11100)
            && cp.Units.Where(unit => unit.Side == 1).All(unit => unit.SkillState.Slots.All(slot =>
                slot.ReleasedTimes == 0 && slot.LastRound == 0 && slot.AutoStartChecked)),
            "An opening skill without a target consumed a release or remained pending.");
        cp = Setup(catalog, items, 15, false);
        foreach (var unit in cp.Units) unit.Statuses.Clear();
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        startup = runner.Step(0, 1).Event;
        Check(cp.Counter == 1 && cp.Units.Where(unit => unit.Side == 1).All(unit =>
            unit.SkillState.Slots.All(slot => slot.AutoStartChecked && slot.ReleasedTimes == 0)),
            "A skipped opening without any passive event was not committed with the first ordinary step.");
        cp = Setup(catalog, items, 6, true);
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        startup = runner.Step(0, 1).Event;
        Check(!startup.SkillCasts.Any(cast => cast.ActorId == 3 && cast.SkillEntityId == 11100)
            && cp.Units.Single(unit => unit.Id == 3).SkillState.Slots.All(slot => slot.ReleasedTimes == 0
                && slot.LastRound == 0 && slot.AutoStartChecked),
            "A control-blocked opening skill consumed a release or cooldown.");
        cp = Setup(catalog, items, 6, false);
        var fragile = cp.Units.Single(unit => unit.Id == 2);
        fragile.Statuses.Clear();
        fragile.Hp = fragile.Turn.Hp = 1;
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        startup = runner.Step(0, 1).Event;
        Check(startup.DamageEvents.Any(hit => hit.TargetId == fragile.Id && hit.AfterHp == 0)
            && cp.Finished, "A lethal opening skill did not settle the battle before an ordinary turn.");
        cp = Setup(catalog, items, 6, false);
        var departing = cp.Units.Single(unit => unit.Id == 2); departing.Hp = departing.Turn.Hp = 0;
        var pendingWave = Unit(4, 2, 6, 50);
        cp.Units.Add(pendingWave);
        var later = new FightBattleWaveState(); later.UnitIds.Add(pendingWave.Id); cp.Waves.Add(later);
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        startup = runner.Step(0, 1).Event;
        Check(cp.WaveIndex == 1 && startup.DamageEvents.Any(hit => hit.SkillEntityId == 11100
            && hit.TargetId == pendingWave.Id), "A new wave was not considered after opening passives settled.");
        var solo = Unit(1, 1, 5, 100);
        var openingBlessing = catalog.CreateSkill(11180, 0, 10060, 2, 20, 1,
            catalog.CalculateSkillProps(11180, 2, 20, 1)
                .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
        solo.Skills.Add(openingBlessing); solo.SkillState.Slots.Add(catalog.CreateSkillSlot(openingBlessing, 4));
        cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 20, Height = 20 } };
        cp.Units.Add(solo);
        runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        Check(!cp.Finished, "Initial result bypassed the pending opening skill.");
        startup = runner.Step(0, 1).Event;
        Check(startup.SkillCasts.Any(cast => cast.SkillEntityId == 11180) && cp.Finished,
            "An opening self-target blessing was skipped by initial result evaluation.");
        foreach (int itemId in new[] { 10049, 10060, 10066, 10127, 10129 })
        {
            var item = items.Required(itemId);
            var player = Unit(1, 1, 5, 100);
            var enemy = Unit(2, 2, 6, 500);
            var ally = Unit(3, 1, 5, 50); ally.Y = 6;
            var skill = catalog.CreateSkill(item.EntityId, 0, itemId, 2, 20, 1,
                catalog.CalculateSkillProps(item.EntityId, 2, 20, 1)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 4));
            cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            cp.Units.AddRange(new[] { player, enemy, ally });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            startup = runner.Step(0, 1).Event;
            Check(startup.SkillCasts.Count(cast => cast.ActorId == 1 && cast.SkillEntityId == item.EntityId
                && cast.SkillItemId == itemId) == 1 && player.SkillState.Round == 0,
                "Original opening item did not release once before the first turn: " + itemId);
            if (itemId == 10060 || itemId == 10127)
                Check(player.Statuses.Any(status => status.ClassId == 24190)
                    && ally.Statuses.Any(status => status.ClassId == 24190),
                    "Opening blessing did not apply the original maximum-HP effect to both allies: " + itemId);
            if (itemId == 10066)
                Check(player.Statuses.Any(status => status.ClassId == 24237)
                    && ally.Statuses.Any(status => status.ClassId == 24237),
                    "Opening encouragement did not apply the original damage effect to both allies.");
            if (itemId == 10049 || itemId == 10129)
                Check(startup.DamageEvents.Any(hit => hit.SourceId == 1 && hit.TargetId == 2
                    && hit.SkillEntityId == item.EntityId), "Opening challenge did not hit the nearby enemy: " + itemId);
        }
        foreach (int itemId in new[] { 10019, 10024, 10025, 10046, 10051, 10058 })
        {
            var item = items.Required(itemId);
            var skill = catalog.CreateSkill(item.EntityId, 0, itemId, 2, 20, 1, null);
            Check(skill.ResetCooldownAtStart, "Original initial-cooldown flag missing: " + itemId);
            var state = new FightSkillAgentState(); state.Slots.Add(catalog.CreateSkillSlot(skill, 4));
            var cooldown = new FightSkillState(state); cooldown.Initialize(0); cooldown.BeginRound(1);
            Check(cooldown.LeftCooldown(0) <= 0, "Initial cooldown was not complete in round one: " + itemId);
            cooldown.RunSkill(0);
            Check(cooldown.LeftCooldown(0) == 4, "Skill did not enter cooldown after release: " + itemId);
        }
        CheckChallengeTargets(catalog, formula, items);
        CheckEncouragementDispel(catalog, formula, items);
        return "Opening skills passed: passive order, cooldown, challenge targets, random dispel and checkpoint.";
    }

    private static void CheckEncouragementDispel(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var negativeIds = new[] { 24010, 24266, 24216 };
        foreach (int count in new[] { 1, 2 })
        {
            var removedCounts = negativeIds.ToDictionary(id => id, id => 0);
            foreach (int seed in Enumerable.Range(0, 64).Select(index => 1 + index * 100000))
            {
                var owner = Unit(1, 1, 5, 1000);
                var ally = Unit(2, 1, 6, 10);
                var enemy = Unit(3, 2, 5, 5); enemy.Y = 6;
                var outside = Unit(4, 1, 11, 5);
                foreach (int statusId in negativeIds)
                    ally.Statuses.Add(catalog.CreateStatus(statusId, ally.Id, enemy.Id, 700000 + statusId));
                var item = items.Required(10066);
                var skill = catalog.CreateSkill(item.EntityId, 0, 10066, 2, 20, 1,
                    catalog.CalculateSkillProps(item.EntityId, 2, 20, 1)
                        .Select(prop => new FightPropertyValue { Id = prop.Key, Value = prop.Value }).ToList());
                var hit = skill.HitGraphEntities.Single(value => value.EntityId == 11551);
                Check(hit.DisperseCount == 1 && hit.DisperseRandom && hit.DisperseChance == 1f
                    && !hit.DisperseAffectedByProp && hit.DisperseStatusTypes.OrderBy(value => value).SequenceEqual(new[] { 2, 3 }),
                    "Encouragement dispel configuration differs.");
                // Two removals exercise the shared random-subset path used by other configured skills.
                hit.DisperseCount = count;
                owner.Skills.Add(skill); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 4));
                var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = seed,
                    Grid = new FightBattleGrid { Width = 20, Height = 20 } };
                checkpoint.Units.AddRange(new[] { owner, ally, enemy, outside });
                var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); checkpoint.Waves.Add(wave);
                var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
                var restoredState = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var restored = FightBattleExecutor.Restore(restoredState, formula, new FightDamageSettlement(), catalog, items);
                var step = executor.Step(0, 1).Event; restored.Step(0, 1);
                Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restoredState),
                    "Encouragement dispel differed after checkpoint restore.");
                var buff = ally.Statuses.SingleOrDefault(status => status.ClassId == 24237);
                Check(buff != null && buff.CreatorId == owner.Id && buff.DurationRound == 2
                    && owner.Statuses.Any(status => status.ClassId == 24237)
                    && !enemy.Statuses.Any(status => status.ClassId == 24237)
                    && !outside.Statuses.Any(status => status.ClassId == 24237), "Encouragement target or damage buff differs.");
                int[] removed = negativeIds.Where(id => !ally.Statuses.Any(status => status.ClassId == id)).ToArray();
                Check(removed.Length == count && step.DispersedUnitIds.Count(id => id == ally.Id) == 1,
                    "Encouragement did not remove the requested number of distinct effects with one notification.");
                foreach (int id in removed) removedCounts[id]++;
            }
            Check(removedCounts.Values.All(value => value > 0 && value < 64),
                "Random dispel did not select every eligible effect across the seeded trials.");
        }
    }

    private static void CheckChallengeTargets(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (int itemId in new[] { 10049, 10129 })
        foreach (int type in new[] { 1, 2, 3, 7 })
        {
            int taunts = 0;
            foreach (int seed in Enumerable.Range(1, 64))
            {
                var owner = Unit(1, 1, 5, 1000);
                var target = Unit(2, 2, 6, 10); target.CharacterType = type;
                var ally = Unit(3, 1, 5, 5); ally.Y = 6;
                var outside = Unit(4, 2, 9, 5); outside.CharacterType = type;
                var item = items.Required(itemId);
                var skill = catalog.CreateSkill(item.EntityId, 0, itemId, 2, 20, 1,
                    catalog.CalculateSkillProps(item.EntityId, 2, 20, 1)
                        .Select(prop => new FightPropertyValue { Id = prop.Key, Value = prop.Value }).ToList());
                var application = skill.HitGraphEntities.Single(hit => hit.EntityId == (itemId == 10049 ? 11101 : 12761))
                    .AppliedStatuses.Single(status => status.StatusId == 24216);
                Check(Math.Abs(application.Chance - .85f) < .00001f && !application.AffectedByProp
                    && application.CharacterTypeRateOverrides.Count == 3
                    && application.CharacterTypeRateOverrides.All(entry => new[] { 2, 3, 7 }.Contains(entry.CharacterType)
                        && entry.Rate == 1f && !entry.AffectedByProp), "Challenge taunt configuration differs.");
                owner.Skills.Add(skill); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 4));
                var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = seed,
                    Grid = new FightBattleGrid { Width = 20, Height = 20 } };
                checkpoint.Units.AddRange(new[] { owner, target, ally, outside });
                var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 4 }); checkpoint.Waves.Add(wave);
                var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
                var restoredState = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var restored = FightBattleExecutor.Restore(restoredState, formula, new FightDamageSettlement(), catalog, items);
                var step = executor.Step(0, 1).Event; restored.Step(0, 1);
                Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restoredState),
                    "Challenge target or taunt differed after checkpoint restore.");
                var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == item.EntityId).ToArray();
                Check(hits.Length == 1 && hits[0].TargetId == target.Id,
                    "Challenge hit a friendly or out-of-range target: " + itemId + "/" + type + "/" + seed);
                var taunt = target.Statuses.SingleOrDefault(status => status.ClassId == 24216);
                Check(!ally.Statuses.Any(status => status.ClassId == 24216)
                    && !outside.Statuses.Any(status => status.ClassId == 24216), "Challenge taunted an excluded target.");
                if (taunt != null)
                {
                    taunts++;
                    Check(taunt.CreatorId == owner.Id && taunt.DurationRound == 1, "Challenge taunt source or duration differs.");
                }
                if (type != 1) Check(taunt != null, "Challenge failed its guaranteed non-character taunt: " + type);
            }
            if (type == 1) Check(taunts > 0 && taunts < 64, "Challenge character taunt did not use its configured chance.");
        }
    }
}
