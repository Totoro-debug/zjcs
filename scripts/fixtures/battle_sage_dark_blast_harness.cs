using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSageDarkBlastHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (var cultivation in new[] { (Rank: 1, Level: 1), (Rank: 2, Level: 2), (Rank: 4, Level: 2) })
            Verify(catalog, formula, items, cultivation.Rank, cultivation.Level);
        return "Dark blast passed: three timed dark hits, four-cell single target, cultivated formula and replay.";
    }

    private static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level)
    {
        var caster = Unit(1, 1, 3, 1100, formula);
        var target = Unit(2, 2, 7, 900, formula);
        var outside = Unit(3, 2, 8, 800, formula);
        var props = catalog.CalculateSkillProps(12120, rank, level, caster.SubRank);
        var skill = catalog.CreateSkill(12120, 0, 10103, rank, level, caster.SubRank, props.Select(pair =>
            new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        skill.ResetCooldownAtStart = true;
        caster.Skills.Add(skill);
        caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 416239,
            Grid = new FightBattleGrid { Width = 12, Height = 12, DefaultGroundType = 2 } };
        checkpoint.Units.AddRange(new[] { caster, target, outside });
        var wave = new FightBattleWaveState();
        wave.UnitIds.AddRange(new[] { target.Id, outside.Id });
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var cast = step.SkillCasts.Single(value => value.SkillEntityId == 12120);
        var hits = step.DamageEvents.Where(value => value.SkillItemId == 10103).ToArray();
        Check(hits.Length == 3, $"the cast produced {hits.Length} hits");
        var definition = skill.HitGraphEntities.Single(value => value.EntityId == 12121);
        Check(definition.BasicProperty == 1 && definition.DamageProperty == 200 && definition.FixedDamageProperty == 190,
            "the original hit's attack and damage properties changed");
        var sourceProps = caster.Properties.ToDictionary(value => value.Id, value => value.Value);
        foreach (var prop in props)
        {
            sourceProps.TryGetValue(prop.Key, out long existing);
            sourceProps[prop.Key] = existing + prop.Value;
        }
        var targetProps = target.Properties.ToDictionary(value => value.Id, value => value.Value);
        var delays = new[] { .98f, 1.14f, 1.34f };
        long before = target.MaxHp;
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            Check(hit.SourceId == caster.Id && hit.TargetId == target.Id && hit.SkillEntityId == 12120
                && hit.HitEntityId == 12121 && hit.DamageEntityId == 10004 && hit.ElementType == 104
                && hit.DamageType == 1 && !hit.IsCure && !hit.TrueDamage && !hit.IsDamageTriggerStatus
                && Math.Abs(hit.Delay - cast.Delay - delays[i]) < .0001f,
                $"hit {i + 1} changed target, origin, effect, type or timing");
            var type = new FightDamageTypeData { Critical = hit.Critical, Dodge = hit.Dodge, Block = hit.Block,
                Blinding = hit.Blinding, Immunity = hit.Immunity };
            var request = new FightDamageFormulaRequest { SourceProps = sourceProps, TargetProps = targetProps,
                SkillDamageBasicPropType = 1, SkillDamagePropType = 200, SkillDamageFixedPropType = 190,
                SourceType = caster.CharacterType, TargetType = target.CharacterType,
                SourceLevel = caster.Level, TargetLevel = target.Level, SourceSubRank = caster.SubRank,
                TargetSubRank = target.SubRank, SourceProfession = caster.Profession, TargetProfession = target.Profession,
                TargetHp = (int)before, StageType = checkpoint.StageType, SourceElementType = 104 };
            long expected = formula.Calculate(request, type, elementConfrontation: hit.ElementConfrontation).Damage;
            Check(expected < 0 && expected == hit.Damage && hit.BeforeHp == before && hit.AfterHp == before + expected,
                $"rank {rank} level {level} hit {i + 1} settled {hit.Damage}, expected {expected}");
            before = hit.AfterHp;
        }
        Check(target.Hp == before && outside.Hp == outside.MaxHp && target.Statuses.Count == 0,
            "the three hits changed another unit or added a status");
        string stored = FightBattleCheckpointSerializer.Save(checkpoint);
        var restored = FightBattleCheckpointSerializer.Load(stored);
        var replay = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(replay.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == stored, "replay repeated the settled damage");
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 17,
            Level = 70, SubRank = 8, X = x, Y = 5, MoveMask = 5, Hp = 100000, MaxHp = 100000, LastMaxHp = 100000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = speed;
        foreach (var property in new[] { ("Attack", 1000), ("Defence", 10), ("MaxHp", 100000), ("Speed", speed),
            ("BaseCritRatePercentValue", 10000), ("BaseCritAvoidPercentValue", 10000),
            ("BaseElementMaster", 10000), ("BaseElementResistance", 10000) })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(property.Item1), Value = property.Item2 });
        return unit;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Dark blast: " + message);
    }
}
