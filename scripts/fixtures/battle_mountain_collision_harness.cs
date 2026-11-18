using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Zjcs.Combat.Official;

public static class MountainCollisionHarness
{
    private sealed class ContactGate : IFightBattleImpactGate, IFightBattleBlockedMovementObserver, IFightBattleBlockedImpactObserver
    {
        public bool ObservesBlockedMovements => true;
        public FightBattleMovementEvent Movement;
        public bool Contact;
        public FightGridPoint Expected = new FightGridPoint(7, 10);
        public readonly List<int> Bound = new List<int>();
        public void BlockedMovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement, FightGridPoint position)
        {
            if (Contact || Movement != null || !position.Equals(Expected))
                throw new InvalidOperationException("Wrong blocked movement.");
            Movement = movement;
        }
        public void AwaitBlockedContact(FightBattleMovementEvent movement)
        {
            if (!ReferenceEquals(Movement, movement)) throw new InvalidOperationException("Wrong action contact.");
            Contact = true;
        }
        public void BindBlockedImpact(FightBattleMovementEvent movement, FightBattleSkillCastEvent cast, int targetId)
        {
            if (!Contact || !ReferenceEquals(Movement, movement) || cast.SkillEntityId != 10100)
                throw new InvalidOperationException("Collision child before contact.");
            Bound.Add(targetId);
        }
        public void AwaitImpact(FightBattlePendingImpact impact)
        {
            if (impact.SkillEntityId == 10100 && (!Contact || !Bound.Contains(impact.TargetId)))
                throw new InvalidOperationException("Collision hit was not bound to contact.");
        }
    }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var entry in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }
    private static void Diagonal(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, string obstacle, bool observe)
    {
        var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 1012348,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 8, 8, formula);
        var sideA = Unit(3, 2, 9, 8, formula); var sideB = Unit(4, 2, 8, 9, formula);
        var blocker = Unit(5, 2, obstacle == "unit" ? 9 : 12, obstacle == "unit" ? 9 : 12, formula);
        if (obstacle == "terrain") cp.Grid.BlockedPositions.Add(new FightGridPoint(9, 9));
        actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
        var skill = catalog.CreateSkill(11120, 0, 10052, 2, 20, 1,
            catalog.CalculateSkillProps(11120, 2, 20, 1)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        cp.Units.AddRange(new[] { actor, target, sideA, sideB, blocker });
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4, 5 }); cp.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var gate = new ContactGate { Expected = new FightGridPoint(9, 9) };
        if (observe) executor.ImpactGate = gate;
        var step = executor.Step(0, 1).Event;
        var collision = step.DamageEvents.Where(value => value.SkillEntityId == 10100).ToArray();
        if (obstacle == "open")
        {
            if (gate.Contact || target.X != 9 || target.Y != 9 || collision.Length != 0
                || !step.Movements.Any(value => value.UnitId == 2 && value.Path.Count == 2
                    && value.Path[1].Equals(new FightGridPoint(9, 9))))
                throw new InvalidOperationException("Diagonal push must reach the free destination regardless of side cells.");
        }
        else if (!observe)
        {
            if (target.X != 8 || target.Y != 8 || collision.Length != 0
                || step.Movements.Any(value => value.UnitId == 2))
                throw new InvalidOperationException("An unobserved blocked diagonal cannot move sideways or settle early.");
        }
        else
        {
            int[] expected = obstacle == "unit" ? new[] { 2, 5 } : new[] { 2 };
            if (!gate.Contact || target.X != 8 || target.Y != 8 || !gate.Bound.SequenceEqual(expected)
                || !collision.Select(value => value.TargetId).SequenceEqual(expected)
                || collision.Any(value => value.Damage != -600 || !value.TrueDamage))
                throw new InvalidOperationException("Diagonal destination collision did not hit exactly its colliding enemies.");
        }
    }
    private static void WindBlade(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int dx, int dy, int direction, string obstacle, bool suppressed)
    {
        var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 1012348,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        var actor = Unit(1, 1, 7, 7, formula); actor.Direction = direction;
        var target = Unit(2, 2, 7 + dx * 4, 7 + dy * 4, formula);
        var blocker = Unit(3, 2, obstacle == "unit" ? 7 + dx * 5 : 14,
            obstacle == "unit" ? 7 + dy * 5 : 14, formula);
        var destination = new FightGridPoint(7 + dx * 5, 7 + dy * 5);
        if (obstacle == "terrain") cp.Grid.BlockedPositions.Add(destination);
        if (suppressed)
            target.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = 20000 });
        else
            actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
        var skill = catalog.CreateSkill(10640, 0, 10012, 2, 20, 1,
            catalog.CalculateSkillProps(10640, 2, 20, 1)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        var hit = skill.HitGraphEntities.Single(value => value.EntityId == 10641);
        if (hit.HitMove == null || hit.HitMove.MoveBehaviourId != 10643 || hit.HitMove.Distance != 1
            || Math.Abs(hit.HitMove.BasePercent - .5f) > .00001f || !hit.HitMove.AffectedByProp)
            throw new InvalidOperationException("Original wind blade knockback configuration changed.");
        skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        cp.Units.AddRange(new[] { actor, target, blocker });
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var gate = new ContactGate { Expected = destination }; executor.ImpactGate = gate;
        var result = executor.Step(0, 1).Event;
        var collision = result.DamageEvents.Where(value => value.SkillEntityId == 10100).ToArray();
        if (!result.DamageEvents.Any(value => value.SkillItemId == 10012 && value.TargetId == target.Id))
            throw new InvalidOperationException("Wind blade did not hit its fourth cell.");
        if (suppressed || obstacle == "open")
        {
            int expectedX = suppressed ? 7 + dx * 4 : destination.X;
            int expectedY = suppressed ? 7 + dy * 4 : destination.Y;
            if (gate.Contact || collision.Length != 0 || target.X != expectedX || target.Y != expectedY)
                throw new InvalidOperationException("Wind blade's resisted or unblocked push was incorrect.");
        }
        else
        {
            int[] expected = obstacle == "unit" ? new[] { 2, 3 } : new[] { 2 };
            if (!gate.Contact || !gate.Bound.SequenceEqual(expected)
                || !collision.Select(value => value.TargetId).SequenceEqual(expected)
                || collision.Any(value => value.Damage != -600 || !value.TrueDamage)
                || target.X != 7 + dx * 4 || target.Y != 7 + dy * 4)
                throw new InvalidOperationException("Wind blade collision differed from the confirmed knockback rule.");
        }
    }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        foreach (var direction in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 }, new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
        foreach (string obstacle in new[] { "open", "unit", "terrain" })
        foreach (bool suppressed in new[] { false, true })
            WindBlade(catalog, formula, items, direction[0], direction[1], direction[2], obstacle, suppressed);
        foreach (string obstacle in new[] { "open", "unit", "terrain" }) Diagonal(catalog, formula, items, obstacle, true);
        Diagonal(catalog, formula, items, "terrain", false);
        var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 1012348,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        var actor = Unit(1, 1, 7, 7, formula);
        var target = Unit(2, 2, 7, 9, formula);
        var blocker = Unit(3, 2, 7, 10, formula);
        actor.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
        var skill = catalog.CreateSkill(11120, 0, 10052, 2, 20, 1,
            catalog.CalculateSkillProps(11120, 2, 20, 1)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        cp.Units.AddRange(new[] { actor, target, blocker });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); wave.UnitIds.Add(3); cp.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        string initial = FightBattleCheckpointSerializer.Save(cp);
        var gate = new ContactGate(); executor.ImpactGate = gate;
        var result = executor.Step(0, 1).Event;
        var collision = result.DamageEvents.Where(value => value.SkillEntityId == 10100).ToArray();
        if (!gate.Contact || !gate.Bound.SequenceEqual(new[] { 2, 3 }) || collision.Length != 2
            || collision.Any(value => value.HitEntityId != 10101 || value.DamageEntityId != 10006
                || !value.TrueDamage || value.ElementType != 0 || value.Damage != -600)
            || target.X != 7 || target.Y != 9 || blocker.X != 7 || blocker.Y != 10)
            throw new InvalidOperationException("Original blocked child, fixed damage or collision targets differ: contact="
                + gate.Contact + ", bound=" + string.Join(",", gate.Bound) + ", collisions="
                + string.Join(";", collision.Select(value => value.TargetId + ":" + value.HitEntityId + ":"
                    + value.DamageEntityId + ":" + value.TrueDamage + ":" + value.ElementType + ":" + value.Damage))
                + ", target=" + target.X + "," + target.Y + ", blocker=" + blocker.X + "," + blocker.Y);
        var wall = FightBattleCheckpointSerializer.Load(initial);
        wall.Units.Single(unit => unit.Id == 3).X = 12;
        wall.Units.Single(unit => unit.Id == 3).Y = 12;
        wall.Grid.BlockedPositions.Add(new FightGridPoint(7, 10));
        var wallExecutor = FightBattleExecutor.Restore(wall, formula, new FightDamageSettlement(), catalog, items);
        var wallGate = new ContactGate(); wallExecutor.ImpactGate = wallGate;
        var wallResult = wallExecutor.Step(0, 1).Event;
        if (!wallGate.Contact || !wallGate.Bound.SequenceEqual(new[] { 2 })
            || wallResult.DamageEvents.Count(value => value.SkillEntityId == 10100 && value.Damage == -600) != 1)
            throw new InvalidOperationException("A blocked terrain cell did not execute the collision child once.");
        var original = FightBattleCheckpointSerializer.Load(initial);
        using (var live = new FightBattleIsolatedStep(original,
            state => FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items), 0, 1, true))
        {
            bool contacted = false;
            int childSettlements = 0;
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!live.IsCompleted && DateTime.UtcNow < deadline)
            {
                while (live.Latch.TryTake(out _)) { }
                if (live.Latch.TryTakeSecondsStatus(out var statusNotice))
                    live.Latch.CompleteSecondsStatus(statusNotice, out _);
                else if (live.Latch.TryTakePreparation(out var prepared))
                    live.Latch.CompletePreparation(prepared, out _);
                else if (live.Latch.TryTakeSelection(out var selected))
                    live.Latch.CompleteSelection(selected, (selectedCast, hit) => {
                        if (selectedCast.SkillEntityId == 10100) return;
                        foreach (var occurrence in hit.Targets)
                            live.Latch.RecordArrival(selectedCast, hit, occurrence.EffectCenterIndex,
                                occurrence.HasScopeIndex ? occurrence.ScopeIndex : -1);
                    }, out _);
                else if (live.Latch.TryTakeMovement(out var movement))
                {
                    if (!movement.HasBlockedPosition || contacted)
                        throw new InvalidOperationException("The real worker did not prepare one blocked action.");
                    live.Latch.CompleteMovement(movement, out long revision);
                    live.Latch.WaitForWorkerBoundary(revision);
                    if (!live.Latch.RecordBlockedContact(movement.Movement, out revision))
                        throw new InvalidOperationException("The real action contact was not recorded.");
                    contacted = true;
                    if (revision > 0) live.Latch.WaitForWorkerBoundary(revision);
                }
                else if (live.Latch.TryTakeSettlement(out var settled))
                {
                    if (settled.Impact.SkillEntityId == 10100)
                    {
                        if (!contacted || settled.Damage.Damage != -600 || !settled.Damage.TrueDamage)
                            throw new InvalidOperationException("The real child settled before contact or with incorrect damage.");
                        childSettlements++;
                    }
                    live.Latch.CompleteSettlement(settled);
                }
                else if (live.Latch.TryTakeCastCompletion(out var completed)) live.Latch.CompleteCast(completed);
                else Thread.Sleep(1);
            }
            bool stepCompleted = live.TryTakeCompleted(out _, out var liveResult);
            int childEvents = stepCompleted ? liveResult.Event.DamageEvents.Count(value => value.SkillEntityId == 10100) : -1;
            if (!stepCompleted || !contacted || childSettlements != 2 || childEvents != 2
                || FightBattleCheckpointSerializer.Save(original) != initial)
                throw new InvalidOperationException("The isolated contact worker did not complete both child hits safely: completed="
                    + stepCompleted + ", contacted=" + contacted + ", settlements=" + childSettlements + ", events=" + childEvents);
        }
        Console.WriteLine("Mountain smash collision child, both victims and contact ordering passed.");
    }
}
