using System;
using Zjcs.Combat.Official;

public static class BattleLiveHealthProjectionHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Live health projection: " + message); }
    public static void Run()
    {
        var checkpoint = new FightBattleCheckpoint();
        checkpoint.Units.Add(new FightBattleUnitState { Id = 1, Hp = 100, MaxHp = 100 });
        checkpoint.Units.Add(new FightBattleUnitState { Id = 2, Hp = 200, MaxHp = 200 });
        var view = new FightBattleLiveHealthProjection(checkpoint);
        var cast = new FightBattleSkillCastEvent { ActorId = 1, HasSkillCostHp = true,
            SkillCostBeforeHp = 100, SkillCostAfterHp = 90 };
        Check(view.TryApplyCost(cast) && view.Find(1).Hp == 90 && checkpoint.Units[0].Hp == 100,
            "actual cast cost must update only presentation health");
        Check(!view.TryApplyCost(cast), "the same cost must not apply twice");
        var hit = new FightBattleDamageEvent { DamageId = 12, TargetId = 2, HasDamageValue = true,
            BeforeHp = 200, AfterHp = 150, MaxHp = 200 };
        var future = new FightBattleDamageEvent { DamageId = 13, TargetId = 2, HasDamageValue = true,
            BeforeHp = 150, AfterHp = 120, MaxHp = 200 };
        Check(!view.TryApplyDamage(future) && view.Find(2).Hp == 200,
            "out-of-order damage cannot modify visible health");
        Check(view.TryApplyDamage(hit) && view.Find(2).Hp == 150 && checkpoint.Units[1].Hp == 200,
            "the actual impact must update presentation without settling the checkpoint");
        Check(!view.TryApplyDamage(hit) && view.TryApplyDamage(future) && view.Find(2).Hp == 120,
            "duplicate damage must be rejected while the following real hit can proceed");
        var shielded = new FightBattleDamageEvent { DamageId = 14, TargetId = 2, HasDamageValue = true,
            BeforeHp = 120, AfterHp = 120, MaxHp = 200 };
        Check(view.TryApplyDamage(shielded) && !view.TryApplyDamage(shielded),
            "zero HP change is still a single actual damage occurrence");
        var heal = new FightBattleDamageEvent { DamageId = 15, TargetId = 2, HasDamageValue = true,
            BeforeHp = 120, AfterHp = 180, MaxHp = 200, IsCure = true };
        Check(view.TryApplyDamage(heal) && view.Find(2).Hp == 180, "healing must use its actual receipt");
        Console.WriteLine("Live health projection ordering, cost, shield, healing and checkpoint isolation passed.");
    }
}
