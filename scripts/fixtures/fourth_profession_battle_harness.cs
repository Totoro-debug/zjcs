using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

/// <summary>
/// Executes every fourth-promotion active skill through the shared offline
/// battle executor.  The catalog harness proves the graph is present; this
/// harness proves the graph can be selected, cast and settled by the runtime.
/// </summary>
public static class FourthProfessionBattleHarness
{
    private sealed class Case
    {
        public int Item, Entity, Profession;
        public Case(int item, int entity, int profession)
        { Item = item; Entity = entity; Profession = profession; }
    }

    private static readonly Case[] Cases = {
        new Case(10047,10880,4), new Case(10082,11980,4), new Case(10083,11990,4),
        new Case(10084,11890,4), new Case(10085,11900,4), new Case(10099,12080,4),
        new Case(10184,13580,4),
        new Case(10068,11520,8), new Case(10088,11910,8), new Case(10089,11920,8),
        new Case(10100,12090,8), new Case(10101,12100,8), new Case(10104,12130,8),
        new Case(10185,13590,8),
        new Case(10002,11450,13), new Case(10056,11080,13), new Case(10091,11960,13),
        new Case(10092,12010,13), new Case(10093,11970,13), new Case(10102,12110,13),
        new Case(10059,11380,17), new Case(10077,11760,17), new Case(10095,12140,17),
        new Case(10096,12020,17), new Case(10097,12200,17), new Case(10103,12120,17)
    };

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Fourth promotion battle: " + message); }

    private static FightBattleUnitState Unit(int id, int side, int profession,
        int x, int y, long hp, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = profession,
            Level = 50, SubRank = 8, X = x, Y = y, Hp = hp, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 8, MoveMask = 5
        };
        unit.Turn.Id = id; unit.Turn.Hp = hp; unit.Turn.Speed = id == 1 ? 10000 : 1;
        foreach (string name in new[] {
            "Attack", "Defense", "MaxHp", "Speed", "BaseCureAdd", "BaseBeCureAdd",
            "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge",
            "BaseDamageAdd", "BaseDamageReduce", "BasePhysicalMastery", "BaseWaterAffinity",
            "BaseFireAffinity", "BaseWindAffinity", "BaseLightAffinity", "BaseDarkAffinity" })
        {
            int property = formula.PropertyId(name);
            if (property > 0 && unit.Properties.All(value => value.Id != property))
                unit.Properties.Add(new FightPropertyValue { Id = property, Value = name == "MaxHp" ? 1000000 : 10000 });
        }
        return unit;
    }

    private static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog catalog,
        Case test, int slot, int level, int subRank)
    {
        IReadOnlyDictionary<int, long> values = catalog.CalculateSkillProps(test.Entity, 4, level, subRank);
        return catalog.CreateSkill(test.Entity, slot, test.Item, 4, level, subRank,
            values.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int casts = 0, damageEvents = 0, movementEvents = 0;
        foreach (Case test in Cases)
        {
            var checkpoint = new FightBattleCheckpoint {
                StageId = 4004, MaxRound = 12, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 }
            };
            FightBattleUnitState actor = Unit(1, 1, test.Profession, 5, 5, 500000, formula);
            FightBattleUnitState ally = Unit(2, 1, test.Profession, 6, 5, 1000, formula);
            FightBattleUnitState enemy = Unit(3, 2, 0, 5, 6, 1000000, formula);
            FightBattleUnitState distantEnemy = Unit(4, 2, 0, 5, 8, 1000000, formula);
            FightBattleSkillDefinition skill = Skill(catalog, test, 0, 30, 8);
            if (test.Item == 10096)
            {
                List<FightBattleUnitState> probeTargets = FightBattleTargeting.Select(actor,
                    new[] { actor, ally, enemy, distantEnemy }, skill.TargetType, skill: skill);
                Check(probeTargets.Count > 0, "healing target probe produced no candidate");
            }
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.Units.Add(actor); checkpoint.Units.Add(ally); checkpoint.Units.Add(enemy);
            checkpoint.Units.Add(distantEnemy);
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(enemy.Id); wave.UnitIds.Add(distantEnemy.Id); checkpoint.Waves.Add(wave);

            FightBattleExecutor executor = FightBattleExecutor.Begin(checkpoint, formula,
                new FightDamageSettlement(), catalog, items);
            FightBattleExecutionResult selected = null;
            var observed = new List<string>();
            for (int counter = 1; counter <= 8 && selected == null; counter++)
            {
                FightBattleExecutionResult result = executor.Step(0, counter);
                if (result?.Event?.SkillCasts != null)
                    observed.AddRange(result.Event.SkillCasts.Select(cast =>
                        cast.ActorId + ":" + cast.SkillEntityId + "/" + cast.SkillItemId));
                if (result?.Event?.SkillCasts != null && result.Event.SkillCasts.Any(cast =>
                    cast.ActorId == actor.Id && (cast.SkillEntityId == test.Entity || cast.SkillItemId == test.Item)))
                    selected = result;
            }
            Check(selected != null, "skill was not cast: item " + test.Item + ", entity " + test.Entity
                + "; observed [" + string.Join(",", observed) + "]");
            casts += selected.Event.SkillCasts.Count(cast => cast.ActorId == actor.Id
                && (cast.SkillEntityId == test.Entity || cast.SkillItemId == test.Item));
            damageEvents += selected.Event.DamageEvents.Count;
            movementEvents += selected.Event.Movements.Count;
        }
        Check(casts == Cases.Length, "active cast count changed: " + casts + "/" + Cases.Length);
        return "Fourth-promotion battle execution passed: " + casts
            + " active casts, " + damageEvents + " damage/status events, "
            + movementEvents + " movement events.";
    }
}
