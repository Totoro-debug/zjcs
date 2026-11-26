using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleMeteorFireHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Meteor fire: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, burningHits = 0, cappedGroups = 0;
        var patterns = new HashSet<string>();
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int bodyWidth in new[] { 1, 2, 3 })
        foreach (int ground in new[] { 0, 3 })
        foreach (int seed in new[] { 13, 91, 104729, 209458 })
        {
            bool large = bodyWidth > 1;
            var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = ground } };
            var actor = Unit(1, 1, 5, 5, formula);
            var near = Unit(2, 2, 6, 5, formula);
            var far = Unit(3, 2, 9, 5, formula);
            var friend = Unit(4, 1, 8, 5, formula);
            if (large)
                for (int x = 0; x < bodyWidth; x++)
                    for (int y = 0; y < bodyWidth; y++) far.BodyOffsets.Add(new FightGridPoint(
                        x - (bodyWidth == 3 ? 1 : 0), y - (bodyWidth == 3 ? 1 : 0)));
            cp.Units.AddRange(new[] { actor, near, far, friend });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
            var props = catalog.CalculateSkillProps(11540, rank, level, 8);
            var skill = catalog.CreateSkill(11540, 0, 10070, rank, level, 8,
                props.Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            // Earlier checkpoints retained summon cells but omitted their shared
            // damage scopes. Restore immutable scopes without resetting state.
            foreach (var oldHit in copy.Units[0].Skills[0].HitGraphEntities.Where(h => h.IsSummon))
                oldHit.Scope.Clear();
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var opening = executor.Step(cp.Sequence, cp.Counter + 1).Event;
            resumed.Step(copy.Sequence, copy.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                "cold restoration changed the impact, independent cell rolls or burning startup");
            var cast = opening.SkillCasts.Single(c => c.ActorId == 1 && c.SkillEntityId == 11540);
            var hit = cast.HitSelections.Single(); var center = hit.EffectCenters.Single();
            Check(hit.HitEntityId == 11541 && Math.Abs(hit.Delay - .5f) < .0001f
                && Math.Abs(center.X - 5) + Math.Abs(center.Y - 5) <= 5,
                "the root impact or five-cell cast range changed");
            var expectedCounts = new[] { near, far }.ToDictionary(u => u.Id,
                u => new[] { new FightGridPoint(0, 0) }.Concat(u.BodyOffsets).Distinct()
                    .Count(p => Math.Abs(u.X + p.X - center.X) <= 1 && Math.Abs(u.Y + p.Y - center.Y) <= 1));
            var mainHits = opening.DamageEvents.Where(d => d.SkillEntityId == 11540).ToArray();
            long expectedDamage = -(long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m);
            Check(mainHits.Length > 0 && mainHits.Length == expectedCounts.Values.Sum()
                && mainHits.GroupBy(d => d.TargetId).All(g => expectedCounts.ContainsKey(g.Key)
                    && g.Count() == expectedCounts[g.Key])
                && mainHits.All(d => d.SourceId == 1 && d.SkillItemId == 10070 && d.HitEntityId == 11541
                    && d.DamageEntityId == 10007 && d.ElementType == 102 && d.DamageType == 1
                    && d.Damage == expectedDamage && Math.Abs(d.Delay - 1f) < .0001f
                    && !d.TrueDamage && !d.IsCure && !d.Critical && !d.Block),
                "cultivated fire damage, 3x3 body-cell area, timing or friendly exclusion changed: "
                + rank + "/" + level + "/" + large + "/" + ground + "/" + seed
                + ", expected=" + expectedDamage + ", counts=" + string.Join(",", expectedCounts.Values)
                + ", actual=" + string.Join(";", mainHits.Select(d => d.SourceId + ":" + d.TargetId
                    + ":" + d.HitEntityId + ":" + d.DamageEntityId + ":" + d.Damage + ":" + d.Delay
                    + ":" + d.ElementType + ":" + d.DamageType)));
            if (large) Check(expectedCounts[3] > 0, "large-body priority chose only the nearer small target");
            Check(friend.Hp == friend.MaxHp, "an allied target was damaged");
            if (ground == 3) Check(cp.GridItems.Count == 0, "burning ground was created on water");
            else
            {
                Check(cp.GridItems.Count > 0 && cp.GridItems.Count <= 9,
                    "independent 60-percent burning cells were absent or duplicated");
                patterns.Add(string.Join(";", cp.GridItems.Select(g => (g.X - center.X) + "," + (g.Y - center.Y))));
                Check(cp.Units.Count == 4, "grid actors entered the turn roster");
                foreach (var grid in cp.GridItems)
                {
                    var status = grid.Runtime.Statuses.Single(s => s.ClassId == 11542);
                    Check(grid.GridItemClassId == 3320 && grid.EntityClassId == 3051 && grid.OwnerId == 1
                        && grid.ReferenceUnitId == 1 && grid.Runtime.Side == 1
                        && Math.Abs(grid.CreationDelay - 1f) < .0001f
                        && Math.Abs(grid.X - center.X) <= 1 && Math.Abs(grid.Y - center.Y) <= 1
                        && status.CreatorId == 1 && status.OwnerId == grid.Id && status.DurationRound == 3
                        && status.RoundTarget == 1 && status.RootSkillId == 11540
                        && status.ApplyingSkillItemId == 10070 && status.SourceSkillRank == rank
                        && status.SourceSkillLevel == level && status.RemoveApplyEntityOnEnd,
                        "burning ground lost its snapshot, original status, three creator rounds or cultivation");
                }
                var owners = new List<FightBattleUnitState>(cp.Units);
                owners.AddRange(cp.GridItems.Select(g => g.Runtime));
                foreach (var target in new[] { near, far })
                {
                    var cells = new[] { new FightGridPoint(0, 0) }.Concat(target.BodyOffsets).Distinct()
                        .Select(p => new FightGridPoint(target.X + p.X, target.Y + p.Y)).ToArray();
                    int occupied = cp.GridItems.Count(g => cells.Contains(new FightGridPoint(g.X, g.Y)));
                    var standing = FightStatusTriggerRuntime.CollectMoveNearSkills(target, owners,
                        FightStatusTriggerRuntime.MoveNearEventStandRound, null, new FightRandomStream(seed));
                    Check(standing.Count == occupied, "standing on burning body cells merged or duplicated triggers");
                    var move = new FightBattleMovementEvent { UnitId = target.Id };
                    move.Path.Add(new FightGridPoint(20, 20)); move.Path.Add(new FightGridPoint(target.X, target.Y));
                    var entering = FightStatusTriggerRuntime.CollectMoveNearSkills(target, owners,
                        FightStatusTriggerRuntime.MoveNearEventEnterRange, move, new FightRandomStream(seed));
                    Check(entering.Count == occupied, "landing on burning cells lost the entry trigger");
                }
                foreach (var damage in opening.DamageEvents.Where(d => d.SkillEntityId == 11600))
                {
                    burningHits++;
                    Check(cp.GridItems.Any(g => g.Id == damage.SourceId) && damage.TargetId != 1 && damage.TargetId != 4
                        && damage.SkillItemId == 10070 && damage.HitEntityId == 11601
                        && damage.DamageEntityId == 10021 && damage.ElementType == 102 && damage.Damage < 0
                        && Math.Abs(damage.Delay - 1f) < .0001f,
                        "startup burning damage lost the grid actor, inherited item, element or creation time");
                }
                var burnProps = catalog.CalculateSkillProps(11600, rank, level, 8);
                long baseBurn = (long)((1000m * burnProps[200] / 10000m + burnProps[190]) * 1000m / 1010m);
                foreach (var targetHits in opening.DamageEvents.Where(d => d.SkillEntityId == 11600).GroupBy(d => d.TargetId))
                {
                    if (targetHits.Count() >= 5) cappedGroups++;
                    int count = 0;
                    foreach (var damage in targetHits)
                    {
                        long expectedBurn = -(long)(baseBurn * Math.Pow(1f - .35f, Math.Min(count, 3)));
                        Check(Math.Abs(damage.Damage - expectedBurn) <= 1,
                            "burning cells did not share the original creator/item round counter: hit=" + count
                            + ", expected=" + expectedBurn + ", actual=" + damage.Damage);
                        count++;
                    }
                }
            }
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence, cp.Counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "receipt replay created another impact or burning cell");
            // Direct range-listener probes above advance their trigger counters.
            // Resume from that same state before comparing subsequent live turns.
            copy = FightBattleCheckpointSerializer.Load(saved);
            resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var roundBurnCounts = new Dictionary<int, int>();
            foreach (var group in opening.DamageEvents.Where(d => d.SkillEntityId == 11600).GroupBy(d => d.TargetId))
            {
                var target = cp.Units.Single(u => u.Id == group.Key);
                roundBurnCounts[target.Id] = group.Count();
            }
            var laterBurnProps = catalog.CalculateSkillProps(11600, rank, level, 8);
            long laterBaseBurn = (long)((1000m * laterBurnProps[200] / 10000m + laterBurnProps[190]) * 1000m / 1010m);
            while (cp.Counter < 30 && !cp.Finished && cp.GridItems.Any(g => !g.Ended))
            {
                var next = executor.Step(cp.Sequence, cp.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                foreach (var damage in next.DamageEvents.Where(d => d.SkillEntityId == 11600))
                {
                    var target = cp.Units.Single(u => u.Id == damage.TargetId);
                    int key = target.Id;
                    roundBurnCounts.TryGetValue(key, out int count);
                    long expectedBurn = -(long)(laterBaseBurn * Math.Pow(1f - .35f, Math.Min(count, 3)));
                    Check(Math.Abs(damage.Damage - expectedBurn) <= 1,
                        "standing/entry burning damage lost round reset or the shared falloff cap: expected="
                        + expectedBurn + ", actual=" + damage.Damage + ", round=" + target.SkillState.Round);
                    roundBurnCounts[key] = count + 1;
                }
                // Duration zero expires at the target's turn end, after standing
                // damage. The round number has already advanced inside that step.
                foreach (var target in cp.Units)
                    if (!target.Statuses.Any(status => status.ClassId == 24102))
                        roundBurnCounts.Remove(target.Id);
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                    "restoration changed standing damage, decay, lifetime or grid removal");
            }
            Check(cp.GridItems.All(g => g.Ended && g.Runtime.Ended && g.Runtime.Statuses.Count == 0),
                "burning cells survived their three creator rounds");
            cases++;
        }
        Check(patterns.Count > 3 && burningHits > 0 && cappedGroups > 0,
            "independent cell randomness, burning startup and the three-step falloff cap were not exercised");
        return "Meteor fire cultivation, fire damage, large bodies, probabilistic cells, terrain, burning triggers and restore passed: " + cases;
    }

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 12,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var p in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(p.Key), Value = p.Value });
        unit.BaseProperties = unit.Properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList();
        return unit;
    }
}
