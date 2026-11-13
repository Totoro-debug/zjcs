using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleArcShieldHarness
{
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    static long Prop(FightBattleUnitState unit, int id)
        => unit.Properties.Where(p => p.Id == id).Sum(p => p.Value);

    static FightBattleUnitState Unit(int id, int side, int speed)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Level = 20, SubRank = 1,
            X = 5 + id, Y = 5, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000
        };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = speed;
        foreach (var p in new[] {
            new FightPropertyValue { Id = 1, Value = 1000 },
            new FightPropertyValue { Id = 2, Value = 10 },
            new FightPropertyValue { Id = 3, Value = unit.MaxHp },
            new FightPropertyValue { Id = 4, Value = speed },
            new FightPropertyValue { Id = 62, Value = 170 },
            new FightPropertyValue { Id = 110, Value = 7 }
        }) unit.Properties.Add(p);
        return unit;
    }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var checkpoint = new FightBattleCheckpoint {
            StageId = 1001, MaxRound = 20, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 0 }
        };
        var owner = Unit(1, 1, 1000);
        checkpoint.Units.Add(owner); checkpoint.Units.Add(Unit(2, 2, 900));
        var properties = catalog.CalculateSkillProps(10760, 2, 20, 1)
            .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList();
        var skill = catalog.CreateSkill(10760, 0, 10024, 2, 20, 1, properties);
        // One-shot diagnostic: keep the real hit graph and prevent a later recast
        // from renewing the status whose original expiry is under examination.
        skill.LimitedTimes = 1;
        owner.Skills.Add(skill); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var executor = FightBattleExecutor.Begin(checkpoint, formula,
            new FightDamageSettlement(), catalog, items);
        Check(Prop(owner, 62) == 170 && Prop(owner, 110) == 7, "Status contributed before casting");

        var first = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        Check(first.SkillCasts.Count(c => c.SkillEntityId == 10760 && c.SkillItemId == 10024) == 1,
            "Original arc shield was not released through the active slot");
        var status = owner.Statuses.Single(s => s.ClassId == 24030);
        Check(status.OwnerId == 1 && status.CreatorId == 1 && status.RootSkillId == 10760
            && status.ApplyingSkillItemId == 10024 && status.SourceSkillRank == 2
            && status.SourceSkillLevel == 20, "Status lost its original cultivation/source");
        Check(status.DurationRound == 3 && status.RoundTarget == 2 && status.RoundUpdateTiming == 1
            && !status.IsOpenStack && status.HasPropertyComponent && status.ShieldKind == 0,
            "Original property buff was changed into a shield or another clock");
        Check(status.StartRound == 1 && status.CurDurationRound == 3,
            "Status did not start on the casting owner's current personal round");
        Check(Prop(owner, 62) == 1970 && Prop(owner, 110) == 25
            && status.ResolvedProperties.Count == 2
            && status.ResolvedProperties.Single(p => p.Id == 62).Value == 1800
            && status.ResolvedProperties.Single(p => p.Id == 110).Value == 18,
            "Rank 2 level 20 original block properties did not reach the owner");
        var sourceProps = status.SourceProperties.ToDictionary(p => p.Id, p => p.Value);
        var baseProps = owner.BaseProperties.ToDictionary(p => p.Id, p => p.Value);
        // Execute the original InitProps/GetStatusProps/GetProps bodies with
        // explicit entity/data adapters, independently of the migrated pipeline.
        var reference = SourceStatusPropertyReference.ReferenceState.Create(24030,
            true, true, false, false, 1, owner.Hp,
            new Dictionary<int, long> { { 62, 1800 }, { 110, 18 } }, sourceProps, baseProps,
            new Dictionary<int, long>(), new Dictionary<int, double>(), new int[0][]);
        foreach (var pair in new[] {
            Tuple.Create(status.StatusConfigProperties, reference.Config),
            Tuple.Create(status.StatusProperties, reference.Status),
            Tuple.Create(status.ResolvedProperties, reference.Final)
        })
            Check(pair.Item1.Count == pair.Item2.Count
                && pair.Item1.All(p => pair.Item2.TryGetValue(p.Id, out long value) && value == p.Value),
                "Migrated property buff differs from original status property methods");
        var start = first.StatusViewEvents.Single(e => e.Status.ClassId == 24030
            && e.Kind == FightBattleStatusViewEventKind.Start);
        Check(Math.Abs(start.Delay - .2f) < .00001f && start.Status.ShieldMaximum == 0,
            "Original 10761 hit delay or property-buff presentation changed");

        // Compare the full uninterrupted state with a fresh executor restored at
        // every boundary, including immediately before and after natural expiry.
        string saved = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(saved);
        var resumed = FightBattleExecutor.Restore(restored, formula,
            new FightDamageSettlement(), catalog, items);
        Check(FightBattleCheckpointSerializer.Save(restored) == saved,
            "Restoring an initialized property buff changed the checkpoint");
        int ends = 0, foreignRounds = 0;
        for (int i = 0; i < 20; i++)
        {
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(restored.Sequence, restored.Counter + 1);
            saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(FightBattleCheckpointSerializer.Save(restored) == saved,
                "Fresh-executor continuation diverged at step " + checkpoint.Counter);
            bool expired = owner.SkillState.Round >= 4;
            var alive = owner.Statuses.SingleOrDefault(s => s.ClassId == 24030);
            Check((alive == null) == expired, "Status expiry did not follow three owner rounds");
            Check(Prop(owner, 62) == (expired ? 170 : 1970)
                && Prop(owner, 110) == (expired ? 7 : 25),
                "Expired buff retained properties or live buff lost them");
            Check(step.SkillCasts.All(c => c.SkillEntityId != 10760), "One-shot diagnostic recast");
            int stepEnds = step.StatusViewEvents.Count(e => e.Status.ClassId == 24030
                && e.Kind == FightBattleStatusViewEventKind.End);
            ends += stepEnds;
            if (step.ActorId == 2 && !expired) foreignRounds++;
            if (stepEnds > 0)
                Check(step.ActorId == 1 && step.ActorRound == 4 && stepEnds == 1,
                    "Status ended on another unit's clock or emitted duplicate end events");
            if (alive != null)
                Check(FightStatusLifecycle.IconRemainingRound(alive, checkpoint.Units)
                    == 4 - owner.SkillState.Round, "Original status icon remaining rounds differ");
            string beforeReplay = saved;
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == beforeReplay,
                "Replaying a receipt repeated a property/status transition");
            restored = FightBattleCheckpointSerializer.Load(saved);
            resumed = FightBattleExecutor.Restore(restored, formula,
                new FightDamageSettlement(), catalog, items);
            if (expired && owner.SkillState.Round >= 5) break;
        }
        Check(ends == 1 && foreignRounds >= 2 && owner.SkillState.Round >= 5,
            "Did not cover intervening enemy turns, expiry and settled continuation");
    }
}
