using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFireCoreHarness
{
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int critical = 0;
        int ordinary = 0;
        for (int seed = 1; seed <= 64; seed++)
        {
            var fire = RunCast(catalog, formula, items, seed, 102, false);
            if (fire.Critical) critical++; else ordinary++;
            Check(fire.Burst == fire.Critical, "only successful critical fire hits trigger the burst");
            Check(!RunCast(catalog, formula, items, seed, 103, false).Burst,
                "a non-fire hit triggered the burst");
            Check(!RunCast(catalog, formula, items, seed, 102, true).Burst,
                "a forbidden secret triggered the burst");
        }
        Check(critical > 0 && ordinary > 0, "the test must cover ordinary and critical hits");
        return $"Fire core burst passed: {critical} fire casts with critical hits and {ordinary} without, non-fire and forbidden controls, original area, attribution and replay.";
    }

    private static (bool Critical, bool Burst) RunCast(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int seed, int element, bool forbidden)
    {
        const int rank = 4, level = 2, subRank = 8;
        var actor = Unit(1, 1, 5, 5, formula);
        var primary = Unit(2, 2, 5, 6, formula);
        var adjacent = Unit(3, 2, 6, 6, formula);
        var border = Unit(4, 2, 5, 8, formula);
        var outside = Unit(5, 2, 5, 9, formula);
        var friendly = Unit(6, 1, 4, 6, formula);
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        cp.Units.AddRange(new[] { actor, primary, adjacent, border, outside, friendly });
        var wave = new FightBattleWaveState();
        wave.UnitIds.AddRange(new[] { 2, 3, 4, 5 });
        cp.Waves.Add(wave);
        var fireball = catalog.CreateSkill(10800, 0, 10028, rank, level, subRank,
            catalog.CalculateSkillProps(10800, rank, level, subRank)
                .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        fireball.ElementType = element;
        fireball.ResetCooldownAtStart = true;
        actor.Skills.Add(fireball);
        actor.SkillState.Slots.Add(catalog.CreateSkillSlot(fireball, 0));
        var listener = catalog.CreateStatus(25001, 1, 1, 725001, 25001, 10503, rank, level, subRank);
        actor.Statuses.Add(listener);
        var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        if (forbidden) listener.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
        var step = executor.Step(cp.Sequence, cp.Counter + 1).Event;
        var hits = step.DamageEvents.Where(value => value.SkillEntityId == 10800 && value.Damage < 0).ToArray();
        Check(hits.Any(value => value.TargetId == primary.Id), "the original fireball missed its fixture target");
        var critical = hits.Where(value => value.Critical).ToArray();
        var targets = element == 102 && !forbidden
            ? critical.SelectMany(hit => cp.Units.Where(unit => unit.Side != actor.Side
                && Math.Abs(unit.X - hit.DamageX) + Math.Abs(unit.Y - hit.DamageY) <= 2)
                .Select(unit => unit.Id)).OrderBy(id => id).ToArray()
            : Array.Empty<int>();
        var casts = step.SkillCasts.Where(value => value.SkillEntityId == 10540).ToArray();
        var burst = step.DamageEvents.Where(value => value.SkillEntityId == 10540).ToArray();
        if (casts.Length == 0)
        {
            Check(burst.Length == 0, "the child dealt damage without a cast");
            return (critical.Length > 0, false);
        }

        Check(casts.Length == critical.Length && burst.Select(value => value.TargetId).OrderBy(id => id)
            .SequenceEqual(targets), "the two-grid burst targeted the wrong units");
        Check(burst.All(value => value.SourceId == actor.Id && value.SkillItemId == 10503
            && value.HitEntityId == 10541 && value.DamageEntityId == 10023 && value.ElementType == 102
            && value.DamageType == 1 && !value.IsCure && !value.TrueDamage && value.Damage < 0),
            "the child lost its source, original fire damage or cultivated attribution");
        string settled = FightBattleCheckpointSerializer.Save(cp);
        var restored = FightBattleCheckpointSerializer.Load(settled);
        var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(resumed.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == settled, "restoration repeated or changed the burst");
        return (critical.Length > 0, true);
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        int speed = id == 1 ? 1000 : 100;
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y,
            CharacterType = 1, Profession = 13, Level = 70, SubRank = 8,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = speed;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", speed }, { "BaseCritRatePercentValue", 10000 },
            { "BaseCritAvoidPercentValue", 10000 }, { "CritRatePercentValue", id == 1 ? 3000 : 0 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 },
            { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }

    private static void Check(bool condition, string details)
    {
        if (!condition) throw new InvalidOperationException("Fire core burst: " + details);
    }
}
