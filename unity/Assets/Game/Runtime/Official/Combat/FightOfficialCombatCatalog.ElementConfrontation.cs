using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        /// <summary>
        /// Restores a single incoming-element entry from FightStatusDamageElementComponent.
        /// The server combiner is absent: multiple matching statuses remain unresolved instead
        /// of inventing an additive or multiplicative rule. Nonmatching entries do not conflict.
        /// </summary>
        public bool TryResolveElementConfrontation(FightBattleUnitState target, int elementType,
            out float value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            value = 0f;
            bool found = false;
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (status == null || !entities.TryGetValue(status.ClassId,
                    out Dictionary<string, object> definition)) continue;
                Dictionary<string, object> component = Component(definition,
                    "FightStatusDamageElementComponent");
                if (component == null) continue;
                foreach (object raw in Array(component["ElementInfos"], "element confrontation"))
                {
                    var entry = Object(raw, "element confrontation entry");
                    if (Int(entry, "ElementType") != elementType) continue;
                    if (found)
                    {
                        value = 0f;
                        return false;
                    }
                    value = Float(entry, "Value", 0f);
                    found = true;
                }
            }
            return true;
        }
    }
}
