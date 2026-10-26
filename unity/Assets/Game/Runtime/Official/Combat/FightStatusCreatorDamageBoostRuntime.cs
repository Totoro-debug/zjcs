using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Confirmed offline wiring for 24062's BeforeShield damage component. Its description restricts
    /// the bonus to the status creator; the user confirmed percentage first, then fixed damage.
    /// Other damage-process component branches remain unsupported.
    /// </summary>
    public static class FightStatusCreatorDamageBoostRuntime
    {
        public static long BeforeShield(FightBattleUnitState source, FightBattleUnitState target,
            long damage, FightDamageFormula formula)
        {
            if (source == null || target == null || target.Statuses == null || damage > 0) return damage;
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (status == null || !status.HasCreatorDamageBoost || status.CreatorId != source.Id
                    || status.ResolvedProperties == null) continue;
                var properties = new Dictionary<int, long>();
                foreach (FightPropertyValue value in status.ResolvedProperties)
                    if (value != null) properties[value.Id] = value.Value;
                double percent = formula.ReadProperty(properties, formula.PropertyId("StatusDmgAddPer"));
                double fixedDamage = formula.ReadProperty(properties, formula.PropertyId("FixedStatusDmgAdd"));
                double amount = -(double)damage * (1.0 + percent) + fixedDamage;
                damage = amount <= 0.0 ? 0L : amount >= long.MaxValue ? -long.MaxValue : -(long)amount;
            }
            return damage;
        }
    }
}
