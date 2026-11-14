using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class OfficialBattleStatusActionTimingCases
{
    public static FightBattleCheckpoint Exercise(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, bool approach, bool replay,
        bool initial, out FightBattleStepEvent step)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 20, RandomSeed = 11,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        for (int id = 1; id <= 3; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2,
                CharacterType = 1, X = id == 1 ? 4 : id == 2 ? 7 : 12, Y = 5,
                Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000, Level = 150, SubRank = 1,
                Direction = 3, HasFlipX = true, MoveMask = 5, MoveDist = approach ? 2 : 0,
                Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 - id } };
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = 1000000 });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = 100 - id });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MoveDist"), Value = approach ? 2 : 0 });
            cp.Units.Add(unit);
        }
        var owner = cp.Units[0];
        var cd = catalog.CreateStatus(60481, 1, 1, 100, 10100);
        // Controlled invocation input: do not also invoke this state during startup.
        cd.SkillCooldownModifyInitialized = true;
        owner.Statuses.Add(cd);
        var skill = new FightBattleSkillDefinition { EntityId = 10100, Slot = 0, TargetType = 1,
            Duration = .75f, ActionDuration = .75f };
        skill.Range.Add(new FightGridPoint(approach ? 1 : 3, 0));
        foreach (float delay in new[] { .25f, .5f })
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = 10101, DamageId = 13012,
                Delay = delay, DamageType = 8, HasCustomDamage = true, CustomDamage = 5 });
        if (replay)
        {
            skill.HasReplay = true; skill.ReplayStatusId = 24031; skill.ReplayStackCount = 10;
            skill.ReplayRelation = 1; skill.ReplayConsumeStack = true; skill.ReplayConsumeCount = 10;
            var stacks = catalog.CreateStatus(24031, 1, 1, 101, 10100); stacks.StackCount = 20;
            owner.Statuses.Add(stacks);
        }
        if (initial)
        {
            var pending = catalog.CreateStatus(24031, 1, 1, 102, 10100);
            pending.SkillCooldownActionPendingCount = 1; owner.Statuses.Add(pending);
        }
        owner.Skills.Add(skill);
        owner.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
        var settlement = new FightDamageSettlement();
        // Real executor dispatch invokes the production cooldown runtime here.
        // No StatusList association is supplied; its own occurrence clock is required.
        settlement.SourceHit += hit => {
            if (hit.SourceId == 1 && hit.TargetId == 2) FightStatusCooldownRuntime.Invoke(owner, cd, null);
        };
        var executor = FightBattleExecutor.Begin(cp, formula, settlement, catalog, items);
        step = executor.Step(0, 1).Event;
        var hits = step.DamageEvents.Where(value => value.SourceId == 1 && value.TargetId == 2).ToArray();
        var actions = step.StatusCustomActions.Where(value => value.HasDelay).ToArray();
        Require(hits.Length == (replay ? 6 : 2) && actions.Length == hits.Length,
            "Each actual cooldown invocation must retain a separate receipt: approach=" + approach
                + ", replay=" + replay + ", initial=" + initial + ", hits=" + hits.Length
                + ", actions=" + actions.Length + ", casts=" + step.SkillCasts.Count + ".");
        for (int i = 0; i < hits.Length; i++)
            Require(actions[i].Action == 3 && actions[i].UnitId == 1 && actions[i].SourceDamageId == 0
                && Math.Abs(actions[i].Delay - hits[i].Delay) < .00001f,
                "Unassociated cooldown timing lost the causing cast's final approach/replay offset.");
        if (approach) Require(step.Movements.Count > 0 && actions[0].Delay > .25f,
            "The fixture must exercise an actual approach offset.");
        Require(step.StatusCustomActions.Count(value => !value.HasDelay) == (initial ? 1 : 0),
            "Old pending counters must remain timing-unknown and emit only once.");
        Require(cd.SkillCooldownActionPendingCount == 0
            && executor.Step(0, 1).Replayed, "Replay must retain its receipt without invoking again.");
        Require(typeof(FightBattleUnitState).GetField("CooldownActionTriggered", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(owner) == null, "A completed step must release its runtime observer.");
        string saved = FightBattleCheckpointSerializer.Save(cp);
        var restored = FightBattleCheckpointSerializer.Load(saved);
        Require(FightBattleCheckpointSerializer.Save(restored) == saved,
            "The full checkpoint must round-trip without losing occurrence times or identity.");
        var restoredExecutor = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        var replayed = restoredExecutor.Step(0, 1);
        Require(replayed.Replayed && replayed.Event.StatusCustomActions.Count == step.StatusCustomActions.Count
            && replayed.Event.StatusCustomActions.Select(value => value.Delay).SequenceEqual(step.StatusCustomActions.Select(value => value.Delay)),
            "Restoring a saved receipt must not invoke or retime its original actions.");
        return cp;
    }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (bool approach in new[] { false, true })
            foreach (bool replay in new[] { false, true })
                foreach (bool initial in new[] { false, true })
                    Exercise(catalog, formula, items, approach, replay, initial, out _);
        Console.WriteLine("Eight actual cooldown occurrence cases passed: repeated invocations, approach, same-class replay, unknown startup counters and observer release.");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
