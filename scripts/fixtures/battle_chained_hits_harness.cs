using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleChainedHitsHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected, string message)
    { Check(Math.Abs(actual - expected) < .0001f, message + ": " + actual + " != " + expected); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var lightning = RunCase(catalog, items, formula, 10820, new[] {
            new FightGridPoint(7, 5), new FightGridPoint(8, 5),
            new FightGridPoint(7, 6), new FightGridPoint(9, 5) });
        Check(Cast(lightning, 10820).HitSelections.SelectMany(hit => hit.Targets)
            .Select(hit => hit.TargetId).SequenceEqual(new[] { 2, 3, 5, 4 }),
            "First-promotion lightning lost nearest-distance/ID order or revisited a target.");
        ValidateEdges(lightning, Cast(lightning, 10820), catalog);
        var reordered = RunCase(catalog, items, formula, 10820, new[] {
            new FightGridPoint(7, 5), new FightGridPoint(8, 5),
            new FightGridPoint(7, 6), new FightGridPoint(9, 5) },
            arrange: checkpoint => checkpoint.Units.Reverse());
        Check(Cast(reordered, 10820).HitSelections.SelectMany(hit => hit.Targets)
            .Select(hit => hit.TargetId).SequenceEqual(new[] { 2, 3, 5, 4 }),
            "Equal-distance ID ordering changed with unit enumeration order.");
        foreach (bool reverseBody in new[] { false, true })
        {
            // Supply the first selected target explicitly: this checks next-hop
            // tie breaking independently of the ordinary first-target AI.
            var source = Unit(1, 5, 5); var first = Unit(2, 7, 5); var next = Unit(3, 10, 5);
            next.BodyOffsets.Clear();
            next.BodyOffsets.Add(new FightGridPoint(-2, reverseBody ? -1 : 1));
            next.BodyOffsets.Add(new FightGridPoint(-2, reverseBody ? 1 : -1));
            var skill = catalog.CreateSkill(10820, 0, 10030, 2, 20, 1, null);
            var cast = FightBattleSkillCastPresentation.Capture(source, skill,
                new List<FightBattleUnitState> { first });
            var traversal = new FightHitChainExecution(FightBattleHitCollector.CollectAll(skill), cast, true);
            var context = new FightHitTargetSelectionContext {
                Source = source, TargetingSource = source, Skill = skill, Cast = cast,
                Units = new List<FightBattleUnitState> { source, first, next },
                Targets = new List<FightBattleUnitState> { first }, Primary = first
            };
            Check(traversal.TryTake(out var root), "Missing original chained root");
            traversal.Select(root, context, null);
            Check(traversal.TryTake(out var child), "Missing original chained child");
            var center = traversal.Select(child, context, null).EffectCenters.Single();
            Check(center.X == 8 && center.Y == (reverseBody ? 4 : 6),
                "Equal-distance cells did not retain their original body order: "
                    + reverseBody + "/" + center.X + "," + center.Y);
        }
        var shield = RunCase(catalog, items, formula, 11090,
            new[] { new FightGridPoint(7, 5), new FightGridPoint(9, 5) });
        Check(Cast(shield, 11090).HitSelections.SelectMany(hit => hit.Targets)
            .Select(hit => hit.TargetId).SequenceEqual(new[] { 2, 3, 2, 3 }),
            "First-promotion shield did not retain all four alternating original hits.");
        ValidateEdges(shield, Cast(shield, 11090), catalog);
        foreach (int id in new[] { 10820, 11090 })
            Check(Cast(RunCase(catalog, items, formula, id,
                new[] { new FightGridPoint(7, 5) }), id).HitSelections.Count == 1,
                "First-promotion chain fabricated a next target: " + id);

        var linear = RunCase(catalog, items, formula, 12860,
            new[] { new FightGridPoint(7, 5), new FightGridPoint(9, 5) });
        var linearCast = Cast(linear, 12860);
        Check(Chained(linearCast, 12861, 12866).Length == 6, "Original six-hop chain stopped early.");
        Check(linearCast.HitSelections.SelectMany(hit => hit.Targets).Select(hit => hit.TargetId)
            .SequenceEqual(new[] { 2, 3, 2, 3, 2, 3 }), "Original repeat/back chain lost its alternating targets.");
        ValidateEdges(linear, linearCast, catalog);

        var one = RunCase(catalog, items, formula, 12860, new[] { new FightGridPoint(7, 5) });
        Check(Cast(one, 12860).HitSelections.Count == 1, "No candidate should stop the remaining chain.");

        foreach (bool repeat in new[] { false, true })
        foreach (bool back in new[] { false, true })
        {
            var battle = RunCase(catalog, items, formula, 12860, new[] { new FightGridPoint(7, 5) },
                skill => SetFlags(skill, repeat, back, false), true);
            int count = Cast(battle, 12860).HitSelections.Count;
            Check(count == (repeat && back ? 6 : 1), "Controlled repeat/back restriction failed: repeat="
                + repeat + ", back=" + back + ", count=" + count);
        }

        foreach (bool prefer in new[] { false, true })
        {
            var battle = RunCase(catalog, items, formula, 12860,
                new[] { new FightGridPoint(7, 5), new FightGridPoint(9, 5) },
                skill => SetFlags(skill, true, true, prefer), true);
            int next = Cast(battle, 12860).HitSelections[1].Targets[0].TargetId;
            Check(next == (prefer ? 3 : 2), "PreferUnvisited did not outrank the nearest visited body cell.");
        }

        var branching = RunCase(catalog, items, formula, 12330, new[] {
            new FightGridPoint(7, 5), new FightGridPoint(8, 5), new FightGridPoint(7, 6),
            new FightGridPoint(8, 6), new FightGridPoint(9, 5), new FightGridPoint(9, 6),
            new FightGridPoint(7, 4) });
        var branchCast = Cast(branching, 12330);
        Check(branchCast.HitSelections.Count == 7 && branchCast.HitSelections.SelectMany(hit => hit.Targets)
            .Select(hit => hit.TargetId).Distinct().Count() == 7,
            "Repeated class references did not share the root's visited set across all branches.");
        Check(branchCast.HitSelections.Take(3).Select(hit => hit.HitEntityId)
            .SequenceEqual(new[] { 12331, 12332, 12332 }), "Equal-time branch creation lost configured reference order.");
        ValidateEdges(branching, branchCast, catalog);

        var sparse = RunCase(catalog, items, formula, 12330,
            new[] { new FightGridPoint(7, 5), new FightGridPoint(8, 5), new FightGridPoint(7, 6) });
        Check(Cast(sparse, 12330).HitSelections.Count == 3,
            "A branch without an unvisited target must stop without creating empty descendants.");

        foreach (int skillId in new[] { 18180, 63890 })
        {
            var random = RunCase(catalog, items, formula, skillId,
                new[] { new FightGridPoint(7, 5), new FightGridPoint(10, 5), new FightGridPoint(8, 7) }, null, true);
            var randomCast = Cast(random, skillId);
            var definitions = catalog.CreateSkill(skillId, 0, 0, 5, 20, 1, null).HitGraphEntities;
            var ids = new HashSet<int>(definitions.Where(hit => hit.IsChained).Select(hit => hit.EntityId));
            int targetCount = randomCast.HitSelections.Where(hit => ids.Contains(hit.HitEntityId))
                .Sum(hit => hit.Targets.Count);
            Console.WriteLine("Original random chain " + skillId + ": " + targetCount + " targets.");
            Check(targetCount == 27, "Random starting hits must own nine independent three-hop histories: " + targetCount);
            Check(randomCast.HitSelections.Any(hit => ids.Contains(hit.HitEntityId)
                && hit.EffectCenters.Any(cell => random.Units.Any(unit => unit.Id == 2
                    && cell.X == unit.X + 1 && cell.Y == unit.Y))),
                "The random fixture did not exercise an actual non-centre body cell.");
            ValidateEdges(random, randomCast, catalog);
        }

        var triggered = RunCase(catalog, items, formula, 13520,
            new[] { new FightGridPoint(7, 5), new FightGridPoint(9, 5), new FightGridPoint(8, 7) }, null, true);
        var children = triggered.Receipts[0].Event.SkillCasts.Where(value => value.SkillEntityId == 13523).ToArray();
        Check(children.Length > 0, "Original trigger skill did not create its chained child skill.");
        foreach (var child in children)
        {
            Check(child.Presentation.HasEntityPosition, "Triggered child lost its actual hit entity origin.");
            ValidateEdges(triggered, child, catalog);
        }
        Console.WriteLine("Chained hit rules passed: linear, stop, repeat/back, unvisited preference, shared branches, independent random roots, body cells, child-skill origins and checkpoint continuation.");
    }

    private static void SetFlags(FightBattleSkillDefinition skill, bool repeat, bool back, bool prefer)
    {
        foreach (var hit in skill.HitGraphEntities)
            if (hit.IsChained) { hit.Repeat = repeat; hit.Back = back; hit.PreferUnvisited = prefer; }
    }

    private static FightBattleCheckpoint RunCase(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula, int skillId,
        FightGridPoint[] positions, Action<FightBattleSkillDefinition> configure = null, bool large = false,
        Action<FightBattleCheckpoint> arrange = null)
    {
        var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        var source = Unit(1, 5, 5);
        int itemId = skillId == 10820 ? 10030 : skillId == 11090 ? 10048 : 0;
        var skill = catalog.CreateSkill(skillId, 0, itemId, itemId == 0 ? 5 : 2, 20, 1, null);
        source.Skills.Add(skill);
        source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        checkpoint.Units.Add(source);
        var wave = new FightBattleWaveState();
        for (int index = 0; index < positions.Length; index++)
        {
            var unit = Unit(index + 2, positions[index].X, positions[index].Y);
            if (large && index == 0) unit.BodyOffsets.Add(new FightGridPoint(1, 0));
            checkpoint.Units.Add(unit); wave.UnitIds.Add(unit.Id);
        }
        checkpoint.Waves.Add(wave);
        arrange?.Invoke(checkpoint);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        // Begin restores immutable official configuration; controlled flag cases
        // deliberately vary it only after that production restoration.
        configure?.Invoke(skill);
        executor.Step(0, 1);
        Check(checkpoint.Receipts[0].Event.SkillCasts.Any(value => value.SkillEntityId == skillId),
            "Original chain skill was not cast: " + skillId);
        string saved = FightBattleCheckpointSerializer.Save(checkpoint);
        Check(!saved.Contains("PreviousChainHit") && !saved.Contains("ChainConfiguration"),
            "Cast-local traversal references leaked into persistent JSON.");
        if (configure != null) return checkpoint;
        var restored = FightBattleCheckpointSerializer.Load(saved);
        var resume = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        executor.Step(checkpoint.Sequence, checkpoint.Counter + 1);
        resume.Step(restored.Sequence, restored.Counter + 1);
        Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
            "Checkpoint continuation diverged for skill " + skillId);
        return checkpoint;
    }

    private static FightBattleUnitState Unit(int id, int x, int y)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2,
            CharacterType = id == 1 ? 1 : 2, X = x, Y = y, Hp = 10000000, MaxHp = 10000000,
            Level = 150, SubRank = 1, MoveMask = 5, Direction = 3, HasFlipX = true,
            Turn = new FightTurnUnit { Id = id, Hp = 10000000, Speed = 100 - id } };
        foreach (var pair in new[] { new[] { 1, 10001 }, new[] { 2, 10003 },
            new[] { 3, 10000000 }, new[] { 4, 100 - id } })
            unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
        return unit;
    }

    private static FightBattleSkillCastEvent Cast(FightBattleCheckpoint battle, int id)
        => battle.Receipts[0].Event.SkillCasts.Single(value => value.SkillEntityId == id);

    private static FightBattleHitSelectionSnapshot[] Chained(FightBattleSkillCastEvent cast, int first, int last)
        => cast.HitSelections.Where(value => value.HitEntityId >= first && value.HitEntityId <= last).ToArray();

    private static void ValidateEdges(FightBattleCheckpoint battle, FightBattleSkillCastEvent cast,
        FightOfficialCombatCatalog catalog)
    {
        var graph = catalog.CreateSkill(cast.SkillEntityId, 0, 0, 5, 20, 1, null).HitGraphEntities;
        var chainIds = new HashSet<int>(graph.Where(value => value.IsChained).Select(value => value.EntityId));
        var expectedDamage = cast.HitSelections.Where(hit => chainIds.Contains(hit.HitEntityId))
            .SelectMany(hit => hit.Targets.Select(target => new { Hit = hit, Target = target })).ToArray();
        var actualDamage = battle.Receipts[0].Event.DamageEvents.Where(value =>
            cast.DamageEventIds.Contains(value.DamageId) && chainIds.Contains(value.HitEntityId)).ToArray();
        Check(actualDamage.Length == expectedDamage.Length,
            "Settled chained damage count differs from the actual selected body cells.");
        for (int index = 0; index < actualDamage.Length; index++)
        {
            var damage = actualDamage[index]; var expected = expectedDamage[index];
            var definition = graph.Single(value => value.EntityId == expected.Hit.HitEntityId);
            Check(damage.HitEntityId == expected.Hit.HitEntityId
                && damage.DamageEntityId == definition.DamageId && damage.TargetId == expected.Target.TargetId
                && damage.HasDamagePosition && damage.DamageX == expected.Target.GridX
                && damage.DamageY == expected.Target.GridY,
                "Chained damage settled on a different node, target or body cell.");
            Near(damage.Delay, cast.Delay + expected.Hit.EffectDelays[expected.Target.EffectCenterIndex]
                + expected.Target.ScopeDelay, "Chained damage lost its actual flight/scope timing");
        }
        foreach (var hit in cast.HitSelections)
        {
            var definition = graph.Single(value => value.EntityId == hit.HitEntityId);
            if (!definition.IsChained) continue;
            var parents = graph.Where(value => value.ChainedHitLinks.Any(link => link.EntityId == hit.HitEntityId)).ToArray();
            if (parents.Length == 0)
            {
                Check(hit.SourceX == (cast.Presentation.HasEntityPosition ? cast.Presentation.EntityX : cast.Presentation.SourceX)
                    && hit.SourceY == (cast.Presentation.HasEntityPosition ? cast.Presentation.EntityY : cast.Presentation.SourceY),
                    "First chained flight must start at the actual skill entity position.");
                continue;
            }
            bool found = false;
            foreach (var parent in cast.HitSelections)
            foreach (var parentDefinition in parents.Where(value => value.EntityId == parent.HitEntityId))
            foreach (var link in parentDefinition.ChainedHitLinks.Where(value => value.EntityId == hit.HitEntityId))
            for (int index = 0; index < parent.EffectCenters.Count; index++)
            {
                var center = parent.EffectCenters[index];
                float delay = parent.Targets.Where(target => target.EffectCenterIndex == index)
                    .Select(target => target.ScopeDelay).DefaultIfEmpty(0f).Max();
                if (hit.SourceX == center.X && hit.SourceY == center.Y
                    && Math.Abs(hit.Delay - parent.EffectDelays[index] - delay - link.Delay) < .0001f) found = true;
            }
            Check(found, "Chained child lost its actual previous cell or accumulated damage/edge timing: " + hit.HitEntityId);
            Check(hit.Targets.All(value => value.HasGridPosition), "Chain damage lost its actual body cell.");
            foreach (var target in hit.Targets)
                Near(target.ScopeDelay, definition.ScopeEntries[0].Delay, "Original chained scope delay changed");
        }
    }
}
