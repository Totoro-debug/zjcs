using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSageRoundSecretsHarness
{
    private static readonly int[] Gains = { 24396, 24398, 24399 };

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 1, 4, 7 })
        foreach (int level in new[] { 1, 2, 10 })
        {
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 64; seed++)
            {
                var state = State(seed);
                var actor = Unit(1, 1, 1000, 1200, 1, formula);
                var enemy = Unit(2, 2, 1000, 1000, 2, formula);
                Assemble(catalog, items, actor, 10611, rank, level);
                Populate(state, actor, enemy);
                var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
                Require(!actor.Statuses.Any(value => Gains.Contains(value.ClassId)), "Dark Star fired before the first personal turn.");
                executor.Step(state.Sequence, state.Counter + 1);
                var gain = actor.Statuses.Single(value => Gains.Contains(value.ClassId));
                seen.Add(gain.ClassId);
                Require(gain.CreatorId == actor.Id && gain.ApplyingSkillItemId == 10611
                    && gain.SourceSkillRank == rank && gain.SourceSkillLevel == level && gain.DurationRound == 1,
                    "Dark Star lost its cultivated source or duration.");
                PropertiesMatch(catalog, gain, rank, level);
                VerifyNextRound(executor, state, actor, formula, catalog, items);
                Require(actor.Statuses.Count(value => Gains.Contains(value.ClassId)) == 1
                    && actor.Statuses.Single(value => Gains.Contains(value.ClassId)).StartRound == actor.SkillState.Round,
                    "Dark Star retained an expired gain or failed to obtain its next gain.");
                cases++;
            }
            Require(seen.SetEquals(Gains), "The random streams omitted an official gain.");
            for (int excluded = 0; excluded <= 3; excluded++)
            {
                Blessing(catalog, formula, items, rank, level, excluded);
                cases++;
            }
        }
        return "Sage round secrets passed: " + cases + " cultivation, random-stream, role-filter, existing-blessing and restored-lifetime cases.";
    }

    private static void Blessing(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int excluded)
    {
        var state = State(73);
        var actor = Unit(1, 1, 1000, 1200, 1, formula);
        var enemy = Unit(2, 2, 99999, 1000, 2, formula);
        var ally = Unit(3, 1, 3000, 1000, 1, formula);
        var clone = Unit(4, 1, 5000, 1000, 4, formula);
        var summon = Unit(5, 1, 9999, 1000, 3, formula);
        Assemble(catalog, items, actor, 10610, rank, level);
        Populate(state, actor, enemy, ally, clone, summon);
        foreach (var target in new[] { clone, ally, actor }.Take(excluded))
            FightStatusLifecycle.Apply(target, catalog.CreateStatus(24395, target.Id, ally.Id,
                100000 + target.Id, 25119, 10610, rank, level, 8), 0, 100000 + target.Id);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        int expected = new[] { clone.Id, ally.Id, actor.Id, 0 }[excluded];
        var casts = step.SkillCasts.Where(value => value.SkillEntityId == 12180).ToArray();
        if (expected == 0)
        {
            Require(casts.Length == 0 && actor.Statuses.Single(value => value.ClassId == 25119).RoundStartTriggeredCount == 0,
                "Blessing recast on already blessed roles.");
            return;
        }
        Require(casts.Length == 1 && casts[0].TargetIds.SequenceEqual(new[] { expected }),
            "Blessing did not filter existing blessings, summons and enemies before ranking allied role attack.");
        var recipient = state.Units.Single(value => value.Id == expected);
        var gain = recipient.Statuses.Single(value => value.ClassId == 24395);
        Require(gain.CreatorId == actor.Id && gain.ApplyingSkillItemId == 10610 && !gain.CanDispersed
            && gain.SourceSkillRank == rank && gain.SourceSkillLevel == level && gain.DurationRound == 1,
            "Blessing lost its cultivated source, duration or dispel immunity.");
        PropertiesMatch(catalog, gain, rank, level);
        bool recipientActed = recipient.Id == actor.Id;
        int round = actor.SkillState.Round;
        VerifyNextRound(executor, state, actor, formula, catalog, items, () =>
        {
            if (actor.SkillState.Round != round || recipient.SkillState.Round == 0) return;
            recipientActed = true;
            Require(recipient.Statuses.Any(value => value.ClassId == 24395 && value.CreatorId == actor.Id),
                "Blessing expired during its recipient's turn.");
        });
        var refreshed = state.Units.SelectMany(value => value.Statuses)
            .Where(value => value.ClassId == 24395 && value.CreatorId == actor.Id).ToArray();
        Require(recipientActed && refreshed.Length == 1 && refreshed[0].StartRound == actor.SkillState.Round,
            "Blessing survived its creator's boundary or failed to select the next eligible role.");
    }

    private static void VerifyNextRound(FightBattleExecutor executor, FightBattleCheckpoint state,
        FightBattleUnitState actor, FightDamageFormula formula, FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, Action observe = null)
    {
        string saved = FightBattleCheckpointSerializer.Save(state);
        var copy = FightBattleCheckpointSerializer.Load(saved);
        var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        Require(restored.Step(copy.Sequence, copy.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(copy) == saved, "A restored personal-round receipt settled twice.");
        int round = actor.SkillState.Round;
        for (int count = 0; count < 12 && actor.SkillState.Round == round; count++)
        {
            executor.Step(state.Sequence, state.Counter + 1);
            restored.Step(copy.Sequence, copy.Counter + 1);
            Require(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                "Restoring changed the personal-round random stream, selected role or status lifetime.");
            observe?.Invoke();
        }
        Require(actor.SkillState.Round > round, "The fixture did not reach the next personal turn.");
    }

    private static void PropertiesMatch(FightOfficialCombatCatalog catalog, FightBattleStatusState gain, int rank, int level)
    {
        foreach (var property in catalog.CalculateSkillProps(gain.ClassId, rank, level, 8))
            Require(gain.StatusProperties.Single(value => value.Id == property.Key).Value == property.Value,
                "The status omitted or changed an original cultivated property.");
    }

    private static void Assemble(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightBattleUnitState actor, int item, int rank, int level)
        => catalog.AssembleUnit(actor, null, catalog.CreatePassiveStatusBindings(actor, items,
            new[] { new FightOfficialPassiveSkillBinding { ItemId = item, Rank = rank, Level = level, SubRank = 8 } }));

    private static void Populate(FightBattleCheckpoint state, params FightBattleUnitState[] units)
    {
        state.Units.AddRange(units);
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(2);
        state.Waves.Add(wave);
    }

    private static FightBattleCheckpoint State(int seed) => new FightBattleCheckpoint
    { StageId = 1, MaxRound = 8, RandomSeed = seed * 1000003 + 71, Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };

    private static FightBattleUnitState Unit(int id, int side, long attack, int speed, int type, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = type, Profession = 17,
            SubRank = 8, Level = 70, X = id + 3, Y = side + 4, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = speed;
        foreach (var pair in new Dictionary<string, long> { { "Attack", attack }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", speed }, { "CritAvoidPercent", 10000 },
            { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
