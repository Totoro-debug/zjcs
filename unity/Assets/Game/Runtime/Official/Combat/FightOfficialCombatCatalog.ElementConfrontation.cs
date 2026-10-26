using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        /// <summary>
        /// Reads the original incoming-element entries. The absent server combiner follows
        /// the confirmed offline rule: immunity takes priority; otherwise multiply damage
        /// factors. Keep the exact configured float for a single matching entry.
        /// </summary>
        public bool TryResolveElementConfrontation(FightBattleUnitState target, int elementType,
            out float value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            value = 0f;
            int count = 0;
            float multiplier = 1f;
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
                    float entryValue = Float(entry, "Value", 0f);
                    if (entryValue == 1f)
                    {
                        value = 1f;
                        return true;
                    }
                    value = entryValue;
                    multiplier *= 1f - entryValue;
                    count++;
                }
            }
            if (count > 1) value = 1f - multiplier;
            return true;
        }
    }
}
