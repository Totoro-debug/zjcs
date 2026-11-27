using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSoulGuardianHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static List<FightPropertyValue> Values(IReadOnlyDictionary<int, long> props) =>
        props.Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList();
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (long hp in new long[] { 1, 2, 3, 999, 1000 })
        foreach (int seed in new[] { 13, 104729 })
        {
            var actor = Unit(1, 1, hp, formula); var enemy = Unit(2, 2, 10000, formula);
            var listener = catalog.CreateStatus(25093, 1, 1, 1, 25093, 10590, rank, level, 5, actor.Properties);
            actor.Statuses.Add(listener);
            Check(listener.HasRoundStartTrigger && listener.RoundStartRulesSupported
                && listener.RoundStartMaxCount == 1 && !listener.RoundStartTryAtStart,
                "Soul Guardian lost its original once-only round-start definition.");
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = seed, MaxRound = 20,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            cp.Units.AddRange(new[] { actor, enemy });
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var run = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var replay = FightBattleExecutor.Begin(copy, formula, new FightDamageSettlement(), catalog, items);
            int casts = 0; long paid = 0;
            for (int i = 0; i < 8 && !cp.Finished; i++)
            {
                var step = run.Step(cp.Sequence, cp.Counter + 1).Event;
                replay.Step(copy.Sequence, copy.Counter + 1);
                casts += step.SkillCasts.Count(c => c.ActorId == 1 && c.SkillEntityId == 11870 && c.SkillItemId == 10590);
                foreach (var cost in step.DamageEvents.Where(e => e.SkillEntityId == 11870 && e.IsSkillCost))
                {
                    Check(cost.SourceId == 1 && cost.TargetId == 1 && cost.AfterHp >= 1 && cost.Damage < 0,
                        "Soul Guardian cost did not retain one HP or its actual source.");
                    paid -= cost.Damage;
                }
                Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                    "Soul Guardian continuation diverged after restoring before the opening passive.");
                string saved = FightBattleCheckpointSerializer.Save(cp);
                Check(run.Step(cp.Sequence, cp.Counter).Replayed && saved == FightBattleCheckpointSerializer.Save(cp),
                    "Soul Guardian charged HP again during repeated receipt replay.");
            }
            long expected = Math.Min((long)(hp * 0.5), hp - 1);
            Check(casts == 1 && paid == expected && actor.Hp == hp - expected,
                "Soul Guardian did not pay the original current-HP cost exactly once: " + hp + "/" + paid + "/" + casts
                    + "/HP=" + actor.Hp + "/Max=" + actor.MaxHp + "/seed=" + seed);
            var guards = actor.Statuses.Where(s => s.ClassId == 24372).ToArray();
            Check(guards.Length == 1, "Soul Guardian protection was missing or duplicated.");
            {
                var guard = guards[0];
                Check(!guard.CanDispersed && guard.DurationRound == -1 && guard.CreatorId == 1
                    && guard.ApplyingSkillItemId == 10590 && guard.SourceSkillRank == rank
                    && guard.SourceSkillLevel == level && guard.CustomShieldValue == expected
                    && guard.ShieldKind == FightBattleShieldKind.None,
                    "Soul Guardian protection lost cultivation, HP-cost value, or permanent nondispellable lifetime.");
                var expectedProps = catalog.CalculateSkillProps(24372, rank, level, 5);
                Check(expectedProps.All(p => guard.StatusProperties.Any(v => v.Id == p.Key && v.Value == p.Value)),
                    "Soul Guardian protection changed original damage reduction properties.");
            }
            cases++;
        }
        // Shared formula boundaries also cover simultaneous current/max HP costs,
        // negative coefficients and fractional truncation without killing the payer.
        foreach (long hp in new long[] { 0, 1, 2, 3, 999, 1000 })
        foreach (long current in new long[] { -5000, 0, 3333, 5000, 20000 })
        foreach (long maximum in new long[] { -5000, 0, 2500 })
        {
            var props = new List<FightPropertyValue> {
                new FightPropertyValue { Id = formula.PropertyId("SkillCostByCurHp"), Value = current },
                new FightPropertyValue { Id = formula.PropertyId("SkillCostByMaxHp"), Value = maximum } };
            long expected = hp <= 0 ? 0 : (long)Math.Min(hp - 1,
                hp * Math.Max(0, current) / 10000d + 1000 * Math.Max(0, maximum) / 10000d);
            Check(formula.CalculateSkillCostHp(props, hp, 1000) == expected, "Shared official HP-cost formula boundary changed.");
            cases++;
        }
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        {
            var actor = Unit(1, 1, 10000, formula); var enemy = Unit(2, 2, 10000, formula);
            enemy.Turn.Speed = 2000;
            enemy.Properties.Single(p => p.Id == formula.PropertyId("Speed")).Value = 2000;
            actor.Statuses.Add(catalog.CreateStatus(25093, 1, 1, 1, 25093, 10590, rank, level, 5, actor.Properties));
            var skill = catalog.CreateSkill(10600, 0, 10008, 1, 1, 5,
                Values(catalog.CalculateSkillProps(10600, 1, 1, 5)));
            skill.ResetCooldownAtStart = true;
            enemy.Skills.Add(skill); enemy.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 104729, MaxRound = 20,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 } };
            cp.Units.AddRange(new[] { actor, enemy });
            var run = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            int compared = 0;
            for (int i = 0; i < 10 && !cp.Finished; i++)
            {
                var before = FightBattleCheckpointSerializer.Save(cp);
                bool protectedBefore = actor.Statuses.Any(s => s.ClassId == 24372);
                var incoming = run.Step(cp.Sequence, cp.Counter + 1).Event.DamageEvents
                    .Where(hit => hit.SourceId == 2 && hit.TargetId == 1 && hit.Damage < 0).ToArray();
                if (!protectedBefore || incoming.Length == 0) continue;
                var without = FightBattleCheckpointSerializer.Load(before);
                without.Units.Single(u => u.Id == 1).Statuses.RemoveAll(s => s.ClassId == 24372);
                var other = FightBattleExecutor.Restore(without, formula, new FightDamageSettlement(), catalog, items)
                    .Step(without.Sequence, without.Counter + 1).Event.DamageEvents
                    .Where(hit => hit.SourceId == 2 && hit.TargetId == 1 && hit.Damage < 0).ToArray();
                Check(other.Length == incoming.Length && incoming.Length > 0,
                    "Soul Guardian's comparison did not replay the same incoming attack.");
                for (int h = 0; h < incoming.Length; h++)
                    Check(incoming[h].SkillEntityId == other[h].SkillEntityId && incoming[h].HitEntityId == other[h].HitEntityId
                        && incoming[h].Damage > other[h].Damage && incoming[h].AfterHp > 0,
                        "Soul Guardian did not reduce the same real incoming hit while retaining a living payer.");
                compared += incoming.Length;
            }
            Check(compared > 0, "Soul Guardian's real incoming damage comparison was not exercised.");
            cases++;
        }
        return "Soul Guardian passed: " + cases + " opening/cultivation/HP-floor/seed/replay, shared cost boundaries and actual incoming damage comparisons.";
    }

    static FightBattleUnitState Unit(int id, int side, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 7,
            Level = 50, SubRank = 5, X = 6, Y = side == 1 ? 6 : 7, Hp = hp, MaxHp = 10000, LastMaxHp = 10000, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var p in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", 10000 }, { "Speed", unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(p.Key), Value = p.Value });
        return unit;
    }
}
