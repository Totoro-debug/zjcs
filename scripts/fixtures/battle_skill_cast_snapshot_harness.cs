using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleSkillCastSnapshotHarness
{
    public static void Run()
    {
        var first = new FightBattleStatusState { InstanceId = 11, ClassId = 24022,
            StackCount = 2, SharedStackId = 25300, SharedStackLimit = 5 };
        var second = new FightBattleStatusState { InstanceId = 12, ClassId = 24023,
            StackCount = 1, SharedStackId = 25300, SharedStackLimit = 5 };
        var source = new FightBattleUnitState { Id = 1, X = 4, Y = 7,
            Direction = 1, HasFlipX = true, FlipX = false };
        source.Statuses.Add(first);
        source.Statuses.Add(second);
        source.Statuses.Add(new FightBattleStatusState { InstanceId = 13,
            ClassId = 99001, StackCount = 3 });
        source.Statuses.Add(new FightBattleStatusState { InstanceId = 14,
            ClassId = 99001, StackCount = 1 });
        source.Statuses.Add(null);
        var target = new FightBattleUnitState { Id = 2, X = 8, Y = 9 };
        var targets = new List<FightBattleUnitState> { target };
        var skill = new FightBattleSkillDefinition { SkillTargetType = 1, FlipSource = true };
        var before = FightBattleSkillCastPresentation.Capture(source, skill, targets);
        Require(before.SourceStatuses.Count == 4
            && before.SourceStatuses[0].InstanceId == 11
            && before.SourceStatuses[1].ClassId == 24023
            && before.SourceStatuses[2].ClassId == before.SourceStatuses[3].ClassId
            && before.SourceStatuses[2].StackCount == 3
            && before.SourceStatuses[3].StackCount == 1,
            "Snapshot lost ordered original status identities or duplicate classes.");
        Require(before.SourceSharedStacks.Count == 1
            && before.SourceSharedStacks[0].ShareId == 25300
            && before.SourceSharedStacks[0].StackCount == 3,
            "Snapshot did not capture the live shared total exactly once.");

        // Model consumption and a later cast before the first cast is rendered.
        first.StackCount = 0;
        second.StackCount = 0;
        source.X = 20;
        source.Direction = 3;
        source.FlipX = true;
        target.X = 15;
        var after = FightBattleSkillCastPresentation.Capture(source, skill, targets);
        Require(after.SourceSharedStacks.Count == 0,
            "Zero shared totals must remain absent, as in original ChangeShareStack.");
        Require(after.SourceStatuses[0].StackCount == 0
            && before.SourceStatuses[0].StackCount == 2
            && before.SourceSharedStacks[0].StackCount == 3,
            "Successive casts share mutable status state.");
        Require(before.SourceX == 4 && before.SourceY == 7
            && before.TargetX == 8 && before.FacingIndex == 1 && !before.FlipX
            && after.SourceX == 20 && after.TargetX == 15
            && after.FacingIndex == 3 && after.FlipX,
            "Cast status capture altered original coordinates or explicit facing.");
        source.Statuses.Clear();
        Require(before.SourceStatuses.Count == 4 && after.SourceStatuses.Count == 4,
            "Ending statuses mutated an already captured cast.");

        source.Hp = source.MaxHp = 100;
        source.Turn.Id = source.Id;
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 31,
            Sequence = 1, Counter = 1, Grid = new FightBattleGrid { Width = 32, Height = 20 },
            ResultRules = new FightBattleResultRules() };
        checkpoint.Units.Add(source);
        var step = new FightBattleStepEvent { ActorId = source.Id };
        step.SkillCasts.Add(new FightBattleSkillCastEvent { ActorId = source.Id,
            SkillEntityId = 13200, Presentation = before });
        step.SkillCasts.Add(new FightBattleSkillCastEvent { ActorId = source.Id,
            SkillEntityId = 13200, Presentation = after });
        checkpoint.Receipts.Add(new FightBattleStepReceipt { Sequence = 1, Counter = 1, Event = step });
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        var casts = restored.Receipts[0].Event.SkillCasts;
        Require(restored.Units[0].Statuses.Count == 0
            && casts[0].Presentation.SourceStatuses.Count == 4
            && casts[0].Presentation.SourceStatuses[0].StackCount == 2
            && casts[0].Presentation.SourceStatuses[2].InstanceId == 13
            && casts[0].Presentation.SourceStatuses[3].InstanceId == 14
            && casts[0].Presentation.SourceSharedStacks[0].StackCount == 3
            && casts[1].Presentation.SourceSharedStacks.Count == 0
            && casts[0].Presentation.SourceX == 4 && casts[1].Presentation.SourceX == 20,
            "Current-game checkpoint lost the settled cast's independent presentation snapshot.");
        casts[0].Presentation.SourceStatuses[0].StackCount = 99;
        Require(before.SourceStatuses[0].StackCount == 2,
            "Restored cast snapshot retained references into the saved battle.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
