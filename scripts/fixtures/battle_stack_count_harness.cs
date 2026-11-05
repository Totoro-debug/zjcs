using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStackCountRegression
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (int id in new[] {24034,24145,24289,24313,24316,24353,24410,24421,
            24430,24433,24466,24518,24602,24694,24703,24743,24777,24796,25181})
        {
            var status = catalog.CreateStatus(id, 1, 2);
            Check(status.StackCountRulesSupported && status.StackCountSkillsResolved
                && status.StackCountSkills.Count > 0, "Original threshold skills were not bound: " + id);
        }
        Check(!catalog.CreateStatus(24066, 1, 2).StackCountRulesSupported,
            "Unsupported AI/TriggerTarget threshold was silently enabled.");
        foreach (bool restore in new[] {false, true})
        {
            Console.WriteLine("STACK_COUNT startup legacy=" + restore);
            Start(catalog, formula, items, restore);
            Console.WriteLine("STACK_COUNT hit-stacking restore=" + restore);
            Stacking(catalog, formula, items, restore);
        }
        Creator(catalog, formula, items, false);
        Creator(catalog, formula, items, true);
    }

    private static FightBattleCheckpoint Create(FightDamageFormula formula)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 29, MaxRound = 50,
            Grid = new FightBattleGrid { Width = 12, Height = 8, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
                X = id + 2, Y = 2, Hp = 500000, MaxHp = 1000000, LastMaxHp = 1000000,
                Level = 20, SubRank = 1 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            foreach (var pair in new[] {new {Name="MaxHp",Value=unit.MaxHp},
                new {Name="Attack",Value=1000L},new {Name="Defence",Value=100L},
                new {Name="Speed",Value=(long)unit.Turn.Speed}})
                unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId(pair.Name),Value=pair.Value});
            var skill = new FightBattleSkillDefinition { EntityId = 990200 + id, ItemId = 990200 + id,
                Slot = 0, TargetType = 1, RangeVerified = true, ActionDuration = 1, Duration = 1, CanTriggerChild = true };
            skill.Range.Add(new FightGridPoint(1, 0)); skill.Range.Add(new FightGridPoint(-1, 0));
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990300 + id, DamageId = 990300 + id,
                DamageType = 8, HasCustomDamage = true, CustomDamage = 7, Delay = .1f });
            unit.Skills.Add(skill);
            unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.Add(unit);
        }
        return cp;
    }

    private static void Start(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool legacy)
    {
        var cp = Create(formula);
        var source = catalog.CreateStatus(24145, 1, 1, 1, 990201, 990201, 4, 20, 1, cp.Units[0].Properties);
        if (legacy) { source.StackCountSkillsResolved = false; source.StackCountSkills.Clear(); source.StackCountRulesSupported = false; }
        cp.Units[0].Statuses.Add(source);
        var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var result = runner.Step(0, 1);
        Check(!cp.Finished, "Startup unexpectedly ended battle: " + cp.Outcome.Reason
            + " HP=" + string.Join(",", cp.Units.Select(u => u.Id + ":" + u.Hp)));
        Check(result.Event.SkillCasts.Select(c => c.SkillEntityId).SequenceEqual(new[] {13110,13115}),
            "Initial threshold did not execute the two original skills in order.");
        Check(cp.Units[0].Statuses.All(s => s.ClassId != 24145), "Threshold was not removed after its finite quota.");
        Check(cp.Units[0].SkillState.Slots[0].ReleasedTimes == 0,
            "Threshold startup consumed an ordinary action.");
        Check(result.Event.DamageEvents.Count > 0 && cp.Statistics.Skills.Any(s => s.Casts > 0),
            "Original threshold skills bypassed common settlement/statistics.");
        string saved = FightBattleCheckpointSerializer.Save(cp);
        Check(runner.Step(0, 1).Replayed && saved == FightBattleCheckpointSerializer.Save(cp),
            "Repeated startup receipt cast threshold skills twice.");
        cp = FightBattleCheckpointSerializer.Load(saved);
        runner = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), catalog, items);
        result = runner.Step(0, 2);
        Check(result.Event.SkillCasts.All(c => c.SkillEntityId != 13110 && c.SkillEntityId != 13115),
            "Cold restore repeated a removed startup threshold.");
    }

    private static void Stacking(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool restore)
    {
        var cp = Create(formula);
        var settlement = new FightDamageSettlement();
        int ownHits = 0, releases = 0, lastStackRound = 0;
        settlement.SourceHit += damage =>
        {
            if (damage.SourceId != 1 || cp.Units[0].SkillState.Round == lastStackRound) return;
            lastStackRound = cp.Units[0].SkillState.Round;
            ownHits++;
            var source = catalog.CreateStatus(24034, 1, 2, 999, 990201, 990201, 4, 20, 1, cp.Units[0].Properties);
            FightStatusLifecycle.Apply(cp.Units[0], source, -1, 999);
        };
        var runner = FightBattleExecutor.Begin(cp, formula, settlement, catalog, items);
        while (ownHits < 16)
        {
            Check(!cp.Finished, "Stack fixture ended before 16 actual hits: hits=" + ownHits
                + " counter=" + cp.Counter + " reason=" + cp.Outcome.Reason);
            var result = runner.Step(0, cp.Counter + 1);
            var casts = result.Event.SkillCasts.Where(c => c.SkillEntityId == 12910).ToArray();
            releases += casts.Length;
            Check(casts.All(c => c.ActorId == 1), "Applicator threshold used its foreign creator as the caster.");
            Check(releases == (ownHits >= 15 ? 1 : 0), "Threshold cast before 15 stacks or repeated its quota: hits=" + ownHits + " releases=" + releases
                + " statuses=" + string.Join(",", cp.Units[0].Statuses.Select(s => s.ClassId + ":" + s.StackCount + ":" + s.StackCountTriggered)));
            if (casts.Length > 0) Check(result.Event.DamageEvents.Count > 1,
                "Position-target threshold cast produced no additional resolved hit.");
            if (ownHits >= 15) Check(cp.Units[0].Statuses.All(s => s.ClassId != 24034 || s.StackCount == 1),
                "Completed threshold retained stale layers.");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(0, cp.Counter).Replayed && saved == FightBattleCheckpointSerializer.Save(cp),
                "Repeated hit receipt restacked or recast the threshold.");
            if (restore)
            {
                cp = FightBattleCheckpointSerializer.Load(saved);
                runner = FightBattleExecutor.Restore(cp, formula, settlement, catalog, items);
            }
        }
    }

    private static void Creator(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool missing)
    {
        var cp = Create(formula);
        var status = catalog.CreateStatus(24289, 1, missing ? 999 : 2, 9924289, 17900, 17900,
            4, 20, 1, cp.Units[1].Properties);
        status.StackCount = status.StackCountThreshold;
        cp.Units[0].Statuses.Add(status);
        var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var result = runner.Step(0, 1);
        var casts = result.Event.SkillCasts.Where(c => c.SkillEntityId == 17950).ToArray();
        Check(missing ? casts.Length == 0 : casts.Length == 1 && casts[0].ActorId == 2
            && casts[0].TargetIds.SequenceEqual(new[] {1}),
            "Creator threshold lost its original source/target or synthesized a missing creator.");
    }
}
