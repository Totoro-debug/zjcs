using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFrigidAuraHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Frigid aura: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0, cold = 0, resisted = 0;
        foreach (int rank in new[] { 1, 2, 4 })
        foreach (int level in new[] { 1, 2 })
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int distance in new[] { 1, 2, 3 })
        foreach (bool large in new[] { false, true })
        foreach (int resistance in new[] { -1, 0, 1 })
        foreach (int seed in new[] { 7, 91 })
        foreach (bool forbidden in new[] { false, true })
        {
            var actor = Unit(1, 1, 6, 6, formula);
            var target = Unit(2, 2, 6 + distance * direction[0], 6 + distance * direction[1], formula);
            if (large) target.BodyOffsets.Add(new FightGridPoint(-direction[0], -direction[1]));
            var ally = Unit(3, 1, 6 - direction[1], 6 + direction[0], formula);
            Set(target, formula, "EffectDodgeValue", resistance * 100000);
            var aura = catalog.CreateStatus(25116, 1, 1, 725116, 25116, 10607, rank, level, 8);
            actor.Statuses.Add(aura);
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 10, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            state.Units.AddRange(new[] { actor, target, ally });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            if (forbidden) FightStatusLifecycle.SetForbidden(actor, aura, FightStatusLifecycle.ForbiddenStatusTrigger, true);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null;
            for (int count = 0; count < 12 && !state.Finished; count++)
            {
                step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                    "restoration changed range membership, round-end trigger, cold or random state");
                if (step.ActorId == 2 && step.ActorRound > 0) break;
            }
            bool inside = distance <= 2 || large && distance == 3;
            bool expected = inside && !forbidden;
            var watchers = target.Statuses.Where(status => status.ClassId == 24391).ToArray();
            Check(watchers.Length == (expected ? 1 : 0), "range/forbidden membership differs: " + distance + "/" + large + "/" + forbidden);
            Check(actor.Statuses.All(status => status.ClassId != 24391) && ally.Statuses.All(status => status.ClassId != 24391),
                "an enemy aura attached to the caster or a friend");
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 12170).ToArray();
            Check(hits.Length == (expected ? 1 : 0), "aura did not strike exactly once at the enemy's turn end");
            if (expected)
            {
                var watch = watchers.Single();
                var hit = hits.Single();
                var childProps = catalog.CalculateSkillProps(12170, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value);
                long expectedDamage = -(long)((1000 * formula.ReadProperty(childProps, 200)
                    + formula.ReadProperty(childProps, 190)) * 1000 / 1010);
                Check(hit.SourceId == 1 && hit.TargetId == 2 && hit.SkillItemId == 10607
                    && hit.HitEntityId == 12171 && hit.DamageEntityId == 12172 && hit.ElementType == 101
                    && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                    && step.SkillCasts.Any(cast => cast.SkillEntityId == 12170 && cast.ActorId == 1 && cast.Slot < 0),
                    "water child damage lost the original graph or passive caster/source");
                Check(hit.Damage == expectedDamage, "child damage ignored original cultivation: " + rank + "/" + level
                    + ", actual=" + hit.Damage + ", expected=" + expectedDamage);
                Check(watch.CreatorId == 1 && watch.ApplyingSkillItemId == 10607
                    && watch.SourceSkillRank == rank && watch.SourceSkillLevel == level
                    && watch.SourceSubRank == 8 && watch.RoundEndTriggeredCount == 1
                    && !watch.CanDispersed && watch.DurationRound == -1,
                    "the range watcher lost cultivation, source, count or lifetime");
                Check(step.StatusViewEvents.Any(view => view.Kind == FightBattleStatusViewEventKind.Invoke
                    && view.OwnerId == 2 && view.Status.ClassId == 24391), "the original round-end client invocation was omitted");
                var chilled = target.Statuses.SingleOrDefault(status => status.ClassId == 24266);
                if (chilled != null)
                {
                    cold++;
                    Check(resistance <= 0 && chilled.StackCount == 1 && chilled.DurationRound == 2
                        && chilled.ApplyingSkillItemId == 10607 && chilled.SourceSkillRank == rank && chilled.SourceSkillLevel == level,
                        "cold bypassed resistance or lost cultivation/two target turns");
                }
                else { resisted++; Check(resistance >= 0, "a guaranteed cold application was lost"); }
                string saved = FightBattleCheckpointSerializer.Save(state);
                Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
                    "replay duplicated aura damage, cold or a chance roll");
                var legacy = FightBattleCheckpointSerializer.Load(saved);
                var legacyWatch = legacy.Units.Single(unit => unit.Id == 2).Statuses.Single(status => status.ClassId == 24391);
                legacyWatch.RoundEndRulesSupported = false; legacyWatch.RoundEndSkills.Clear();
                FightBattleExecutor.Restore(legacy, formula, new FightDamageSettlement(), catalog, items);
                Check(legacyWatch.RoundEndRulesSupported && legacyWatch.RoundEndSkills.Single().Source == 3
                    && legacyWatch.RoundEndTriggeredCount == 1, "definition restoration lost a saved round-end count");
                // Range transitions use the same step-boundary state consumed by movement.
                target.X = 11; target.Y = 11;
                executor.Step(state.Sequence, state.Counter + 1);
                Check(target.Statuses.All(status => status.ClassId != 24391), "leaving the aura retained its child watcher");
                target.X = 6 + direction[0]; target.Y = 6 + direction[1];
                executor.Step(state.Sequence, state.Counter + 1);
                Check(target.Statuses.Count(status => status.ClassId == 24391) == 1, "re-entry omitted or duplicated the watcher");
                FightStatusLifecycle.SetForbidden(actor, aura, FightStatusLifecycle.ForbiddenStatusTrigger, true);
                executor.Step(state.Sequence, state.Counter + 1);
                Check(target.Statuses.All(status => status.ClassId != 24391), "a forbidden aura retained its child watcher");
                FightStatusLifecycle.SetForbidden(actor, aura, FightStatusLifecycle.ForbiddenStatusTrigger, false);
                executor.Step(state.Sequence, state.Counter + 1);
                Check(target.Statuses.Count(status => status.ClassId == 24391) == 1, "releasing the aura did not restore its watcher");
                actor.Hp = 0; actor.Turn.Hp = 0;
                executor.Step(state.Sequence, state.Counter + 1);
                Check(target.Statuses.All(status => status.ClassId != 24391), "the dead caster retained a forbidden aura watcher");
            }
            cases++;
        }
        Check(cold > 0 && resisted > 0, "the original 50% cold outcomes were not both exercised");
        return "Frigid aura passed: " + cases + " range, body, round-end, child, cultivation, cold, exit/re-entry, forbidden and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 4,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 10000000, MaxHp = 10000000, LastMaxHp = 10000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 10000 : 100;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", unit.Turn.Speed }, { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseElementAdd", 1000 }, { "BaseElementReduce", 1000 }, { "CritAvoidPercent", 10000 } })
            Set(unit, formula, pair.Key, pair.Value);
        return unit;
    }

    private static void Set(FightBattleUnitState unit, FightDamageFormula formula, string name, long value)
    {
        int id = formula.PropertyId(name); Check(id > 0, "unknown original property: " + name);
        unit.Properties.RemoveAll(property => property.Id == id);
        unit.Properties.Add(new FightPropertyValue { Id = id, Value = value });
    }
}
