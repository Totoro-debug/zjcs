using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWindShieldHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Wind shield: " + message);
    }

    private static FightBattleUnitState Unit(int id, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
            Level = 50, SubRank = 1, X = 4 + id, Y = 4, Hp = 1000, MaxHp = 1000, LastMaxHp = 1000,
            Turn = new FightTurnUnit { Id = id, Hp = 1000, Speed = speed } };
        foreach (var entry in new Dictionary<int, long> { { 1, 100 }, { 2, 100 }, { 3, 1000 }, { 4, speed } })
            unit.Properties.Add(new FightPropertyValue { Id = entry.Key, Value = entry.Value });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("BaseEffectRate"), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("BaseEffectDodge"), Value = 10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Check(items.Required(10513).TryAtStartType == 1 && items.Required(10571).TryAtStartType == 1
            && items.Required(10128).TryAtStartType == 1 && items.Required(10561).TryAtStartType == 0,
            "Official skill-item startup flags differ.");
        var owner = Unit(1, 10, formula);
        var enemy = Unit(2, 1000, formula);
        enemy.Hp = enemy.MaxHp = enemy.LastMaxHp = 1000000;
        enemy.Properties.Single(property => property.Id == 3).Value = enemy.MaxHp;
        enemy.Turn.Hp = enemy.Hp;
        var left = Unit(3, 5, formula); left.Side = enemy.Side; left.X = owner.X - 1; left.Y = owner.Y;
        var diagonal = Unit(4, 5, formula); diagonal.Side = enemy.Side; diagonal.X = owner.X + 1; diagonal.Y = owner.Y + 1;
        var distant = Unit(5, 5, formula); distant.Side = enemy.Side; distant.X = owner.X + 3; distant.Y = owner.Y;
        foreach (var unit in new[] { left, diagonal, distant })
        {
            unit.Hp = unit.MaxHp = unit.LastMaxHp = 1000000; unit.Turn.Hp = unit.Hp;
            unit.Properties.Single(property => property.Id == 3).Value = unit.MaxHp;
        }
        var properties = owner.Properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList();
        var passive = catalog.CreateStatus(25006, 1, 1, 1, 25006, 10513, 2, 20, 1, properties);
        owner.Statuses.Add(passive);
        var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
            Grid = new FightBattleGrid { Width = 12, Height = 12 } };
        var attack = new FightBattleSkillDefinition { EntityId = 990801, Slot = 0, TargetType = 1, ActionType = 2 };
        attack.Range.Add(new FightGridPoint(-1, 0));
        attack.Hits.Add(new FightBattleHitDefinition { EntityId = 990802, ActionType = 4,
            DamageType = 8, HasCustomDamage = true, CustomDamage = 100 });
        enemy.Skills.Add(attack);
        enemy.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
        cp.Units.AddRange(new[] { owner, enemy, left, diagonal, distant });
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4, 5 }); cp.Waves.Add(wave);
        var runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
        var shield = owner.Statuses.SingleOrDefault(status => status.ClassId == 24175);
        if (shield == null)
        {
            var startup = runner.Step(0, 1).Event;
            Check(!startup.DamageEvents.Any(value => value.SkillEntityId == 990801),
                "The faster enemy acted before the pre-battle shield was applied.");
            shield = owner.Statuses.SingleOrDefault(status => status.ClassId == 24175);
        }
        Check(shield != null && shield.ShieldInitialized && shield.ShieldRemainValue > 0
            && shield.ApplyingSkillItemId == 10513 && shield.CreatorId == 1,
            "The initial shield lost its value or passive source.");
        attack.Hits[0].CustomDamage = shield.ShieldRemainValue;
        var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var restored = FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
        int next = cp.Counter + 1;
        var attackStep = runner.Step(0, next).Event; restored.Step(0, next);
        var damage = attackStep.DamageEvents.FirstOrDefault(value => value.SkillEntityId == 990801);
        Check(damage != null && damage.ShieldDamage > 0,
            "The faster enemy's opening hit was not absorbed by the described pre-battle shield.");
        Check(damage.BeforeHp == damage.AfterHp && !owner.Statuses.Any(status => status.ClassId == 24175),
            "Exact shield absorption did not remove the depleted shield.");
        Check(attackStep.SkillCasts.Any(cast => cast.SkillEntityId == 10870 && cast.ActorId == 1
            && cast.SkillItemId == 10513), "Shield destruction lost its original child cast or source.");
        Check(attackStep.DamageEvents.Any(hit => hit.SkillEntityId == 10870 && hit.SourceId == 1
            && hit.TargetId == 2 && hit.SkillItemId == 10513), "Shield destruction did not damage the adjacent enemy.");
        var knockback = attackStep.Movements.SingleOrDefault(move => move.UnitId == 2 && move.MoveBehaviourId == 10873);
        Check(knockback != null && knockback.Path.Count == 2 && knockback.Path[0].X == owner.X + 1
            && knockback.Path[1].X == owner.X + 2 && knockback.Path[1].Y == owner.Y,
            "Shield destruction did not push the adjacent enemy one cell away from the shield owner.");
        Check(new[] { enemy, left, diagonal }.All(unit => attackStep.DamageEvents.Any(hit =>
            hit.SkillEntityId == 10870 && hit.TargetId == unit.Id))
            && new[] { enemy, left, diagonal }.All(unit => attackStep.Movements.Any(move =>
                move.UnitId == unit.Id && move.MoveBehaviourId == 10873 && move.Path.Count == 2))
            && !attackStep.DamageEvents.Any(hit => hit.SkillEntityId == 10870 && hit.TargetId == distant.Id),
            "Shield destruction did not apply its square scope and outward displacement to all adjacent enemies.");
        Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(saved),
            "Shield startup or damage changed after checkpoint restore.");
        foreach (var probe in new[] { (10571, 25041, 11190), (10128, 25160, 12740) })
        {
            owner = Unit(1, 10, formula); enemy = Unit(2, 1000, formula);
            var ally = Unit(3, 20, formula); ally.Side = 1; ally.X = owner.X; ally.Y = owner.Y + 1;
            owner.Statuses.Add(catalog.CreateStatus(probe.Item2, 1, 1, 1, probe.Item2,
                probe.Item1, 2, 20, 1, owner.Properties));
            cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 5, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 12, Height = 12 } };
            cp.Units.AddRange(new[] { owner, enemy, ally });
            wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            runner = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var startup = runner.Step(0, 1).Event;
            Check(startup.SkillCasts.Any(cast => cast.SkillEntityId == probe.Item3 && cast.ActorId == 1
                && cast.SkillItemId == probe.Item1), "Auto-self defensive skill was not dispatched before a turn: " + probe.Item1);
            Check(owner.Statuses.Any(status => status.ClassId == 24200)
                && ally.Statuses.Any(status => status.ClassId == 24200),
                "Auto-self defensive skill did not protect its nearby ally: " + probe.Item1);
        }
        return "Wind shield passed: startup, shield depletion, child damage and knockback, allied protection and checkpoint.";
    }
}
