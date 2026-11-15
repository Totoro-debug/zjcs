using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal static class FightPortableFlameRules
    {
        internal static void ReplaceDifferentSource(FightBattleUnitState owner,
            FightBattleStatusState incoming, FightBattleStatusOrigin origin)
        {
            if (incoming.ClassId != 24210) return;
            // Confirmed offline rule: each holder has one flame. A different
            // caster replaces the entity, restarting its lifetime and acquisition hit.
            foreach (var previous in new List<FightBattleStatusState>(owner.Statuses))
                if (previous != null && previous.ClassId == 24210
                    && !FightBattleStatusOrigin.Matches(FightBattleStatusOrigin.Resolve(previous), origin))
                    FightStatusLifecycle.End(owner, previous);
        }
    }
}
