using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleHurricaneGriefHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, applications = 0;
        var seeds = new Random(10598);
        foreach (int rank in new[] { 3, 4, 5 })
        foreach (int level in new[] { 1, 2, 10 })
        for (int sample = 0; sample < 8; sample++)
        {
            int seed = seeds.Next(1, FightRandomStream.Modulus);
            if (Cast(catalog, formula, items, rank, level, seed, 100, false)) applications++;
            Check(!Cast(catalog, formula, items, rank, level, seed, 102, false),
                "a non-wind cast applied the tear");
            Check(!Cast(catalog, formula, items, rank, level, seed, 100, true),
                "a forbidden listener applied the tear");
            cases++;
        }
        Check(applications > 0 && applications < cases,
            "both application outcomes must be exercised: " + applications + "/" + cases);
        return "Hurricane grief passed: " + cases + " cultivated wind cases, non-wind and forbidden controls, "
            + applications + " tears, real movement damage and checkpoint replay.";
    }

    private static bool Cast(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int seed, int element, bool forbidden)
    {
        var actor = Unit(1, 1, 5, 5, 1000, formula);
        var target = Unit(2, 2, 5, 6, 900, formula);
        target.Profession = 1;
        target.MoveDist = 1;
        target.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MoveDist"), Value = 1 });
        var attack = catalog.CreateSkill(10800, 0, 10028, rank, level, 8,
            Props(catalog, 10800, rank, level));
        // The common listener must use the resolved damage element, including
        // an element changed by other combat rules.
        attack.ElementType = element;
        attack.ResetCooldownAtStart = true;
        actor.Skills.Add(attack);
        actor.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
        var melee = catalog.CreateSkill(10610, 0, 10009, 1, 1, 8, Props(catalog, 10610, 1, 1));
        melee.ResetCooldownAtStart = true;
        target.Skills.Add(melee);
        target.SkillState.Slots.Add(catalog.CreateSkillSlot(melee, 0));
        var listener = catalog.CreateStatus(25099, 1, 1, 725099, 25099, 10598, rank, level, 8);
        actor.Statuses.Add(listener);
        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        checkpoint.Units.AddRange(new[] { actor, target });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(target.Id); checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        Check(target.MoveDist == 1, "the fixture lost its movement property during assembly");
        if (forbidden) listener.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
        var definition = listener.Components.DarkHit;
        Check(definition != null && definition.Supported && definition.Rate == 1f
            && definition.IgnoreTeammate && definition.Elements.SequenceEqual(new[] { 100 })
            && definition.Statuses.Count == 1 && definition.Statuses[0].EntityId == 24488
            && Math.Abs(definition.Statuses[0].BasePercent - .35f) < .000001f
            && definition.Statuses[0].AffectedByProp, "the original wind application definition changed");
        double chance = catalog.CalculateStatusApplicationRate(actor, target, 24488, .35f, true, formula);
        Check(Math.Abs(chance - .35) < .000001, "neutral properties changed the base chance: " + chance);
        var first = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        Check(first.ActorId == actor.Id && first.DamageEvents.Any(hit => hit.SourceId == actor.Id
            && hit.TargetId == target.Id && hit.SkillEntityId == 10800 && hit.ElementType == element
            && hit.Damage < 0 && !hit.IsCure), "the fixture did not land its resolved-element hit");
        Check(definition.TriggerCount == (element == 100 && !forbidden ? 1 : 0),
            "the listener counted a filtered hit or lost a wind hit");
        var tear = target.Statuses.SingleOrDefault(status => status.ClassId == 24488);
        if (tear == null) return false;
        Check(tear.CreatorId == actor.Id && tear.OwnerId == target.Id && tear.ApplyingSkillItemId == 10598
            && tear.SourceSkillRank == rank && tear.SourceSkillLevel == level && tear.SourceSubRank == 8
            && tear.DurationRound == 1 && tear.CanDispersed && tear.StatusType == 2,
            "the tear lost its cultivation, source or original lifetime");

        // Place the afflicted enemy just outside melee range. Its next ordinary
        // AI turn must move through the production action-start trigger path.
        target.X = 7; target.Y = 5;
        string before = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(before);
        var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        bool moved = false;
        for (int step = 0; step < 8 && !checkpoint.Finished; step++)
        {
            var result = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            resumed.Step(restored.Sequence, restored.Counter + 1);
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(restored),
                "resuming before movement changed the tear or damage");
            if (!result.Movements.Any(move => move.UnitId == target.Id)) continue;
            var hits = result.DamageEvents.Where(hit => hit.SkillItemId == 10598
                && (hit.SkillEntityId == 12660 || hit.SkillEntityId == 11950)).ToArray();
            Check(hits.Length == 1 && hits[0].SourceId == actor.Id && hits[0].TargetId == target.Id
                && hits[0].SkillEntityId == 12660 && hits[0].HitEntityId == 12661
                && hits[0].DamageEntityId == 10004 && hits[0].ElementType == 100
                && hits[0].DamageType == 1 && hits[0].Damage < 0 && !hits[0].TrueDamage && !hits[0].IsCure,
                "ordinary movement did not release the current attributed wind child: "
                    + string.Join(",", hits.Select(hit => hit.SkillEntityId + "/" + hit.HitEntityId)));
            Check(!FightSkillTriggerPolicy.AllowsStatus(catalog.CreateSkill(12660, -1, 10598, rank, level, 8,
                    Props(catalog, 12660, rank, level)), listener)
                && definition.TriggerCount == 1,
                "the non-triggering movement child recursively counted as another wind skill hit");
            string settled = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == settled,
                "receipt replay repeated the movement tear");
            moved = true;
            break;
        }
        Check(moved, "the afflicted enemy never took its ordinary movement turn");
        return true;
    }

    private static List<FightPropertyValue> Props(FightOfficialCombatCatalog catalog, int skill, int rank, int level)
        => catalog.CalculateSkillProps(skill, rank, level, 8)
            .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();

    private static FightBattleUnitState Unit(int id, int side, int x, int y, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y,
            CharacterType = 1, Profession = 13, Level = 70, SubRank = 8,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000, MoveDist = 0, MoveMask = 4 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = speed;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", speed }, { "BaseCritRatePercentValue", 10000 },
            { "BaseCritAvoidPercentValue", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Hurricane grief: " + message); }
}
