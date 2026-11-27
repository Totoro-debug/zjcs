using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleHuntJudgmentHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int seed in new[] { 13, 104729 })
        foreach (bool largeSource in new[] { false, true })
        foreach (bool largeTarget in new[] { false, true })
        foreach (string mode in new[] { "plain", "blocked", "no-child" })
        foreach (var offset in new[] { new FightGridPoint(0, 4), new FightGridPoint(4, 0),
            new FightGridPoint(0, -4), new FightGridPoint(-4, 0) })
        {
            var actor = Unit(1, 1, 6, 6, formula);
            var target = Unit(2, 2, 6 + offset.X, 6 + offset.Y, formula);
            int dx = Math.Sign(offset.X), dy = Math.Sign(offset.Y);
            var tangent = new FightGridPoint(dy == 0 ? 0 : 1, dx == 0 ? 0 : 1);
            if (largeSource) actor.BodyOffsets.Add(tangent);
            if (largeTarget) target.BodyOffsets.Add(tangent);
            var values = catalog.CalculateSkillProps(11820, rank, level, 5);
            var skill = catalog.CreateSkill(11820, 0, 10074, rank, level, 5,
                values.Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.ResetCooldownAtStart = true; skill.LimitedTimes = 1;
            if (mode == "no-child") skill.CanTriggerChild = false;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = seed, MaxRound = 20,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            cp.Units.AddRange(new[] { actor, target });
            if (mode == "blocked") cp.Units.Add(Unit(3, 1, 6 + dx, 6 + dy, formula));
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var run = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var resumed = FightBattleExecutor.Begin(copy, formula, new FightDamageSettlement(), catalog, items);
            bool exercised = false;
            for (int i = 0; i < 20 && !cp.Finished; i++)
            {
                var step = run.Step(cp.Sequence, cp.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                    "Hunt Judgment changed pull, damage or statuses after a cold continuation.");
                if (!step.SkillCasts.Any(c => c.SkillEntityId == 11820)) continue;
                exercised = true;
                var parent = step.SkillCasts.Single(c => c.SkillEntityId == 11820);
                var children = step.SkillCasts.Where(c => c.SkillEntityId == 11830).ToArray();
                var hits = step.DamageEvents.Where(h => h.SourceId == 1).ToArray();
                var carriers = hits.Where(h => h.HitEntityId == 11821).ToArray();
                var physical = hits.Where(h => h.HitEntityId == 11823).ToArray();
                var moves = step.Movements.Where(m => m.UnitId == 2).ToArray();
                int body = largeTarget ? 2 : 1;
                bool pulled = mode == "plain";
                Check(parent.SkillItemId == 10074 && carriers.Length == body
                    && carriers.All(h => h.Damage == 0 && h.DamageType == 0 && h.DamageEntityId == 11822)
                    && children.Length == (mode == "no-child" ? 0 : body)
                    && children.All(c => Math.Abs(c.Delay - .05f) < .00001f),
                    "Hunt Judgment changed its nondamaging grab, per-body children or authored child delay.");
                int facing = dx < 0 ? 1 : dx > 0 ? 3 : dy > 0 ? 2 : 0;
                Check(parent.Presentation.FacingIndex == facing,
                    "Hunt Judgment did not capture the actual four-direction release facing.");
                Check(target.X == 6 + (pulled ? dx : offset.X) && target.Y == 6 + (pulled ? dy : offset.Y)
                    && moves.Length == (pulled ? 1 : 0),
                    "Hunt Judgment did not honor rotated exact landing, complete bodies, blocking or child exclusion: "
                        + mode + "/" + facing + "/" + largeSource + "/" + largeTarget);
                if (pulled)
                {
                    Check(moves[0].MoveBehaviourId == 11833 && Math.Abs(moves[0].Delay - .45f) < .00001f
                        && Math.Abs(moves[0].MoveDuration - .1f) < .00001f,
                        "Hunt Judgment changed its nested hit/pull timing or original move action.");
                    long expected = -(long)((1000d * values[201] / 10000d + values[191]) * 1000d / 1010d);
                    Check(physical.Length == 3 * body && physical.All(h => h.Damage == expected
                        && h.SkillEntityId == 11820 && h.DamageEntityId == 11824 && h.TargetId == 2
                        && h.SkillItemId == 10074 && h.ElementType == 0 && h.DamageType == 1 && !h.TrueDamage),
                        "Hunt Judgment changed its three cultivated physical strikes or full-body occurrences.");
                    foreach (float at in new[] { 1.1f, 1.2f, 1.3f })
                        Check(physical.Count(h => Math.Abs(h.Delay - at) < .00001f) == body,
                            "Hunt Judgment changed an authored strike delay.");
                    Check(cp.StatusSeconds.PendingHits.Count == hits.Length,
                        "Hunt Judgment did not defer each authored hit response to its actual presentation time: "
                            + cp.StatusSeconds.PendingHits.Count + "/" + hits.Length + "/physical=" + physical.Length);
                }
                else Check(physical.Length == 0 && target.Statuses.All(s => s.ClassId != 24269),
                    "Hunt Judgment hit or launched a target outside its actual front area after a failed pull.");
                string saved = FightBattleCheckpointSerializer.Save(cp);
                Check(run.Step(cp.Sequence, cp.Counter).Replayed && saved == FightBattleCheckpointSerializer.Save(cp),
                    "Replaying Hunt Judgment moved or damaged its target again.");
                var after = FightBattleCheckpointSerializer.Load(saved);
                var restored = FightBattleExecutor.Restore(after, formula, new FightDamageSettlement(), catalog, items);
                Check(restored.Step(after.Sequence, after.Counter).Replayed && FightBattleCheckpointSerializer.Save(after) == saved,
                    "Restoring Hunt Judgment changed its completed pull/strike receipt.");
                if (pulled)
                {
                    double firstImpact = physical.Min(h => h.Delay) + .01d;
                    run.AdvanceStatusSeconds(firstImpact); restored.AdvanceStatusSeconds(firstImpact);
                    Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(after),
                        "Hunt Judgment's first floating impact diverged after restore.");
                    var airborne = target.Statuses.Single(s => s.ClassId == 24269);
                    Check(airborne.CreatorId == 1 && airborne.ApplyingSkillItemId == 10074
                        && airborne.SourceSkillRank == rank && airborne.SourceSkillLevel == level
                        && airborne.SourceSubRank == 5 && airborne.Components.Seconds.Started,
                        "Hunt Judgment lost its original cultivated airborne status or started it before impact.");
                    bool recovery = false;
                    for (int tick = 0; tick < 24 && (!double.IsInfinity(run.NextStatusSecondsDelay)); tick++)
                    {
                        double next = run.NextStatusSecondsDelay;
                        run.AdvanceStatusSeconds(next); restored.AdvanceStatusSeconds(next);
                        recovery |= target.Statuses.Any(s => s.ClassId == 24270);
                        Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(after),
                            "Hunt Judgment's successive hits, airborne refresh or recovery diverged after restore.");
                    }
                    Check(recovery && cp.StatusSeconds.PendingHits.Count == 0
                        && target.Statuses.All(s => s.ClassId != 24269 && s.ClassId != 24270),
                        "Hunt Judgment's final airborne/recovery did not complete after the three authored impacts.");
                }
                break;
            }
            Check(exercised, "Hunt Judgment's actual root skill was never released.");
            cases++;
        }
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (bool reverse in new[] { false, true })
        foreach (var offset in new[] { new FightGridPoint(0, 4), new FightGridPoint(4, 0),
            new FightGridPoint(0, -4), new FightGridPoint(-4, 0) })
        {
            var actor = Unit(1, 1, 6, 6, formula);
            int dx = Math.Sign(offset.X), dy = Math.Sign(offset.Y);
            var first = Unit(2, 2, 6 + offset.X, 6 + offset.Y, formula);
            var second = Unit(3, 2, first.X + (dy == 0 ? 0 : 1), first.Y + (dx == 0 ? 0 : 1), formula);
            var skill = catalog.CreateSkill(11820, 0, 10074, rank, level, 5,
                catalog.CalculateSkillProps(11820, rank, level, 5).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.ResetCooldownAtStart = true; skill.LimitedTimes = 1;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 104729, MaxRound = 20,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            cp.Units.Add(actor); cp.Units.AddRange(reverse ? new[] { second, first } : new[] { first, second });
            var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var step = runner.Step(cp.Sequence, cp.Counter + 1).Event;
            var pulls = step.Movements.Where(m => m.MoveBehaviourId == 11833).ToArray();
            var physical = step.DamageEvents.Where(h => h.HitEntityId == 11823).ToArray();
            Check(step.SkillCasts.Count(c => c.SkillEntityId == 11830) == 2
                && step.DamageEvents.Count(h => h.HitEntityId == 11821) == 2
                && pulls.Length == 1 && physical.Length == 3 && physical.All(h => h.TargetId == pulls[0].UnitId),
                "Hunt Judgment did not grab both enemies or respect occupancy after the first successful exact pull.");
            var pulled = cp.Units.Single(u => u.Id == pulls[0].UnitId);
            Check(pulled.X == 6 + dx && pulled.Y == 6 + dy
                && !new FightGridPoint(first.X, first.Y).Equals(new FightGridPoint(second.X, second.Y)),
                "Hunt Judgment overlapped enemies at its exact front landing cell.");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            Check(FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items)
                .Step(restored.Sequence, restored.Counter).Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Hunt Judgment repeated its ordered multi-target pulls after restoration.");
            cases++;
        }
        CheckCapturedFacing();
        return "Hunt Judgment passed: " + cases + " cultivated four-facing, full-body, exact blocked and ordered multi-target landing, child exclusion, timing, physical damage, airborne and restoration cases; captured-facing guards passed.";
    }

    static void CheckCapturedFacing()
    {
        var source = new FightBattleUnitState { X = 6, Y = 6, Direction = 0 };
        var target = new FightBattleUnitState { X = 6, Y = 10, MoveMask = 5 };
        var hit = new FightBattleHitDefinition { DamageId = 11832, HitMove = new FightBattleSkillMove {
            MoveBehaviourId = 11833, Direction = 0, Distance = -1, TowardsTargetPos = true, OffsetY = 1 } };
        var context = new FightHitMovementContext { Source = source, Target = target, Hit = hit,
            Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 },
            Occupied = new HashSet<FightGridPoint> { new FightGridPoint(6, 6) },
            Cast = new FightBattleSkillCastPresentation { FacingIndex = 2 } };
        var rule = FightHitMovementRules.For(hit);
        var path = rule.Plan(context);
        Check(path != null && path.Last().Equals(new FightGridPoint(6, 7)),
            "Hunt Judgment used the later unit facing instead of its captured release facing.");
        context.Cast = null;
        Check(rule.Plan(context) == null, "Hunt Judgment guessed a missing release facing.");
        context.Cast = new FightBattleSkillCastPresentation { FacingIndex = 99 };
        Check(rule.Plan(context) == null, "Hunt Judgment accepted an invalid release facing.");
    }

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var u = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y, CharacterType = 1,
            Profession = 7, Level = 50, SubRank = 5, Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000, MoveMask = 5 };
        u.Turn.Id = id; u.Turn.Hp = u.Hp; u.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var p in new Dictionary<string, long> { { "MaxHp", u.MaxHp }, { "Attack", 1000 }, { "Defence", 10 },
            { "Speed", u.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 }, { "CritAvoidPercent", 10000 } })
            u.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(p.Key), Value = p.Value });
        return u;
    }
}
