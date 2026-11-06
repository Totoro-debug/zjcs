using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // Original FightUtilsHandler.CanAccDamageToBoss filters by target CharacterType.Monster
    // and DamageResult.IsDamage. FightActivityDomainBossMainPanel adds |RealDamageValue| + ShieldValue.
    // Receipts preserve that hit history across interruption; unit totals cannot distinguish targets.
    internal static class FightBattleBossDamage
    {
        internal static long Read(FightBattleCheckpoint checkpoint)
        {
            var types = new Dictionary<int, int>();
            foreach (var unit in checkpoint.Units)
                if (unit != null) types.Add(unit.Id, unit.CharacterType);
            long total = 0;
            foreach (var receipt in checkpoint.Receipts)
            {
                if (receipt?.Event?.DamageEvents == null) continue;
                foreach (var hit in receipt.Event.DamageEvents)
                {
                    if (hit == null || hit.IsSkillCost || hit.IsCure) continue;
                    // The original handler returns false when the target entity/component is absent.
                    if (types.TryGetValue(hit.TargetId, out int type) && type == 2)
                        total = checked(total + Math.Abs(hit.Damage) + hit.ShieldDamage);
                }
            }
            return total;
        }
    }
}
