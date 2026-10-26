namespace Zjcs.Combat.Official
{
    /// <summary>Official CharacterDamageUIManager.CanShowDamage, without Unity entity lookup.</summary>
    public static class CharacterDamageUIManager
    {
        public static bool CanShowDamage(FightBattleDamageEvent damage, bool hideDamage,
            bool showZeroDamage, CustomDamageUITypeFilter filter)
        {
            if (damage == null || hideDamage) return false;
            if (damage.IsElementAffect) return true;
            if (damage.ShieldAddFinalDamageValue != 0L) return true;
            if (filter != null && filter.IsFilter(damage)) return false;
            return showZeroDamage;
        }
    }
}
