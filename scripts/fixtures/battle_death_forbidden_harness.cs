using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

public static class BattleDeathForbiddenRegression
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        Clock();
        foreach (bool instant in new[] { false, true })
        foreach (bool retainedReason in new[] { false, true })
            Check(Exercise(catalog, formula, items, instant, retainedReason, false)
                == Exercise(catalog, formula, items, instant, retainedReason, true),
                "Settled death/revival changed after checkpoint restoration.");
    }

    private static void Clock()
    {
        for (int divisions = 1; divisions <= 128; divisions++)
        {
            var cp = new FightBattleCheckpoint { Sequence = 2, Counter = 3 };
            var final = new FightBattleUnitState { Id = 7, Hp = 1, MaxHp = 100 };
            var step = new FightBattleStepEvent { HasUnitLifeEvents = true };
            var death = new FightBattleUnitLifeEvent { UnitId = 7, IsDead = true,
                BeforeHp = 90, BeforeMaxHp = 100, Hp = 0, MaxHp = 100, Delay = .375f };
            step.UnitLifeEvents.Add(death);
            step.UnitLifeEvents.Add(new FightBattleUnitLifeEvent { UnitId = 7, BeforeHp = 0,
                BeforeMaxHp = 100, Hp = 1, MaxHp = 100, Delay = .75f });
            var delivered = new List<bool>();
            var clock = new OfficialBattleUnitLifePlayback(e => delivered.Add(e.IsDead));
            clock.Enqueue(cp, step);
            Check(clock.Project(final).Hp == 90 && final.Hp == 1, "Life projection changed the settled checkpoint.");
            death.Hp = 999; death.Delay = 999;
            for (int i = 0; i < divisions; i++) { clock.AdvanceTimers(.375f / divisions); clock.DispatchDue(); }
            // Floating point subdivision can retain a fraction of one ULP.
            clock.AdvanceTimers(.00001f); clock.DispatchDue();
            Check(delivered.SequenceEqual(new[] { true }) && clock.Project(final).Hp == 0,
                "Confirmed death timing, frozen receipt or intermediate corpse projection differs.");
            clock.Enqueue(cp, step);
            clock.AdvanceTimers(.375f); clock.DispatchDue();
            Check(delivered.SequenceEqual(new[] { true, false }) && ReferenceEquals(clock.Project(final), final),
                "Revival failed to release the temporary projection or repeated a receipt.");
            clock.Enqueue(cp, step); clock.AdvanceTimers(1); clock.DispatchDue();
            Check(delivered.Count == 2, "A repeated completed step republished Die.");
            cp.Counter++; clock.Enqueue(cp, step); clock.Cancel(7); clock.AdvanceTimers(2000); clock.DispatchDue();
            Check(delivered.Count == 2 && ReferenceEquals(clock.Project(final), final), "Removal retained future life events.");
            clock.Clear();
            clock.Enqueue(cp, new FightBattleStepEvent { UnitLifeEvents = step.UnitLifeEvents });
            Check(float.IsPositiveInfinity(clock.NextDelay), "Legacy receipts inferred unconfirmed Die transitions.");
        }
        Console.WriteLine("Unit life clock passed: 128 subdivisions, frozen projections, death/revival order, repeat receipts, removal and legacy compatibility.");
    }

    private static string Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool instant, bool retainedReason, bool restore)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 10, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 8, DefaultGroundType = 2 } };
        for (int id = 1; id <= 3; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2,
                CharacterType = 1, X = id + 2, Y = 2, Hp = id == 2 ? 10 : 1000,
                MaxHp = 1000, LastMaxHp = 1000, Level = 1, SubRank = 1 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp;
            unit.Turn.Speed = 1100 - id * 100;
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
            var skill = new FightBattleSkillDefinition { EntityId = 990600 + id, ItemId = 990600 + id,
                Slot = 0, TargetType = 1, ActionDuration = 1, Duration = 1, CanTriggerChild = true };
            for (int x = -4; x <= 4; x++) skill.Range.Add(new FightGridPoint(x, 0));
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990700 + id,
                DamageId = 990700 + id, DamageType = 8, HasCustomDamage = true, CustomDamage = 20, Delay = .375f });
            unit.Skills.Add(skill);
            unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.Add(unit);
        }
        var victim = cp.Units[1];
        var marker = catalog.CreateStatus(25117, 2, 2, 9925117, 10110);
        marker.StackCount = 4;
        marker.ForbiddenType = retainedReason ? FightStatusLifecycle.ForbiddenStatusTrigger : 0;
        victim.Statuses.Add(marker);
        var hp = catalog.CreateStatus(60410, 2, 2, 9960410, 10110);
        // Controlled variation of the shipped death HP listener: use the real 24233
        // BackToLife component to observe same-hit revival. Production data is unchanged.
        hp.ForbiddenOnApplicatorDie = true;
        hp.HpLessThanMaxCount = 1;
        if (instant)
        {
            hp.HpLessThanStatuses.Clear();
            hp.HpLessThanStatuses.Add(new FightBattleStatusStateTrigger
            { EntityId = 24233, ApplyTarget = 2, BasePercent = 1 });
        }
        victim.Statuses.Add(hp);
        var exempt = catalog.CreateStatus(24040, 2, 2, 9924040, 10110);
        victim.Statuses.Add(exempt);
        var wave = new FightBattleWaveState { Index = 0 };
        wave.UnitIds.Add(2); wave.UnitIds.Add(3); cp.Waves.Add(wave);
        int transitions = 0;
        var settlement = new FightDamageSettlement();
        bool observed = false;
        settlement.SourceHit += damage =>
        {
            if (observed || damage.TargetId != 2) return;
            observed = true;
            var original = victim.StatusForbiddenChanged;
            victim.StatusForbiddenChanged = (owner, status, wasForbidden) =>
            {
                if (status == marker)
                {
                    transitions++;
                    Check(owner.Hp == 0 && hp.HpLessThanTriggeredCount == 1,
                        "Death was published before the hit's HP trigger finished.");
                }
                original?.Invoke(owner, status, wasForbidden);
            };
        };
        var runner = FightBattleExecutor.Begin(cp, formula, settlement, catalog, items);
        var firstStep = runner.Step(0, 1).Event;
        Check(firstStep.HasUnitLifeEvents, "A new step did not publish life receipt capability.");
        var deaths = firstStep.UnitLifeEvents.Where(e => e.UnitId == 2).ToArray();
        Check(deaths.Length == (instant ? 0 : 1), "Raw hit HP produced a false death before immediate revival.");
        if (!instant)
            Check(deaths[0].IsDead && deaths[0].BeforeHp == 10 && deaths[0].Hp == 0
                && deaths[0].MaxHp == victim.MaxHp && deaths[0].Delay > 0
                && Math.Abs(deaths[0].Delay - firstStep.DamageEvents.Single(e => e.TargetId == 2).Delay) < .00001f,
                "Confirmed death lost its before-state or actual delayed hit time.");
        Check(observed && hp.HpLessThanTriggeredCount == 1, "Actual fatal hit/HP listener did not execute.");
        Check(victim.Hp == (instant ? 1 : 0), "Immediate BackToLife did not settle before death confirmation.");
        int other = retainedReason ? FightStatusLifecycle.ForbiddenStatusTrigger : 0;
        Check(marker.ForbiddenType == (other | (instant ? 0 : FightStatusLifecycle.ForbiddenApplicatorDie))
            && transitions == (!instant && !retainedReason ? 1 : 0),
            "Instant revival emitted a death transition or lost an independent reason.");
        Check(exempt.ForbiddenType == 0 && marker.StackCount == 4,
            "Death disabled an exempt status or discarded existing stacks.");
        string first = FightBattleCheckpointSerializer.Save(cp);
        Check(runner.Step(0, 1).Replayed && FightBattleCheckpointSerializer.Save(cp) == first,
            "Receipt replay repeated death settlement.");
        if (instant) return first;

        if (restore)
        {
            cp = FightBattleCheckpointSerializer.Load(first);
            victim = cp.Units.Single(u => u.Id == 2);
            marker = victim.Statuses.Single(s => s.ClassId == 25117);
            runner = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), catalog, items);
        }
        else settlement = new FightDamageSettlement();
        // Drive the subsequent positive-HP transition during a real next-step hit.
        // The supplied input applies a new state to an already confirmed corpse,
        // then uses the unmodified official BackToLife state.
        bool revived = false;
        settlement = new FightDamageSettlement();
        settlement.SourceHit += _ =>
        {
            if (revived) return;
            revived = true;
            var added = FightStatusLifecycle.Apply(victim,
                catalog.CreateStatus(24059, 2, 1, 9924059, 10120), 0, 9924059);
            Check((added.ForbiddenType & FightStatusLifecycle.ForbiddenApplicatorDie) != 0,
                "A new status on a confirmed corpse ran without the death reason.");
            FightStatusLifecycle.Apply(victim,
                catalog.CreateStatus(24233, 2, 1, 9924233, 10120), 0, 9924233);
            Check(victim.Hp == 1 && marker.ForbiddenType == other
                && (added.ForbiddenType & FightStatusLifecycle.ForbiddenApplicatorDie) == 0,
                "Revival failed to clear only death before status-start consumers.");
        };
        runner = FightBattleExecutor.Restore(cp, formula, settlement, catalog, items);
        var nextStep = runner.Step(0, 2).Event;
        var revivals = nextStep.UnitLifeEvents.Where(e => e.UnitId == 2).ToArray();
        Check(revivals.Length == 1 && !revivals[0].IsDead && revivals[0].BeforeHp == 0 && revivals[0].Hp == 1,
            "Later official BackToLife did not publish one confirmed positive-HP transition.");
        Check(revived && victim.Hp == 1 && marker.StackCount == 4,
            "Next-step revival lost the preserved status state.");
        return FightBattleCheckpointSerializer.Save(cp);
    }
}
