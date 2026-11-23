using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFireLifeHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Fire life: " + message);
    }

    private static FightBattleUnitState Unit(int id, int side, long hp, int speed,
        FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState
        {
            Id = id, Side = side, CharacterType = 1, Profession = 13, Level = 70, SubRank = 8,
            X = id == 1 ? 6 : 12, Y = 6, Hp = hp, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5
        };
        unit.Turn.Id = id;
        unit.Turn.Hp = hp;
        unit.Turn.Speed = speed;
        foreach (var pair in new Dictionary<int, long>
        {
            { formula.PropertyId("Attack"), 1000 },
            { formula.PropertyId("Defence"), 10 },
            { formula.PropertyId("MaxHp"), unit.MaxHp },
            { formula.PropertyId("Speed"), speed },
            { formula.PropertyId("BaseCureAdd"), 10000 },
            { formula.PropertyId("BaseBeCureAdd"), 10000 },
            { formula.PropertyId("BaseBlockPercentValue"), 10000 },
            { formula.PropertyId("BaseBlockAvoidPercentValue"), 10000 },
            { formula.PropertyId("BaseCritRatePercentValue"), 10000 },
            { formula.PropertyId("BaseCritAvoidPercentValue"), 10000 },
            { formula.PropertyId("BaseEffectRate"), 10000 },
            { formula.PropertyId("BaseEffectDodge"), 10000 }
        })
            if (pair.Key > 0) unit.Properties.Add(new FightPropertyValue { Id = pair.Key, Value = pair.Value });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        Check(catalog.IsSkillRulePackEntity(25098) && catalog.IsSkillRulePackEntity(12040)
            && catalog.IsSkillRulePackEntity(12041) && catalog.IsSkillRulePackEntity(12042),
            "the current 10597 rule pack was not installed");

        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int burningTiles in new[] { 0, 1, 2, 6, 7 })
        {
            CheckSelfTurn(catalog, formula, items, rank, level, burningTiles, 100000);
            cases++;
        }
        CheckNotAnotherUnitTurn(catalog, formula, items);
        CheckFullHpClipping(catalog, formula, items);
        return "Fire life passed: " + cases
            + " official cultivation, burn-count scaling, checkpoint replay and personal-turn cases.";
    }

    private static FightBattleStatusState CreateFireLifeStatus(FightOfficialCombatCatalog catalog,
        FightBattleUnitState actor, int rank, int level)
    {
        var status = catalog.CreateStatus(25098, actor.Id, actor.Id, 2509800 + rank * 100 + level,
            25098, 10597, rank, level, actor.SubRank, actor.Properties);
        status.StatusProperties = catalog.CalculateSkillProps(25098, rank, level, actor.SubRank)
            .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();
        Check(status.RoundEndRulesSupported && status.RoundEndEntityCount != null
            && status.RoundEndEntityCount.EntityClassId == 3051
            && status.RoundEndEntityCount.MaxCount == 1 && status.RoundEndSkills.Count == 1
            && status.RoundEndSkills[0].EntityId == 12040 && status.RoundEndSkills[0].Target == 2,
            "current 25098 listener, ground count or self-cure binding changed");
        return status;
    }

    private static FightBattleCheckpoint Checkpoint(FightBattleUnitState actor,
        FightBattleUnitState enemy, FightBattleStatusState status, int burningTiles)
    {
        var checkpoint = new FightBattleCheckpoint
        {
            StageId = 1, MaxRound = 8, RandomSeed = 73,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 }
        };
        checkpoint.Units.Add(actor);
        checkpoint.Units.Add(enemy);
        actor.Statuses.Add(status);
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(enemy.Id);
        checkpoint.Waves.Add(wave);
        for (int index = 0; index < burningTiles; index++)
        {
            var offset = status.RoundEndEntityCount.Range[index % status.RoundEndEntityCount.Range.Count];
            checkpoint.GridItems.Add(new FightBattleGridItemState
            {
                Id = 5000 + index, EntityClassId = 3051, GridItemClassId = 3320,
                OwnerId = enemy.Id, X = actor.X + offset.X, Y = actor.Y + offset.Y
            });
        }
        return checkpoint;
    }

    private static void CheckSelfTurn(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int burningTiles, long hp)
    {
        var actor = Unit(1, 1, hp, 1000, formula);
        var enemy = Unit(2, 2, 1000000, 10, formula);
        var status = CreateFireLifeStatus(catalog, actor, rank, level);
        var checkpoint = Checkpoint(actor, enemy, status, burningTiles);
        int counted = FightBattleEntityCountRuntime.Count(status.RoundEndEntityCount, actor, checkpoint.GridItems);

        var childProperties = catalog.CalculateSkillProps(12040, rank, level, actor.SubRank)
            .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();
        var child = catalog.CreateSkill(12040, 0, 10597, rank, level, actor.SubRank, childProperties);
        var sourceProperties = actor.Properties.ToDictionary(value => value.Id, value => value.Value);
        foreach (FightPropertyValue property in child.Properties)
        {
            sourceProperties.TryGetValue(property.Id, out long previous);
            sourceProperties[property.Id] = previous + property.Value;
        }
        FightConditionalApplyProperties.Merge(actor, actor, child, new FightDamageTypeData(), true,
            sourceProperties, checkpoint.Units, checkpoint.GridItems);
        var hit = child.HitGraphEntities.FirstOrDefault(value => value.EntityId == 12041);
        Check(hit != null, "official child skill 12040 did not bind hit graph 12041");
        long expected = formula.CalculateCure(sourceProperties, actor.Properties.ToDictionary(
            value => value.Id, value => value.Value), hit.DamageProperty, hit.FixedDamageProperty).Cure;

        var executor = FightBattleExecutor.Begin(checkpoint, formula,
            new FightDamageSettlement(), catalog, items);
        int sequence = checkpoint.Sequence;
        int counter = checkpoint.Counter + 1;
        var saved = FightBattleCheckpointSerializer.Save(checkpoint);
        var resumedCheckpoint = FightBattleCheckpointSerializer.Load(saved);
        var resumed = FightBattleExecutor.Restore(resumedCheckpoint, formula,
            new FightDamageSettlement(), catalog, items);
        var step = executor.Step(sequence, counter).Event;
        resumed.Step(sequence, counter);
        Check(FightBattleCheckpointSerializer.Save(checkpoint)
            == FightBattleCheckpointSerializer.Save(resumedCheckpoint),
            "checkpoint resume changed a cultivated round-end cure");

        var cures = step.DamageEvents.Where(value => value.SkillEntityId == 12040).ToArray();
        if (counted == 0)
        {
            Check(cures.Length == 0 && status.RoundEndTriggeredCount == 0,
                "the passive triggered without a live burning ground entity");
        }
        else
        {
            Check(expected > 0 && cures.Length == 1 && cures[0].IsCure
                && cures[0].SourceId == actor.Id && cures[0].TargetId == actor.Id
                && cures[0].SkillItemId == 10597 && cures[0].HitEntityId == 12041
                && cures[0].DamageEntityId == 12042 && cures[0].Damage == expected
                && cures[0].AfterHp - cures[0].BeforeHp == expected
                && status.RoundEndTriggeredCount == 1,
                "current cure amount, cultivation or attribution changed; expected=" + expected
                + ", actual=" + (cures.Length == 0 ? 0 : cures[0].Damage)
                + ", tiles=" + counted + ", rank=" + rank + ", level=" + level);
        }
        Check(executor.Step(sequence, counter).Replayed,
            "replaying a receipt repeated the passive cure");
    }

    private static void CheckNotAnotherUnitTurn(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var actor = Unit(1, 1, 100000, 10, formula);
        var enemy = Unit(2, 2, 1000000, 1000, formula);
        var status = CreateFireLifeStatus(catalog, actor, 4, 20);
        var checkpoint = Checkpoint(actor, enemy, status, 2);
        var executor = FightBattleExecutor.Begin(checkpoint, formula,
            new FightDamageSettlement(), catalog, items);
        int sequence = checkpoint.Sequence;
        int counter = checkpoint.Counter + 1;
        var step = executor.Step(sequence, counter).Event;
        Check(step.ActorId == enemy.Id && step.DamageEvents.All(value => value.SkillEntityId != 12040)
            && status.RoundEndTriggeredCount == 0,
            "the cure triggered at another unit's round end");
    }

    private static void CheckFullHpClipping(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var actor = Unit(1, 1, 1000000, 1000, formula);
        var enemy = Unit(2, 2, 1000000, 10, formula);
        var status = CreateFireLifeStatus(catalog, actor, 4, 20);
        var checkpoint = Checkpoint(actor, enemy, status, 2);
        var executor = FightBattleExecutor.Begin(checkpoint, formula,
            new FightDamageSettlement(), catalog, items);
        var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
        var cures = step.DamageEvents.Where(value => value.SkillEntityId == 12040).ToArray();
        Check(cures.Length == 1 && cures[0].IsCure && cures[0].BeforeHp == actor.MaxHp
            && cures[0].AfterHp == actor.MaxHp && cures[0].Damage == 0 && cures[0].DamageValue > 0
            && status.RoundEndTriggeredCount == 1,
            "full-HP healing no longer triggers and clips at maximum HP; cures=" + cures.Length
            + ", hp=" + actor.Hp + ", before=" + (cures.Length == 0 ? 0 : cures[0].BeforeHp)
            + ", after=" + (cures.Length == 0 ? 0 : cures[0].AfterHp)
            + ", applied=" + (cures.Length == 0 ? 0 : cures[0].DamageValue)
            + ", count=" + status.RoundEndTriggeredCount);
    }
}
