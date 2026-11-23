using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleChaosRuneHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var skill = catalog.CreateSkill(11380, 0, 10059, 4, 2, 8,
            catalog.CalculateSkillProps(11380, 4, 2, 8).Select(pair =>
                new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        var definition = skill.HitGraphEntities.Single(value => value.EntityId == 11381);
        Check(definition.AppliedStatuses.Count == 1,
            $"official hit has {definition.AppliedStatuses.Count} status applications");
        var configured = definition.AppliedStatuses[0];
        Check(configured.StatusId == 24217 && Math.Abs(configured.Chance - .4f) < .0001f
            && configured.AffectedByProp, "the original 40-percent confusion application is missing");
        int applied = 0;
        bool resumed = false;
        var seeds = new Random(11380);
        for (int sample = 1; sample <= 100; sample++)
        {
            var caster = Unit(1, 1, 3, 1100, formula);
            var target = Unit(2, 2, 6, 900, formula);
            var outside = Unit(3, 2, 7, 800, formula);
            double chance = catalog.CalculateStatusApplicationRate(caster, target, 24217,
                configured.Chance, configured.AffectedByProp, formula);
            Check(Math.Abs(chance - .4) < .0001, $"neutral status properties produced chance {chance}");
            var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 6,
                RandomSeed = seeds.Next(1, FightRandomStream.Modulus),
                Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
            skill = catalog.CreateSkill(11380, 0, 10059, 4, 2, 8,
                catalog.CalculateSkillProps(11380, 4, 2, 8).Select(pair =>
                    new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            skill.ResetCooldownAtStart = true;
            skill.LimitedTimes = 1;
            caster.Skills.Add(skill);
            caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.Units.AddRange(new[] { caster, target, outside });
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(new[] { target.Id, outside.Id });
            checkpoint.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
            var hits = step.DamageEvents.Where(value => value.SkillItemId == 10059).ToArray();
            Check(hits.Length == 1, $"turn {step.ActorId} created {hits.Length} hits from casts "
                + string.Join(", ", step.SkillCasts.Select(cast => cast.SkillItemId)));
            var hit = hits[0];
            Check(step.ActorId == caster.Id && hit.TargetId == target.Id && hit.HitEntityId == 11381
                && hit.DamageEntityId == 11382 && hit.ElementType == 104 && hit.DamageType == 1
                && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure && Math.Abs(hit.Delay - 1f) < .0001f,
                "the original dark-element hit changed range, timing, target or damage");
            Check(outside.Statuses.All(status => status.ClassId != 24217), "the single-target skill confused another enemy");
            var confusion = target.Statuses.SingleOrDefault(status => status.ClassId == 24217);
            if (confusion == null) continue;
            applied++;
            Check(confusion.ActionType == 54 && confusion.DurationRound == 1 && confusion.CreatorId == caster.Id
                && confusion.ApplyingSkillItemId == 10059 && confusion.SourceSkillRank == 4 && confusion.SourceSkillLevel == 2,
                "confusion lost its action restriction, duration or cultivated provenance");
            if (resumed) continue;
            var stored = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(stored);
            var restoredExecutor = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
            Check(restoredExecutor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == stored,
                "replay changed an already settled confusion roll");
            for (int turn = 0; turn < 5 && target.Statuses.Contains(confusion); turn++)
            {
                executor.Step(checkpoint.Sequence, checkpoint.Counter + 1);
                restoredExecutor.Step(restored.Sequence, restored.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(restored) == FightBattleCheckpointSerializer.Save(checkpoint),
                    "resuming before confusion ended changed the next battle state");
            }
            Check(!target.Statuses.Contains(confusion), "the original confusion did not expire");
            resumed = true;
        }
        Check(resumed && applied >= 25 && applied <= 55, $"100 unbiased hits produced {applied} confusion applications");
        return $"Chaos rune passed: {applied}/100 confusion applications, dark damage, timing, range, source, expiration and restore.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 17,
            Level = 70, SubRank = 8, X = x, Y = 5, MoveMask = 5, Hp = 100000, MaxHp = 100000, LastMaxHp = 100000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = speed;
        foreach (var property in new[] { ("Attack", 1000), ("Defence", 10), ("MaxHp", 100000), ("Speed", speed),
            ("BaseEffectRate", 10000), ("BaseEffectDodge", 10000), ("BaseCritRatePercentValue", 10000),
            ("BaseCritAvoidPercentValue", 10000), ("BaseElementMaster", 10000), ("BaseElementResistance", 10000) })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(property.Item1), Value = property.Item2 });
        return unit;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Chaos rune: " + message);
    }
}
