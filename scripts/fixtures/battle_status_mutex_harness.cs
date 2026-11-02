using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleStatusMutexRegression
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        Check(catalog.StatusMutexGroups.Count == 6, "Official mutex settings were not loaded.");
        foreach (var definition in catalog.StatusMutexGroups)
        foreach (bool reverse in new[] { false, true })
        {
            var owner = new FightBattleUnitState { Id = 1 };
            var runtime = new FightStatusMutexRuntime(catalog.StatusMutexGroups);
            var first = catalog.CreateStatus(definition.StatusIds[reverse ? 1 : 0], 1, 1, 1, 10110);
            var second = catalog.CreateStatus(definition.StatusIds[reverse ? 0 : 1], 1, 1, 2, 10110);
            owner.Statuses.Add(first); runtime.Add(owner, first);
            owner.Statuses.Add(second); runtime.Add(owner, second);
            var winner = definition.Mode == 1 ? first
                : first.ClassId == definition.StatusIds[1] ? first : second;
            var loser = ReferenceEquals(first, winner) ? second : first;
            Check(winner.ForbiddenType == 0 && loser.ForbiddenType == FightStatusLifecycle.ForbiddenMutex,
                "Official order/weight selection changed: " + first.ClassId + "/" + second.ClassId);
            foreach (int boundary in Enumerable.Range(0, 5))
            {
                var independent = catalog.CreateStatus(winner.ClassId, 1, 1, 10 + boundary, 10110);
                independent.Origin = FightBattleStatusOrigin.Resolve(independent);
                switch (boundary)
                {
                    case 0: independent.Origin.UnitId = 2; break;
                    case 1: independent.Origin.SkillEntityId = 10120; break;
                    case 2: independent.Origin.EntityKind = FightBattleStatusOrigin.Equipment;
                        independent.Origin.EntityId = 8801; break;
                    case 3: independent.Origin.EntityId = 2; break;
                    case 4: independent.Origin.SkillItemId = 0;
                        independent.Origin.SkillEntityId = 0; break;
                }
                owner.Statuses.Add(independent); runtime.Add(owner, independent);
                Check(independent.ForbiddenType == 0 && winner.ForbiddenType == 0
                    && loser.ForbiddenType == FightStatusLifecycle.ForbiddenMutex,
                    "An independent/unknown origin entered another source's mutex group.");
            }
            loser.ForbiddenType |= FightStatusLifecycle.ForbiddenStatusTrigger;
            owner.Statuses.Remove(winner); runtime.Remove(owner, winner);
            Check(loser.ForbiddenType == FightStatusLifecycle.ForbiddenStatusTrigger,
                "Winner removal failed to clear Mutex independently of another forbidden reason.");
            loser.ForbiddenType = 0;
            var restored = new FightStatusMutexRuntime(catalog.StatusMutexGroups);
            restored.Synchronize(owner);
            Check(loser.ForbiddenType == 0, "Registration merged previously separate origins.");
        }

        // Equal weight uses the later registered instance, preserving the original
        // transition order for legacy parallel instances; one retained member is not reset.
        var tieOwner = new FightBattleUnitState { Id = 1 };
        var tieRuntime = new FightStatusMutexRuntime(catalog.StatusMutexGroups);
        var a = catalog.CreateStatus(24040, 1, 1, 1, 10110);
        var b = catalog.CreateStatus(24040, 1, 1, 2, 10110);
        a.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
        tieOwner.Statuses.Add(a); tieRuntime.Add(tieOwner, a);
        Check(a.ForbiddenType == FightStatusLifecycle.ForbiddenMutex, "First registration cleared a retained reason.");
        tieOwner.Statuses.Add(b); tieRuntime.Add(tieOwner, b);
        Check(a.ForbiddenType != 0 && b.ForbiddenType == 0, "Equal weight did not prefer the later instance.");

        Check(Execute(catalog, formula, false) == Execute(catalog, formula, true),
            "Real status startup/end/property continuation differs after checkpoint restoration.");
        CheckForbiddenConsumers(catalog);
    }

    private static string Execute(FightOfficialCombatCatalog catalog, FightDamageFormula formula, bool restore)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 5, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 8, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
                X = id + 2, Y = 2, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000, SubRank = 1 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 10000 });
            var skill = new FightBattleSkillDefinition { EntityId = 990200 + id, ItemId = 990200 + id,
                Slot = 0, TargetType = 1, ActionDuration = 1, Duration = 1, CanTriggerChild = true };
            skill.Range.Add(new FightGridPoint(1, 0)); skill.Range.Add(new FightGridPoint(-1, 0));
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990300 + id,
                DamageId = 990300 + id, DamageType = 8, HasCustomDamage = true, CustomDamage = 7 });
            unit.Skills.Add(skill);
            unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.Add(unit);
        }
        var wave = new FightBattleWaveState { Index = 0 }; wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var settlement = new FightDamageSettlement();
        int hits = 0;
        settlement.SourceHit += damage =>
        {
            hits++;
            var owner = cp.Units[0];
            long Attack() => owner.Properties.Single(p => p.Id == formula.PropertyId("Attack")).Value;
            if (hits == 1)
            {
                FightBattleStatusState Apply(int id, long gear)
                {
                    var template = catalog.CreateStatus(id, 1, 1, id + (int)gear * 100, 10110, 0, 1, 1, 1);
                    template.DurationRound = -1;
                    template.Origin = new FightBattleStatusOrigin { IsSet = true, UnitId = 1,
                        EntityKind = FightBattleStatusOrigin.Equipment, EntityId = gear, SkillEntityId = 10110 };
                    return FightStatusLifecycle.Apply(owner, template, owner.SkillState.Round, template.InstanceId);
                }
                var low = Apply(24040, 8801);
                Check(Attack() == 8500, "First status startup did not apply original -15% AttackScale.");
                var high = Apply(24041, 8801);
                Check(Attack() == 7000 && low.ForbiddenType == 2 && high.ForbiddenType == 0,
                    "Higher weight did not replace the lower live property contribution immediately.");
                Apply(24040, 8802);
                Check(Attack() == 5500, "Independent equipment attack contribution was lost: attack=" + Attack()
                    + "; statuses=" + string.Join(",", owner.Statuses.Select(s => s.ClassId + ":" + s.ForbiddenType
                        + ":" + s.Origin?.EntityId + ":" + string.Join("/", s.ResolvedProperties.Select(p => p.Id + "=" + p.Value)))));
                FightStatusLifecycle.SetForbidden(owner, low, FightStatusLifecycle.ForbiddenStatusTrigger, true);
            }
            else if (hits == 2)
            {
                var high = owner.Statuses.Single(s => s.ClassId == 24041);
                var low = owner.Statuses.Single(s => s.ClassId == 24040 && s.Origin.EntityId == 8801);
                var cached = low.ResolvedProperties;
                FightStatusLifecycle.End(owner, high);
                Check(low.ForbiddenType == 4 && Attack() == 8500,
                    "Winner end cleared another forbidden reason or kept its ended contribution.");
                FightStatusLifecycle.SetForbidden(owner, low, FightStatusLifecycle.ForbiddenStatusTrigger, false);
                Check(Attack() == 7000 && ReferenceEquals(cached, low.ResolvedProperties),
                    "Complete release lost cached values or did not restore the contribution immediately.");
            }
        };
        var runner = FightBattleExecutor.Begin(cp, formula, settlement, catalog);
        for (int counter = 1; counter <= 4; counter++)
        {
            runner.Step(0, counter);
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(0, counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay changed a mutex group.");
            if (restore)
            {
                cp = FightBattleCheckpointSerializer.Load(saved);
                runner = FightBattleExecutor.Restore(cp, formula, settlement, catalog);
                Check(FightBattleCheckpointSerializer.Save(cp) == saved, "Restoring mutex registration changed a current checkpoint.");
            }
        }
        Check(hits == 4, "Controlled actual-hit fixture did not run four turns.");
        return FightBattleCheckpointSerializer.Save(cp);
    }

    private static void CheckForbiddenConsumers(FightOfficialCombatCatalog catalog)
    {
        var owner = new FightBattleUnitState { Id = 1, Side = 1, Hp = 1000, MaxHp = 1000 };
        var enemy = new FightBattleUnitState { Id = 2, Side = 2, CharacterType = 1, Hp = 1000, X = 1 };
        var states = new List<FightBattleUnitState> { owner, enemy };
        var hp = catalog.CreateStatus(25312, 1, 1, 1, 10110);
        var move = catalog.CreateStatus(25032, 1, 1, 2, 10110);
        owner.Statuses.Add(hp); owner.Statuses.Add(move);
        var runtime = new FightStatusMutexRuntime(catalog.StatusMutexGroups); runtime.Synchronize(owner);
        var random = new FightRandomStream(23);
        var movement = new FightBattleMovementEvent { UnitId = 2 };
        movement.Path.Add(new FightGridPoint(4, 0)); movement.Path.Add(new FightGridPoint(1, 0));
        int seed = random.Seed;
        Check(FightStatusTriggerRuntime.CollectMoveNearSkills(owner, states,
            FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, random).Count == 0
            && random.Seed == seed && move.MoveNearTriggeredCount == 0,
            "Disabled movement listener consumed probability or invoked.");
        hp.ForbiddenType |= FightStatusLifecycle.ForbiddenStatusTrigger;
        Check(FightStatusTriggerRuntime.ApplyAccumulatedDamage(owner, 250, catalog, random) == 0
            && hp.HpAccDamageAccumulated == 0 && hp.HpAccTriggeredCount == 0,
            "Disabled HP-loss listener accumulated loss or invoked.");
        owner.Statuses.Remove(hp); runtime.Remove(owner, hp);
        move.MoveNearRate = 1;
        Check(move.ForbiddenType == 0 && FightStatusTriggerRuntime.CollectMoveNearSkills(owner, states,
            FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, random).Count == 1,
            "Removing the earlier ordered listener did not resume original movement skill 11370.");
    }
}
