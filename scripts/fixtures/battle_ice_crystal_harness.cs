using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleIceCrystalHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int successSeed = 0;
        int resistedSeed = 0;
        for (int seed = 1; seed <= 256 && (successSeed == 0 || resistedSeed == 0); seed++)
        {
            var outcome = Freeze(catalog, formula, items, seed * 1009, true);
            if (!outcome.First) continue;
            if (outcome.Second && successSeed == 0) successSeed = seed * 1009;
            if (!outcome.Second && resistedSeed == 0) resistedSeed = seed * 1009;
        }
        Check(successSeed > 0 && resistedSeed > 0, "the fixture could not produce both second-caster outcomes");
        Verify(catalog, formula, items, successSeed, true, 2);
        Verify(catalog, formula, items, resistedSeed, true, 1);
        Verify(catalog, formula, items, successSeed, false, 0);
        return "Ice crystal passed: latest accepted freeze source, resisted refresh, source without the secret, water damage and resumed thaw replay.";
    }

    private static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int seed, bool secondOwnsSecret, int expectedSource)
    {
        var scenario = Freeze(catalog, formula, items, seed, secondOwnsSecret);
        Check(scenario.First && scenario.Second == (expectedSource != 1), "the pinned freeze outcome changed");
        var checkpoint = scenario.Checkpoint;
        var target = checkpoint.Units.Single(unit => unit.Id == 3);
        var markers = target.Statuses.Where(status => status.ClassId == 24487).ToArray();
        Check(markers.Length == (expectedSource > 0 ? 1 : 0), "the target has a stale or duplicate thaw listener");
        if (expectedSource > 0)
            Check(markers[0].CreatorId == expectedSource && markers[0].ApplyingSkillItemId == 10557
                && markers[0].SourceSkillRank == expectedSource + 2 && markers[0].SourceSkillLevel == 2,
                "the thaw listener lost the last successful caster or its cultivation");

        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        var uninterrupted = FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        for (int turn = 0; turn < 20 && target.Statuses.Any(status => status.ClassId == 24215); turn++)
        {
            var step = uninterrupted.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(restored.Sequence, restored.Counter + 1);
            var bursts = step.DamageEvents.Where(hit => hit.SkillEntityId == 11410).ToArray();
            bool stillFrozen = target.Statuses.Any(status => status.ClassId == 24215);
            Check(bursts.Length == (!stillFrozen && expectedSource > 0 ? 1 : 0),
                "the water strike triggered early, failed to trigger, or repeated");
            if (bursts.Length == 1)
            {
                var hit = bursts[0];
                Check(hit.SourceId == expectedSource && hit.TargetId == target.Id && hit.SkillItemId == 10557
                    && hit.HitEntityId == 11411 && hit.DamageEntityId == 10005 && hit.ElementType == 101
                    && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure,
                    "the thaw strike lost its original cultivated water damage or source");
                Check(step.StatusViewEvents.Any(value => value.OwnerId == target.Id && value.Status.ClassId == 24487
                    && value.Status.CreatorId == expectedSource && value.Kind == FightBattleStatusViewEventKind.Invoke),
                    "the thaw did not publish the original listener invocation");
            }
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
                "resuming before the thaw changed its outcome");
        }
        Check(target.Statuses.All(status => status.ClassId != 24215 && status.ClassId != 24487),
            "the original freeze or one-shot listener remained after thawing");
        string settled = FightBattleCheckpointSerializer.Save(restored);
        Check(resumed.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == settled, "replay duplicated the completed thaw");
    }

    private static (FightBattleCheckpoint Checkpoint, bool First, bool Second) Freeze(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int seed, bool secondOwnsSecret)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        var first = Unit(1, 1, 5, 1000, formula);
        var second = Unit(2, 1, 7, 900, formula);
        var target = Unit(3, 2, 6, 800, formula);
        foreach (var source in new[] { first, second })
        {
            int rank = source.Id + 2;
            var skill = catalog.CreateSkill(10900, 0, 10035, rank, 2, 8,
                catalog.CalculateSkillProps(10900, rank, 2, 8).Select(pair =>
                    new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            skill.LimitedTimes = 1;
            skill.ResetCooldownAtStart = true;
            source.Skills.Add(skill);
            source.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            if (source.Id == 1 || secondOwnsSecret)
                source.Statuses.Add(catalog.CreateStatus(25037, source.Id, source.Id, 725037, 25037, 10557, rank, 2, 8));
        }
        checkpoint.Units.AddRange(new[] { first, second, target });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(target.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var firstStep = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var secondStep = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        Check(firstStep.ActorId == 1 && secondStep.ActorId == 2
            && firstStep.SkillCasts.Any(cast => cast.SkillEntityId == 10900)
            && secondStep.SkillCasts.Any(cast => cast.SkillEntityId == 10900),
            $"the casters did not act before the target ({CastSummary(firstStep)} then {CastSummary(secondStep)})");
        Check(!firstStep.SkillCasts.Concat(secondStep.SkillCasts).Any(cast => cast.SkillEntityId == 11410),
            "applying or refreshing the freeze caused premature thaw damage");
        return (checkpoint, target.Statuses.Any(status => status.ClassId == 24215 && status.CreatorId == 1),
            target.Statuses.Any(status => status.ClassId == 24215 && status.CreatorId == 2));
    }

    private static string CastSummary(FightBattleStepEvent step)
        => $"{step.ActorId}: {string.Join(",", step.SkillCasts.Select(cast => cast.SkillEntityId))}";

    private static FightBattleUnitState Unit(int id, int side, int x, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = 5, MoveMask = 5,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = speed;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", speed }, { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 },
            { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }

    private static void Check(bool condition, string details)
    {
        if (!condition) throw new InvalidOperationException("Ice crystal: " + details);
    }
}
