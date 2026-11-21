using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSoulPiercerHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Soul piercer: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int rank in new[] { 6, 7 }) foreach (int level in new[] { 2, 20 })
        foreach (string mode in new[] { "normal", "shield", "overkill", "full", "combined", "cancelled" })
        foreach (bool friendly in new[] { false, true })
        {
            int dx = direction[0], dy = direction[1];
            var actor = Unit(1, 1, 10, 10, formula);
            actor.Direction = dx < 0 ? 1 : dx > 0 ? 3 : dy > 0 ? 2 : 0;
            if (mode == "full") actor.Hp = actor.MaxHp;
            actor.Turn.Hp = actor.Hp;
            long before = actor.Hp;
            int steal = mode == "combined" ? 1500 : mode == "cancelled" ? 0 : 1000;
            if (mode == "combined" || mode == "cancelled") actor.Properties.Add(new FightPropertyValue {
                Id = 213, Value = mode == "combined" ? 500 : -1000 });
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 6, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 22, Height = 22, DefaultGroundType = 2 } };
            state.Units.Add(actor);
            var wave = new FightBattleWaveState();
            int next = 2;
            for (int row = 1; row <= 3; row++) for (int column = -2; column <= 2; column++)
            {
                if (row == 2 && column == 1) continue;
                var unit = Unit(next++, friendly && row == 1 && column == -1 ? 1 : 2,
                    10 + row * dx + column * dy, 10 + row * dy - column * dx, formula);
                if (row == 2 && column == 0) unit.BodyOffsets.Add(new FightGridPoint(dy, -dx));
                if (mode == "overkill") unit.Hp = 1;
                if (mode == "shield") unit.Statuses.Add(catalog.CreateStatus(24435, unit.Id, unit.Id,
                    900000 + unit.Id, customShieldValue: 1000000));
                unit.Turn.Hp = unit.Hp;
                state.Units.Add(unit);
                if (unit.Side == 2) wave.UnitIds.Add(unit.Id);
            }
            state.Waves.Add(wave);
            var props = catalog.CalculateSkillProps(11920, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value);
            Check(props[213] == 1000, "the original skill's ten-percent lifesteal changed with cultivation");
            var skill = catalog.CreateSkill(11920, 0, 10089, rank, level, 8,
                props.Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent step = null;
            FightBattleSkillCastEvent cast = null;
            while (cast == null && state.Counter < 8)
            {
                step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10089 && value.SkillEntityId == 11920);
            }
            Check(cast != null, "the original attack was not released");
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 11920).ToArray();
            var cures = step.DamageEvents.Where(hit => hit.IsCure && hit.SkillEntityId == 10110).ToArray();
            var cells = state.Units.Where(unit => unit.Side == 2).SelectMany(unit => unit.BodyOffsets.Select(body => new {
                unit.Id, Row = (unit.X + body.X - 10) * dx + (unit.Y + body.Y - 10) * dy,
                Column = (unit.X + body.X - 10) * dy - (unit.Y + body.Y - 10) * dx
            })).Where(cell => cell.Row >= 1 && cell.Row <= 2 && Math.Abs(cell.Column) <= 1).ToArray();
            if (mode != "overkill") Check(hits.Length == cells.Length && hits.Length == (friendly ? 5 : 6),
                "the three-by-two area, body-cell occurrences or friend exclusion changed");
            Check(hits.Length > 0 && hits.All(hit => cells.Any(cell => cell.Id == hit.TargetId)
                && hit.SourceId == 1 && hit.HitEntityId == 11921 && hit.DamageEntityId == 10027
                && hit.ElementType == 104 && hit.DamageType == 1 && !hit.IsCure && !hit.TrueDamage
                && Math.Abs(hit.Delay - cast.Delay - .57f) < .0001f), "dark damage or authored hit timing changed");
            long expectedDamage = -(long)((1000 * formula.ReadProperty(props, 200)
                + formula.ReadProperty(props, 190)) * 1000 / 1010);
            if (mode != "shield" && mode != "overkill")
                Check(hits.All(hit => hit.Damage == expectedDamage), "the cultivated attack coefficient or fixed dark damage changed");
            var actual = hits.Where(hit => (hit.BeforeHp - hit.AfterHp) * steal / 10000 > 0).ToArray();
            Check(cures.Length == actual.Length, "shield absorption or overkill entered the lifesteal basis: " + mode
                + ", cures=" + cures.Length + ", expected=" + actual.Length + ", prop=" + skill.HitGraphEntities.Single(hit => hit.EntityId == 11921).HemophagiaProperty
                + ", hp=" + string.Join(",", hits.Select(hit => hit.BeforeHp - hit.AfterHp)));
            for (int i = 0; i < cures.Length; i++)
            {
                var cure = cures[i];
                Check(cure.SourceId == 1 && cure.TargetId == 1 && cure.SkillItemId == 10089
                    && cure.HitEntityId == 10111 && cure.DamageEntityId == 10028
                    && cure.DamageValue == (actual[i].BeforeHp - actual[i].AfterHp) * steal / 10000
                    && Math.Abs(cure.Delay - actual[i].Delay) < .0001f,
                    "the native lifesteal child lost its actual-HP basis, truncation, source or timing");
            }
            Check(actor.Hp - before == cures.Sum(cure => cure.AfterHp - cure.BeforeHp)
                && state.Statistics.Unit(1).HealDealt == actor.Hp - before, "lifesteal was duplicated or overheal entered statistics");
            if (mode == "full") Check(actor.Hp == before, "lifesteal exceeded maximum HP");
            if (mode == "shield") Check(hits.All(hit => hit.ShieldDamage > 0 && hit.BeforeHp == hit.AfterHp), "the shield case did not absorb the original attack");
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy), "checkpoint restoration changed damage or lifesteal");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved, "replay repeated healing");
            cases++;
        }
        return "Soul piercer passed: " + cases + " directions, area/body cells, cultivation, actual-HP lifesteal, shield, overkill, full health and restoration cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 8, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 500000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var property in new[] { new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
            new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
            new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed },
            new FightPropertyValue { Id = formula.PropertyId("BaseCureAdd"), Value = 10000 },
            new FightPropertyValue { Id = formula.PropertyId("BaseBeCureAdd"), Value = 10000 } }) unit.Properties.Add(property);
        return unit;
    }
}
