using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSoulSplashHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Soul Splash: " + message); }
    static List<FightPropertyValue> Values(IReadOnlyDictionary<int, long> values) =>
        values.Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList();
    static FightBattleUnitState Unit(int id, int side, int x, int y, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = id == 1 ? 1 : 2,
            Profession = 7, Level = 50, SubRank = 5, X = x, Y = y, Direction = 2,
            Hp = hp, MaxHp = 1000000, LastMaxHp = 1000000, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.BodyOffsets.Add(new FightGridPoint(0, 0));
        foreach (var p in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", 1000000 }, { "Speed", unit.Turn.Speed }, { "CritAvoidPercent", 1000000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(p.Key), Value = p.Value });
        return unit;
    }
    static FightBattleStatusState Listener(FightOfficialCombatCatalog catalog, FightBattleUnitState actor, int rank, int level)
    {
        var status = catalog.CreateStatus(25024, actor.Id, actor.Id, 1, 25024, 10545, rank, level, 5, actor.Properties);
        Check(status.HasKillSkill && status.KillSkillRulesSupported && status.KillSkillEntityId == 11280
            && status.KillSkillCustomOverflowDamage && status.KillSkillUseCustomDamage
            && status.KillSkillCustomDamagePropertyId == 233 && status.KillSkillFilterSelf
            && status.KillSkillFilterClassIds.SequenceEqual(new[] { 3051, 5401, 5402, 5403, 5404 }),
            "current kill definition, custom property or exclusions changed");
        return status;
    }
    static FightKillDamageContext Context(FightDamageFormula formula, long damage, long before,
        bool pvp, bool cure = false, bool hasValue = true)
    {
        return new FightKillDamageContext(new FightBattleDamageEvent {
            DamageValue = damage, HasDamageValue = hasValue, BeforeHp = before,
            AfterHp = Math.Max(0L, before + Math.Max(-before, damage)),
            IsCure = cure, HasDamagePosition = true, DamageX = 7, DamageY = 8, Delay = .7f,
            ShieldDamage = -12345 }, new FightBattleSkillCastPresentation { FacingIndex = 3 }, pvp, formula);
    }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (var pair in new[] { new long[] { -150, 100, 50 }, new long[] { -100, 100, 0 },
            new long[] { -99, 100, 0 }, new long[] { long.MinValue, 1, long.MaxValue } })
        {
            var context = Context(formula, pair[0], pair[1], false);
            Check(context.OverflowDamage == pair[2], "post-shield/exact-lethal/overflow bound changed"); cases++;
        }
        Check(!Context(formula, -150, 100, false, cure: true).KilledTarget
            && !Context(formula, -150, 100, false, hasValue: false).KilledTarget,
            "a cure or unknown damage value became a killing hit");
        var direct = new FightKillDamageContext(new FightBattleDamageEvent {
            HasDamageValue = true, DamageValue = 100, BeforeHp = 100, AfterHp = 0 }, null, false, formula);
        Check(direct.KilledTarget && direct.Damage == 100 && direct.OverflowDamage == 0,
            "an explicit direct killing result was classified by its numeric sign"); cases++;
        var source = new FightBattleSkillDefinition { EntityId = 990745, ItemId = 10039,
            ActionType = 2, CanTriggerChild = true };
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int character in new[] { 2, 3, 7 })
        foreach (bool pvp in new[] { false, true })
        {
            var actor = Unit(1, 1, 1, 1, 1000, formula);
            var defeated = Unit(2, 2, 2, 4, 0, formula); defeated.CharacterType = character;
            var status = Listener(catalog, actor, rank, level); actor.Statuses.Add(status);
            FightStatusPropertyRuntime.RebuildAll(new[] { actor, defeated }, catalog, formula);
            foreach (long overflow in new long[] { 0, 1, 7, 19, 1001 })
            {
                status.KillSkillTriggeredCount = 0;
                var invocations = new List<FightBattleStatusSkillInvocation>();
                var context = Context(formula, -100 - overflow, 100, pvp);
                int count = FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, source,
                    new[] { actor, defeated }, catalog, new FightRandomStream(13), invocations, context);
                long property = status.StatusProperties.Single(v => v.Id == 233).Value;
                long expected = (long)(overflow * (property / 10000.0));
                if (pvp && character != 2) expected = (long)(expected * (character == 3 ? 2000m : 3333m) / 10000m);
                Check(count == 1 && invocations.Count == 1 && invocations[0].HasCustomDamage
                    && invocations[0].CustomDamage == expected && invocations[0].AllowDeadTarget
                    && invocations[0].HitOrigin.Value.Equals(new FightGridPoint(7, 8))
                    && invocations[0].Delay == .7f && invocations[0].HitFacing == 3,
                    "cultivated custom damage, PVP order, dead-target position or hit delay changed"); cases++;
            }
            foreach (int filtered in new[] { 3051, 5401, 5402, 5403, 5404 })
            {
                defeated.MonsterId = filtered;
                Check(FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, source,
                    new[] { actor, defeated }, catalog, new FightRandomStream(13),
                    new List<FightBattleStatusSkillInvocation>(), Context(formula, -200, 100, pvp)) == 0,
                    "an explicitly excluded class triggered a kill child"); cases++;
            }
            defeated.MonsterId = 0; source.CanTriggerChild = false;
            Check(FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, source,
                new[] { actor, defeated }, catalog, new FightRandomStream(13),
                new List<FightBattleStatusSkillInvocation>(), Context(formula, -200, 100, pvp)) == 0,
                "a child that forbids status triggers recursively splashed");
            source.CanTriggerChild = true;
            status.ForbiddenType = 1;
            Check(FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, source,
                new[] { actor, defeated }, catalog, new FightRandomStream(13),
                new List<FightBattleStatusSkillInvocation>(), Context(formula, -200, 100, pvp)) == 0,
                "a forbidden listener triggered a kill child");
            status.ForbiddenType = 0; status.KillSkillMaxCount = 0;
            Check(FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, source,
                new[] { actor, defeated }, catalog, new FightRandomStream(13),
                new List<FightBattleStatusSkillInvocation>(), Context(formula, -200, 100, pvp)) == 0,
                "a capped listener triggered a kill child");
            status.KillSkillMaxCount = -1; defeated.Id = actor.Id;
            Check(FightStatusTriggerRuntime.ApplyKillSkills(actor, defeated, source,
                new[] { actor, defeated }, catalog, new FightRandomStream(13),
                new List<FightBattleStatusSkillInvocation>(), Context(formula, -200, 100, pvp)) == 0,
                "the self exclusion was ignored"); cases += 3;
        }
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int parent in new[] { 10940, 11440 })
        {
            var actor = Unit(1, 1, 5, 5, 1000000, formula);
            var enemy = Unit(2, 2, 5, 6, 1000000, formula);
            actor.Statuses.Add(Listener(catalog, actor, rank, level));
            int item = parent == 10940 ? 10039 : 10065;
            var skill = catalog.CreateSkill(parent, 0, item, 3, 2, 5,
                Values(catalog.CalculateSkillProps(parent, 3, 2, 5)));
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 13, MaxRound = 20,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            checkpoint.Units.AddRange(new[] { actor, enemy });
            var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var startup = runner.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            Check(startup.SkillCasts.Count == 0 && actor.Statuses.Single(s => s.ClassId == 25024).Components.Start.Started,
                "the configured start component did not run once before the first combat cast");
            var bonus = actor.Statuses.SingleOrDefault(s => s.ClassId == 24471);
            Check(bonus != null && bonus.ApplyingSkillItemId == 10545 && bonus.SourceSkillRank == rank
                && bonus.SourceSkillLevel == level && bonus.SourceSubRank == 5,
                "the permanent single-segment modifier lost its start trigger or cultivation");
            var bare = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            bare.Units.Single(u => u.Id == 1).Statuses.RemoveAll(s => s.ClassId == 24471);
            FightStatusPropertyRuntime.RebuildAll(bare.Units, catalog, formula);
            var control = FightBattleExecutor.Restore(bare, formula, new FightDamageSettlement(), catalog, items);
            FightBattleDamageEvent[] Next(FightBattleExecutor executor)
            {
                for (int turn = 0; turn < 80 && !executor.State.Finished; turn++)
                {
                    var step = executor.Step(executor.State.Sequence, executor.State.Counter + 1).Event;
                    if (step.SkillCasts.Any(c => c.SkillEntityId == parent))
                        return step.DamageEvents.Where(h => h.SkillEntityId == parent && h.TargetId == 2).ToArray();
                }
                throw new InvalidOperationException("Soul Splash modifier parent did not cast: " + parent);
            }
            var enhanced = Next(runner); var normal = Next(control);
            Check(enhanced.Length == (parent == 10940 ? 1 : 6) && enhanced.Length == normal.Length
                && enhanced.Select((hit, index) => parent == 10940
                    ? hit.DamageValue < normal[index].DamageValue
                    : hit.DamageValue == normal[index].DamageValue).All(v => v),
                "the cultivated modifier did not strengthen single-segment damage exclusively"); cases++;
        }
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (long shield in new long[] { 0, 50, 1000000000 })
        foreach (bool body in new[] { false, true })
        foreach (bool pvp in new[] { false, true })
        {
            var actor = Unit(1, 1, 5, 5, 1000000, formula);
            var enemy = Unit(2, 2, 5, 6, 1, formula);
            if (body) enemy.BodyOffsets.Add(new FightGridPoint(1, 0));
            var bystander = Unit(3, 2, 5, 8, 1000000, formula);
            var other = Unit(4, 2, 7, 6, 1, formula);
            var outside = Unit(5, 2, 5, 9, 1000000, formula);
            var friend = Unit(6, 1, 4, 6, 1000000, formula);
            actor.Statuses.Add(Listener(catalog, actor, rank, level));
            var attack = catalog.CreateSkill(10940, 0, 10039, 3, 2, 5,
                Values(catalog.CalculateSkillProps(10940, 3, 2, 5)));
            actor.Skills.Add(attack); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            if (shield > 0) enemy.Statuses.Add(catalog.CreateStatus(24174, 2, 2, 2, 24174, 0, 3, 2, 5, null, shield));
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 13, MaxRound = 20, IsPvp = pvp,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            checkpoint.Units.AddRange(new[] { actor, enemy, bystander, other, outside, friend });
            var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null;
            for (int turn = 0; turn < 80 && !checkpoint.Finished; turn++)
            {
                var current = runner.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                if (current.SkillCasts.Any(c => c.SkillEntityId == 10940)) { step = current; break; }
            }
            Check(step != null, "the actual original physical parent never cast: rank=" + rank
                + " level=" + level + " shield=" + shield + " body=" + body + " pvp=" + pvp
                + " round=" + actor.SkillState.Round + " hp=" + enemy.Hp
                + " target=" + enemy.X + "," + enemy.Y + " hits=" + attack.Hits.Count);
            var hit = step.DamageEvents.Single(h => h.SkillEntityId == 10940 && h.TargetId == 2);
            var children = step.SkillCasts.Where(c => c.SkillEntityId == 11280).ToArray();
            if (shield == 1000000000)
                Check(hit.AfterHp == 1 && children.Length == 0, "shield-only damage triggered a kill child");
            else
            {
                Check(hit.AfterHp == 0 && children.Length == 1 && children[0].SkillItemId == 10545,
                    "a genuine killing hit did not collect exactly one sourced child");
                var splash = step.DamageEvents.Where(h => h.SkillEntityId == 11280).ToArray();
                long coefficient = actor.Statuses.Single(s => s.ClassId == 25024).StatusProperties.Single(p => p.Id == 233).Value;
                long expected = (long)((-(decimal)hit.DamageValue - hit.BeforeHp) * (decimal)(coefficient / 10000.0));
                Check(splash.Length == 2 && splash.Select(h => h.TargetId).OrderBy(v => v).SequenceEqual(new[] { 3, 4 })
                    && splash.All(h => h.HitEntityId == 11281 && h.DamageEntityId == 10023 && h.DamageType == 8
                        && h.ElementType == 102 && !h.IsCure && h.HasDamageValue && h.DamageValue == -expected)
                    && splash.All(h => Math.Abs(h.Delay - hit.Delay - .15f - .1f) < .0001f),
                    "death-cell range, fire true damage, original cell delays or post-shield overflow differs");
            }
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            Check(replay.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "restore/replay changed kill scalars, repeated splash or shared source data"); cases++;
        }
        return "Soul Splash passed: " + cases + " post-shield/PVP/exclusion/range/child/restoration cases.";
    }
}
