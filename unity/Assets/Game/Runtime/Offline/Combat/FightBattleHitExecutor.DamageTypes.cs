namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleHitExecutor
    {
        // The common hit path has already calculated ordinary damage and run
        // its damage responses. Breaking is an additional physical/element layer;
        // fixed true damage and sharing transfers retain their separate path.
        private long ApplyDamageTypeEffects(FightBattleUnitState target,
            FightBattleSkillDefinition skill,FightBattleHitDefinition hit,long commonDamage)
        {
            if (!hit.TrueDamage || hit.DamageType == 0 || hit.DamageType == 8
                || skill?.DamageSharingTransfer == true) return commonDamage;
            long shield = hit.IgnoreShield ? 0 : FightBattleShieldRuntime.TotalShieldValue(target);
            return formula.CalculateBreakingDamage(commonDamage,target.CharacterType,shield);
        }
    }
}
