using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class KnockbackSettlementHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Knockback settlement: " + message); }

    public static void Run()
    {
        var formula = new FightDamageFormula(new FightDamagePropertySchema(), new FightDamageFormulaConfig());
        var settlement = new FightDamageSettlement();
        var executor = new FightBattleHitExecutor(formula, settlement);
        var actor = new FightBattleUnitState { Id = 1, Hp = 1000, MaxHp = 1000 };
        var target = new FightBattleUnitState { Id = 2, Hp = 1000, MaxHp = 1000 };
        var skill = new FightBattleSkillDefinition { EntityId = 10100, CanTriggerChild = true };
        var hit = new FightBattleHitDefinition { EntityId = 10101, DamageId = 10006, DamageType = 8, ActionType = 4 };
        var events = new List<string>();
        settlement.TargetDamaged += _ => events.Add("target");
        settlement.SourceHit += _ => events.Add("source");
        settlement.BattleDamaged += _ => events.Add("battle");
        var shield = new FightBattleStatusState { ShieldKind = FightBattleShieldKind.Persistent, ShieldValue = 30 };
        target.Statuses.Add(shield);
        var result = executor.ApplyKnockbackCollision(new FightRandomStream(1), 1, actor, target, skill, hit, 500, 1);
        Check(target.Hp == 930 && result.ShieldDamage == 30 && result.Settlement.RealDamageValue == -70
            && result.Formula.AbsoluteDamage == -100 && result.BrokenShields.Contains(shield),
            "shield absorption and the fixed pre-shield value must both be preserved");
        Check(string.Join(",", events) == "target,source,battle", "official damage event ordering changed");
        target.Hp = 1000;
        result = executor.ApplyKnockbackCollision(new FightRandomStream(1), 2, actor, target, skill, hit, 50, 1);
        Check(target.Hp == 970 && result.Settlement.RealDamageValue == -30, "source attack cap was not applied");
        target.Hp = 1;
        var prevention = new FightBattleStatusState { ClassId = 25013, HasHpLimit = true,
            HpLimitListenerType = 0, HpLimitMaxCount = 1, HpLimitLowerLimit = 1, OwnerId = 2 };
        target.Statuses.Add(prevention);
        result = executor.ApplyKnockbackCollision(new FightRandomStream(1), 3, actor, target, skill, hit, 500, 1);
        Check(target.Hp == 1 && result.FatalPrevention == prevention && !result.Settlement.KilledTarget,
            "fatal prevention must run during the normal HP settlement");
        hit.EntityId = 7;
        bool rejected = false;
        try { executor.ApplyKnockbackCollision(new FightRandomStream(1), 4, actor, target, skill, hit, 500, 1); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "other hits must not use the collision-specific damage path");
        hit.EntityId = 10101; skill.EntityId = 7; rejected = false;
        try { executor.ApplyKnockbackCollision(new FightRandomStream(1), 4, actor, target, skill, hit, 500, 1); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "another skill must not use the collision-specific damage path");
        Console.WriteLine("Knockback fixed settlement, shield, fatal prevention and event ordering passed.");
    }
}
