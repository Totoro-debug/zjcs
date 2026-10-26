using System;

namespace Zjcs.Combat.Official
{
    // Official CustomDamageUITypeFilter.IsFilter, adapted from MyFightDamage to the
    // persisted local damage result. Flags are read from the original role prefab.
    [Serializable]
    public sealed class CustomDamageUITypeFilter
    {
        public bool IsCrit;
        public bool IsBlock;
        public bool IsDodge;
        public bool IsBlinding;
        public bool IsImmunity;
        public bool IsBreakResilience;
        public bool IsTrueDamage;

        public bool IsFilter(FightBattleDamageEvent damage)
        {
            if ((!IsCrit || !damage.Critical) && (!IsBlock || !damage.Block)
                && (!IsDodge || !damage.Dodge) && (!IsBlinding || !damage.Blinding)
                && (!IsImmunity || !damage.Immunity) && (!IsBreakResilience || !damage.BreakResilience))
            {
                if (IsTrueDamage) return damage.TrueDamage;
                return false;
            }
            return true;
        }
    }
}
