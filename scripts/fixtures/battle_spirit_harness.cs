using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSpiritHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Battle spirit: " + message); }

    private static long Prop(FightBattleUnitState unit, int id)
        => unit.Properties.Where(p => p.Id == id).Sum(p => p.Value);

    private static FightBattleUnitState Unit(int id, int side, int speed)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Level = 20, SubRank = 1,
            X = 5 + id, Y = 5, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = speed;
        foreach (var p in new[] { new FightPropertyValue { Id = 1, Value = 1000 },
            new FightPropertyValue { Id = 2, Value = 10 }, new FightPropertyValue { Id = 3, Value = unit.MaxHp },
            new FightPropertyValue { Id = 4, Value = speed }, new FightPropertyValue { Id = 58, Value = 170 },
            new FightPropertyValue { Id = 109, Value = 7 } }) unit.Properties.Add(p);
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 0 } };
        var owner = Unit(1, 1, 1000); checkpoint.Units.Add(owner); checkpoint.Units.Add(Unit(2, 2, 900));
        var properties = catalog.CalculateSkillProps(11260, 2, 20, 1)
            .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList();
        var skill = catalog.CreateSkill(11260, 0, 10051, 2, 20, 1, properties);
        skill.LimitedTimes = 1; owner.Skills.Add(skill); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        Check(Prop(owner, 58) == 170 && Prop(owner, 109) == 7, "Status contributed before casting");
        var first = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        Check(first.SkillCasts.Count(c => c.SkillEntityId == 11260 && c.SkillItemId == 10051) == 1,
            "Original active slot did not cast");
        var status = owner.Statuses.Single(s => s.ClassId == 24214);
        Check(status.OwnerId == 1 && status.CreatorId == 1 && status.RootSkillId == 11260
            && status.ApplyingSkillItemId == 10051 && status.SourceSkillRank == 2 && status.SourceSkillLevel == 20,
            "Status lost its original owner or cultivation");
        Check(status.DurationRound == 4 && status.RoundTarget == 2 && status.RoundUpdateTiming == 1
            && !status.IsOpenStack && status.HasPropertyComponent && status.StartRound == 1 && status.CurDurationRound == 4,
            "Original four-owner-round property buff changed");
        var expected = catalog.CalculateSkillProps(24214, 2, 20, 1).ToDictionary(p => p.Key, p => p.Value);
        Check(expected.ContainsKey(58) && expected.ContainsKey(109) && expected[58] > 0 && expected[109] > 0
            && status.ResolvedProperties.Count == 2
            && status.ResolvedProperties.Single(p => p.Id == 58).Value == expected[58]
            && status.ResolvedProperties.Single(p => p.Id == 109).Value == expected[109]
            && Prop(owner, 58) == 170 + expected[58] && Prop(owner, 109) == 7 + expected[109],
            "Cultivated critical bonuses did not reach the owner");
        var sourceProps = status.SourceProperties.ToDictionary(p => p.Id, p => p.Value);
        var baseProps = owner.BaseProperties.ToDictionary(p => p.Id, p => p.Value);
        var reference = SourceStatusPropertyReference.ReferenceState.Create(24214, true, true, false, false, 1, owner.Hp,
            expected, sourceProps, baseProps, new Dictionary<int, long>(), new Dictionary<int, double>(), new int[0][]);
        foreach (var pair in new[] { Tuple.Create(status.StatusConfigProperties, reference.Config),
            Tuple.Create(status.StatusProperties, reference.Status), Tuple.Create(status.ResolvedProperties, reference.Final) })
            Check(pair.Item1.Count == pair.Item2.Count && pair.Item1.All(p => pair.Item2.TryGetValue(p.Id, out long value) && value == p.Value),
                "Status property pipeline differs from the original methods");
        var start = first.StatusViewEvents.Single(e => e.Status.ClassId == 24214 && e.Kind == FightBattleStatusViewEventKind.Start);
        Check(Math.Abs(start.Delay - .25f) < .00001f, "Original status start timing changed");
        string saved = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(saved);
        var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        int ends = 0, foreign = 0;
        for (int i = 0; i < 24; i++)
        {
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(restored.Sequence, restored.Counter + 1);
            saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(FightBattleCheckpointSerializer.Save(restored) == saved, "Cold continuation diverged");
            bool expired = owner.SkillState.Round >= 5;
            var alive = owner.Statuses.SingleOrDefault(s => s.ClassId == 24214);
            Check((alive == null) == expired && Prop(owner, 58) == (expired ? 170 : 170 + expected[58])
                && Prop(owner, 109) == (expired ? 7 : 7 + expected[109]), "Four-round expiry or property restoration changed");
            int stepEnds = step.StatusViewEvents.Count(e => e.Status.ClassId == 24214 && e.Kind == FightBattleStatusViewEventKind.End);
            ends += stepEnds;
            if (step.ActorId == 2 && !expired) foreign++;
            if (stepEnds > 0) Check(step.ActorId == 1 && step.ActorRound == 5 && stepEnds == 1, "Status ended on the wrong clock");
            if (alive != null) Check(FightStatusLifecycle.IconRemainingRound(alive, checkpoint.Units) == 5 - owner.SkillState.Round,
                "Original status icon remaining rounds changed");
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved, "Replay repeated a status transition");
            restored = FightBattleCheckpointSerializer.Load(saved);
            resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            if (expired && owner.SkillState.Round >= 6) break;
        }
        Check(ends == 1 && foreign >= 3 && owner.SkillState.Round >= 6, "Missing enemy rounds or settled expiry");
        RunCooldownCycles(catalog, formula, items, expected);
        return "Battle spirit passed: original critical bonuses " + expected[58] + "/" + expected[109]
            + ", source status properties, four owner rounds, expiry, cooldown cycles and cold/replay continuity.";
    }

    private static void RunCooldownCycles(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, Dictionary<int, long> expected)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 25, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 0 } };
        var owner = Unit(1, 1, 1000); checkpoint.Units.Add(owner); checkpoint.Units.Add(Unit(2, 2, 900));
        var properties = catalog.CalculateSkillProps(11260, 2, 20, 1)
            .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList();
        var skill = catalog.CreateSkill(11260, 0, 10051, 2, 20, 1, properties);
        owner.Skills.Add(skill);
        long cooldown = properties.Single(value => value.Id == formula.PropertyId("CD")).Value;
        owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, cooldown));
        Check(skill.LimitedTimes == -1 && owner.SkillState.Slots[0].Cooldown == 6, "Original cooldown changed");
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var casts = new List<int>(); var ends = new List<int>();
        for (int i = 0; i < 80 && owner.SkillState.Round < 16; i++)
        {
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            foreach (var cast in step.SkillCasts.Where(value => value.ActorId == 1 && value.SkillEntityId == 11260))
            {
                casts.Add(step.ActorRound);
                var active = owner.Statuses.Single(value => value.ClassId == 24214);
                Check(active.StartRound == step.ActorRound && active.CurDurationRound == 4
                    && active.SourceSkillRank == 2 && active.SourceSkillLevel == 20 && active.ApplyingSkillItemId == 10051,
                    "Natural recast lost its lifetime or cultivation");
            }
            ends.AddRange(step.StatusViewEvents.Where(value => value.Status.ClassId == 24214
                && value.Kind == FightBattleStatusViewEventKind.End).Select(value => step.ActorRound));
            bool alive = owner.Statuses.Any(value => value.ClassId == 24214);
            Check(Prop(owner, 58) == (alive ? 170 + expected[58] : 170)
                && Prop(owner, 109) == (alive ? 7 + expected[109] : 7), "Repeated casts duplicated or lost bonuses");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            checkpoint = FightBattleCheckpointSerializer.Load(saved);
            executor = FightBattleExecutor.Restore(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            owner = checkpoint.Units.Single(value => value.Id == 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == saved, "Restoring cooldown cycle changed checkpoint");
        }
        Check(casts.SequenceEqual(new[] { 1, 8, 15 }) && ends.SequenceEqual(new[] { 5, 12 })
            && owner.SkillState.Slots[0].ReleasedTimes == 3, "Initial cooldown or subsequent natural recasts changed");
    }
}
