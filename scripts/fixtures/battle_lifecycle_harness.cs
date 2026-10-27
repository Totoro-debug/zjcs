using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleLifecycleHarness
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static FightBattleUnitState Unit(int id, int side, int type, long hp = 100)
    {
        return new FightBattleUnitState
        {
            Id = id, Side = side, CharacterType = type, Hp = hp, MaxHp = 100,
            Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = 100 }
        };
    }

    private static FightBattleCheckpoint State(int kind)
    {
        var state = new FightBattleCheckpoint { StageId = 1, RandomSeed = 1,
            ResultRules = new FightBattleResultRules { Kind = kind } };
        state.Units.Add(Unit(1, 1, 1));
        state.Units.Add(Unit(2, 2, kind == FightBattleResultRules.Duel ? 1 : 2));
        return state;
    }

    public static void Run()
    {
        // Realistic messages may share timestamps or have a later timestamp followed by an
        // already-due message. NormalPlaybackStrategy preserves StepList order in both cases.
        var times = new[] { -.1, .5, .5, .2, 1.0 };
        var dispatched = new List<double>();
        var playback = new FightProcessPlayback<double>(times, value => value, dispatched.Add);
        playback.Tick(0);
        Require(dispatched.Count == 1, "Initial due message was delayed.");
        playback.Tick(.25);
        Require(dispatched.Count == 1, "A later list entry bypassed the waiting message.");
        playback.Tick(.25);
        Require(dispatched.Count == 4 && dispatched[1] == .5 && dispatched[3] == .2,
            "Equal-time or overdue messages changed original list order.");
        playback.Tick(0);
        Require(dispatched.Count == 4, "Zero time advanced a future message.");
        playback.Cancel();
        playback.Tick(10);
        Require(dispatched.Count == 4 && playback.Cancelled, "Disposed playback dispatched stale work.");
        var resumed = new FightProcessPlayback<double>(times, value => value, dispatched.Add, 4);
        resumed.Tick(.9);
        Require(dispatched.Count == 4, "startStep incorrectly reset relative message times.");
        resumed.Tick(.1);
        resumed.Tick(10);
        Require(dispatched.Count == 5 && resumed.Complete, "Last message was lost or dispatched twice.");
        FightProcessPlayback<int> interrupted = null;
        int count = 0;
        interrupted = new FightProcessPlayback<int>(new[] {0, 0, 0}, _ => 0, _ =>
        { count++; interrupted.Cancel(); });
        interrupted.Tick(0);
        Require(count == 1, "Cancellation inside dispatch failed to stop the batch.");

        var first = new FightBattlePreparationRange(new FightGridPoint(5, 5), 4, 2,
            new FightGridPoint(2, 1));
        Require(first.Contains(new FightGridPoint(3, 4)) && first.Contains(new FightGridPoint(6, 5))
            && !first.Contains(new FightGridPoint(7, 5)), "Even-size source range was recentered.");
        Require(FightBattlePreparationRange.RangeCenterOffset(4) == -.5f
            && FightBattlePreparationRange.RangeCenterOffset(3) == 0, "Source half-cell display offset differs.");
        FightGridPoint point;
        var ranges = new[] { first };
        Require(FightBattlePreparationRange.TryGetClosest(ranges, new FightGridPoint(4, 4),
            candidate => !candidate.Equals(new FightGridPoint(4, 4)), out point)
            && point.Equals(new FightGridPoint(3, 4)), "Equal-distance tie did not preserve Y/X list order.");
        var second = new FightBattlePreparationRange(new FightGridPoint(20, 20), 1, 1,
            new FightGridPoint());
        Require(FightBattlePreparationRange.TryGetClosest(new[] {first, second}, new FightGridPoint(5, 5),
            _ => true, out point) && point.Equals(new FightGridPoint(20, 20)),
            "Current ChangeRangeTool last-matching-area branch was normalized away.");
        Require(!FightBattlePreparationRange.TryGetClosest(ranges, point, _ => false, out point),
            "An entirely blocked range returned a fabricated legal grid.");

        var preparing = State(FightBattleResultRules.Standard);
        preparing.Grid = new FightBattleGrid { Width = 8, Height = 8 };
        preparing.Units[0].X = 3; preparing.Units[0].Y = 4;
        preparing.Units.Add(Unit(3, 1, 4)); preparing.Units[2].X = 4; preparing.Units[2].Y = 4;
        preparing.Units.Add(Unit(4, 1, 4)); preparing.Units[3].X = 5; preparing.Units[3].Y = 4;
        var positions = new FightBattlePreparationPositions(preparing, ranges, new[] {1, 3});
        Require(positions.TryMove(1, new FightGridPoint(4, 4), FightBattlePhase.Preparing, false)
            && preparing.Units[2].X == 3 && preparing.Units[0].X == 4,
            "Owned NPC position swap was not applied atomically.");
        Require(!positions.TryMove(1, new FightGridPoint(5, 4), FightBattlePhase.Preparing, false)
            && preparing.Units[0].X == 4, "Uncontrolled NPC occupancy was moved.");
        Require(!positions.TryMove(1, new FightGridPoint(4, 5), FightBattlePhase.Play, false)
            && !positions.TryMove(1, new FightGridPoint(4, 5), FightBattlePhase.Preparing, true),
            "Started or ready preparation accepted a position request.");
        preparing.Grid.BlockedPositions.Add(new FightGridPoint(4, 5));
        Require(!positions.TryMove(1, new FightGridPoint(4, 5), FightBattlePhase.Preparing, false),
            "Preparation allowed a blocked destination.");
        preparing.PreparationPending = true;
        preparing.PreparationSecondsRemaining = 12.5;
        var preparingRestored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(preparing));
        Require(preparingRestored.Units[0].X == 4 && preparingRestored.Units[2].X == 3,
            "Checkpoint restore lost swapped positions.");
        Require(preparingRestored.PreparationPending && preparingRestored.PreparationSecondsRemaining == 12.5,
            "Checkpoint restore lost preparation phase or countdown.");

        var state = State(FightBattleResultRules.Standard);
        state.Units[1].Hp = 0;
        foreach (int type in new[] {0, 3, 5, 6, 7, 8, 9}) state.Units.Add(Unit(10 + type, 2, type));
        var outcome = state.ResultRules.Evaluate(state, 1);
        Require(outcome.Won && outcome.EnemyHp == 0 && outcome.EnemyMaxHp == 100,
            "Official excluded character types blocked clear or inflated result HP.");
        state.Units.Add(Unit(30, 2, 4));
        Require(!state.ResultRules.Evaluate(state, 1).Finished, "A living enemy NPC was excluded.");
        state.Units[0].Hp = 0;
        Require(state.ResultRules.Evaluate(state, 1).Reason == 2, "Player death did not fail standard combat.");
        var guard = State(FightBattleResultRules.Guard);
        guard.Units.Add(Unit(3, 1, 4, 0));
        guard.ResultRules.GuardUnitIds.Add(3);
        Require(guard.ResultRules.Evaluate(guard, 1).Reason == FightBattleResultRules.GuardFailedReason,
            "Protected-unit death did not fail guard combat.");
        guard.Units[2].Hp = 100;
        guard.Units[0].Hp = 0;
        Require(guard.ResultRules.Evaluate(guard, 1).Reason == 2, "Player death did not fail guard combat.");

        var duel = State(FightBattleResultRules.Duel);
        duel.Units.Add(Unit(3, 1, 1));
        duel.Units[0].Hp = 0;
        Require(!duel.ResultRules.Evaluate(duel, 1).Finished, "Duel ended while another allied player lived.");
        duel.Units[1].Hp = 0;
        Require(duel.ResultRules.Evaluate(duel, 1).Won, "Duel did not finish when all opposing players died.");
        duel.Units[2].Hp = 0;
        duel.Outcome = duel.ResultRules.Evaluate(duel, 1);
        duel.Finished = duel.Outcome.Finished;
        Require(duel.Outcome.Invalid && !duel.Outcome.CanSettle && !duel.Outcome.Won,
            "Duel double knockout entered a consumable loss/win settlement.");
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(duel));
        Require(restored.Outcome.Invalid && !restored.Outcome.CanSettle
            && restored.ResultRules.Kind == FightBattleResultRules.Duel,
            "Checkpoint restore lost invalid-duel or result policy state.");
        var legacy = State(FightBattleResultRules.Standard);
        legacy.ResultRules = null;
        Require(FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(legacy)).ResultRules == null,
            "Legacy checkpoint silently changed result policy.");

        // Exercise the actual executor and wave consumer, rather than just the result predicate.
        var fight = State(FightBattleResultRules.Standard);
        fight.Units[1].X = 1;
        fight.Units[1].Hp = 10;
        fight.Units[1].Turn.Hp = 10;
        fight.Units[1].Turn.Speed = 1;
        fight.Units.Add(Unit(3, 2, 3));
        fight.Units[2].X = 5;
        var skill = new FightBattleSkillDefinition { EntityId = 100, Slot = 0, TargetType = 1 };
        skill.Range.Add(new FightGridPoint(1, 0));
        skill.Hits.Add(new FightBattleHitDefinition { EntityId = 200, HasCustomDamage = true,
            CustomDamage = -100, DamageType = 8 });
        fight.Units[0].Skills.Add(skill);
        fight.Units[0].SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        var executor = FightBattleExecutor.Begin(fight, new FightDamageFormula(
            new FightDamagePropertySchema(), new FightDamageFormulaConfig()), new FightDamageSettlement());
        executor.Step(0, 1);
        Require(fight.Finished && fight.Outcome.Won && fight.Units[2].Hp == 100,
            "Executor still waited for an excluded enemy summon after clear.");
    }
}
