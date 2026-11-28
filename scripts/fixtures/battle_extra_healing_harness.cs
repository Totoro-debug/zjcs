using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleExtraHealingHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Extra healing: " + message); }

    static FightBattleUnitState Unit(FightDamageFormula formula, int id, int side, int x, int y)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, X = x, Y = y,
            CharacterType = side == 1 ? 1 : 2, Level = 150, SubRank = 8, MoveMask = 5,
            Hp = id == 2 ? 1000 : 500000000, MaxHp = 1000000000, LastMaxHp = 1000000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 1;
        foreach (var value in new[] { ("Attack", 10001), ("Defence", 0),
            ("MaxHp", 1000000000), ("Speed", id == 1 ? 1000 : 1),
            ("BaseCureAdd", 10000), ("BaseBeCureAdd", 10000) })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(value.Item1), Value = value.Item2 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int[] subRanks = { 5, 8, 11, 14, 17, 20, 23, 26 };
        long[,] fixedRows = { { 1656, 34015 }, { 1656, 34015 }, { 6304, 115568 },
            { 14313, 33561 }, { 1423568, 1467513 }, { 1220134, 1247416 },
            { 3070146, 3129185 }, { 6957019, 7066909 } };
        int[] ranks = { 1, 4, 26 }; long[] rankFixed = { 6000, 11000, 134000 };
        long[] rankHp = { 6000, 7000, 11500 }; int[] levels = { 1, 20 };
        int cases = 0;
        for (int s = 0; s < subRanks.Length; s++)
        for (int r = 0; r < ranks.Length; r++)
        for (int l = 0; l < levels.Length; l++)
        {
            long fixedCure = (long)((long)(fixedRows[s, l] * (rankFixed[r] / 10000.0)) * .0425);
            long hpCure = (long)((long)(10000 * (rankHp[r] / 10000.0)) * .0425);
            var properties = catalog.CalculateSkillProps(11430, ranks[r], levels[l], subRanks[s]);
            Check(properties.Count == 2 && properties[170] == fixedCure && properties[210] == hpCure,
                "the current official cultivated cure values changed");
            foreach (bool pvp in new[] { false, true })
            {
                long offeredHp = pvp ? (long)(hpCure * .5) : hpCure;
                long offeredFixed = pvp ? (long)(fixedCure * .5) : fixedCure;
                long expected = (long)(1000000000 * (offeredHp / 10000.0) + offeredFixed);
                Verify(catalog, formula, items, ranks[r], levels[l], subRanks[s], 2, expected, pvp);
                cases++;
            }
        }
        foreach (int action in new[] { 0, 6, 10 })
            Verify(catalog, formula, items, 4, 1, 8, action, -1);
        int cd = formula.PropertyId("CD");
        var normal = catalog.CalculateSkillProps(10720, 4, 20, 8);
        var versus = catalog.CalculateSkillPropsOnBattle(10720, 4, 20, 8, true);
        Check(normal.ContainsKey(cd) && versus[cd] == normal[cd]
            && versus[210] == (long)(normal[210] * .45), "PVP scaling changed the original cooldown exemption");
        return "Extra healing passed: " + cases + " PVE/PVP cultivated cross-shaped cures, separate property truncation, cooldown exemption, original scope delays, action filtering, child exclusion and checkpoint replay.";
    }

    static void Verify(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, int subRank, int action, long expected, bool pvp = false)
    {
        var caster = Unit(formula, 1, 1, 3, 5); caster.SubRank = subRank;
        var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 73, MaxRound = 10, IsPvp = pvp,
            Grid = new FightBattleGrid { Width = 20, Height = 20 } };
        state.Units.AddRange(new[] { caster, Unit(formula, 2, 1, 6, 5), Unit(formula, 3, 1, 5, 5),
            Unit(formula, 4, 1, 7, 5), Unit(formula, 5, 1, 6, 4), Unit(formula, 6, 2, 6, 6),
            Unit(formula, 7, 1, 7, 6) });
        catalog.AssembleUnit(caster, null, catalog.CreatePassiveStatusBindings(caster, items,
            new[] { new FightOfficialPassiveSkillBinding { ItemId = 10570, Rank = rank, Level = level, SubRank = subRank } }));
        var listener = caster.Statuses.Single(status => status.ClassId == 25039);
        Check(Math.Abs(listener.Components.HitSkill.Rate - .3f) < .000001f
            && listener.Components.HitSkill.Cure && !listener.Components.HitSkill.Damage,
            "the original 30% cure trigger changed");
        // Exercise the successful branch without depending on an unrelated target-selection roll.
        listener.Components.HitSkill.Rate = 1f;
        var props = catalog.CalculateSkillProps(12020, 2, 2, subRank).Select(pair =>
            new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();
        var active = catalog.CreateSkill(12020, 0, 10096, 2, 2, subRank, props);
        active.ActionType = action; active.LimitedTimes = 1; active.ResetCooldownAtStart = true;
        caster.Skills.Add(active); caster.SkillState.Slots.Add(catalog.CreateSkillSlot(active, 0));
        var battle = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var step = battle.Step(0, 1).Event;
        Check(step.SkillCasts.Any(cast => cast.SkillEntityId == 12020), "the real parent cure did not execute");
        var children = step.SkillCasts.Where(cast => cast.SkillEntityId == 11430).ToArray();
        var cures = step.DamageEvents.Where(hit => hit.SkillEntityId == 11430).ToArray();
        if (action == 2 || action == 6)
        {
            Check(children.Length == 1 && cures.Length == 4 && listener.Components.HitSkill.InvokeCount == 1,
                "the extra cure missed its cross, repeated itself or lost its single trigger");
            Check(cures.Select(hit => hit.TargetId).OrderBy(id => id).SequenceEqual(new[] { 2, 3, 4, 5 }),
                "the cure selected an enemy, caster outside the cross or diagonal ally");
            var child = children.Single();
            foreach (var cure in cures)
                Check(cure.SourceId == caster.Id && cure.SkillItemId == 10570 && cure.IsCure && cure.Damage > 0
                    && (expected < 0 || cure.DamageValue == expected) && cure.HitEntityId == 11431
                    && cure.DamageEntityId == 11432 && !cure.TrueDamage
                    && Math.Abs(cure.Delay - child.Delay - (cure.TargetId == 2 ? 0f : .75f)) < .0001f,
                    $"the original cure lost its source, value, private damage or center/ring timing: source={cure.SourceId}, item={cure.SkillItemId}, target={cure.TargetId}, value={cure.DamageValue}/{expected}, hit={cure.HitEntityId}, damage={cure.DamageEntityId}, delay={cure.Delay-child.Delay}, cure={cure.IsCure}, true={cure.TrueDamage}");
        }
        else Check(children.Length == 0 && cures.Length == 0 && listener.Components.HitSkill.InvokeCount == 0,
            "an ordinary/child action consumed the original skill-only cure trigger");
        string saved = FightBattleCheckpointSerializer.Save(state);
        var restored = FightBattleCheckpointSerializer.Load(saved);
        var resumed = FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(resumed.Step(restored.Sequence, restored.Counter).Replayed
            && FightBattleCheckpointSerializer.Save(restored) == saved, "restoring repeated the cure or counter");
    }
}
