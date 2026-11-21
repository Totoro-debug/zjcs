using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSwordWindDanceHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Sword wind dance: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int rank in new[] { 4, 5, 7 })
        foreach (int level in new[] { 2, 20 })
        foreach (bool friendly in new[] { false, true })
        {
            int dx = direction[0], dy = direction[1];
            var actor = Unit(1, 1, 10, 10, formula);
            actor.Direction = dx < 0 ? 1 : dx > 0 ? 3 : dy > 0 ? 2 : 0;
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 6, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 22, Height = 22, DefaultGroundType = 2 } };
            state.Units.Add(actor);
            var wave = new FightBattleWaveState();
            int next = 2;
            for (int row = 1; row <= 5; row++) for (int column = -1; column <= 1; column++)
            {
                if (row == 3 && column == 0) continue;
                var unit = Unit(next++, friendly && row == 1 && column == 0 ? 1 : 2,
                    10 + row * dx + column * dy, 10 + row * dy - column * dx, formula);
                if (row == 2 && column == 0) unit.BodyOffsets.Add(new FightGridPoint(dx, dy));
                state.Units.Add(unit);
                if (unit.Side == 2) wave.UnitIds.Add(unit.Id);
            }
            state.Waves.Add(wave);
            var props = catalog.CalculateSkillProps(11910, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value);
            var skill = catalog.CreateSkill(11910, 0, 10088, rank, level, 8,
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
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == 10088);
            }
            Check(cast != null && actor.X == 10 && actor.Y == 10, "the original cast moved or was not released");
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 11910).ToArray();
            var cells = state.Units.Where(unit => unit.Side == 2).SelectMany(unit => unit.BodyOffsets.Select(body => new {
                unit.Id, Row = (unit.X + body.X - 10) * dx + (unit.Y + body.Y - 10) * dy,
                Column = (unit.X + body.X - 10) * dy - (unit.Y + body.Y - 10) * dx
            })).Where(cell => cell.Column == 0 && cell.Row >= 1 && cell.Row <= 4).ToArray();
            Check(hits.Length == cells.Length * 3 && hits.Length == (friendly ? 9 : 12),
                "the three attacks lost a front cell, included an ally or hit outside the one-by-four area");
            long damage = -(long)((1000 * formula.ReadProperty(props, 200) + formula.ReadProperty(props, 190)) * 1000 / 1010);
            foreach (var cell in cells)
            {
                var expected = new[] { .45f + new[] { 0f, .02f, .05f, .11f }[cell.Row - 1],
                    .61f + new[] { 0f, .02f, .05f, .11f }[cell.Row - 1],
                    1.13f + new[] { 0f, .02f, .04f, .07f }[cell.Row - 1] };
                foreach (float delay in expected)
                    Check(hits.Count(hit => hit.TargetId == cell.Id && Math.Abs(hit.Delay - cast.Delay - delay) < .0001f) == 1,
                        "the three authored strike times or successive row delays changed");
            }
            Check(hits.All(hit => hit.SourceId == 1 && hit.ElementType == 100 && hit.DamageType == 1 && !hit.IsCure
                && !hit.TrueDamage && hit.Damage == damage && (hit.HitEntityId == 11911 && hit.DamageEntityId == 10004
                    || hit.HitEntityId == 11913 && hit.DamageEntityId == 11912)), "wind damage, cultivation or authored damage identity changed");
            Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy), "restoration changed ordered hit results");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved, "replay repeated wind damage");
            cases++;
        }
        return "Sword wind dance passed: " + cases + " directions, cultivation, three strikes, row delays, body cells, ally exclusion and restoration cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 8, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var property in new[] { new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
            new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
            new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed } }) unit.Properties.Add(property);
        return unit;
    }
}
