using System;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleHitExecutor
    {
        // Collision damage has a user-confirmed fixed value. Keep the normal shield,
        // fatal-prevention and damage-event settlement without applying stat modifiers.
        internal FightBattleHitResult ApplyKnockbackCollision(FightRandomStream random, int damageId,
            FightBattleUnitState actor, FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, long sourceAttack, int stageType,
            Action<FightBattleTransientDamageContext> beforeShield = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (actor.Hp <= 0 || target.Hp <= 0)
                throw new InvalidOperationException("Fight hit source and target must be alive.");
            if (skill.EntityId != 10100 || hit.EntityId != 10101 || hit.DamageId != 10006 || hit.DamageType != 8)
                throw new ArgumentException("The original blocked-skill hit is required.", nameof(hit));

            long damage = -FightKnockbackCollision.Damage(target.MaxHp, sourceAttack);
            var source = actor.EntityState();
            var destination = target.EntityState();
            var result = new FightBattleHitResult { Type = new FightDamageTypeData() };
            var context = new FightBattleTransientDamageContext(result.Type, damage, damage, false);
            beforeShield?.Invoke(context);
            damage = context.Damage;
            long remaining = FightBattleShieldRuntime.AbsorbDamage(random, target, hit, damage,
                out result.ShieldDamage, result.TriggeredStatusSkills, skill,
                result.TriggeredStatusSkillInvocations, result.BrokenShields);
            result.Formula = new FightDamageFormulaResult { Damage = remaining, AbsoluteDamage = context.AbsoluteDamage };
            result.Settlement = SettleDamage(damageId, source, destination, target, skill,
                remaining, stageType, result);
            target.Hp = destination.Hp;
            return result;
        }
    }
}
