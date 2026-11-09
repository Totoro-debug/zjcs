using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        private sealed class TargetHpScale : IFightSkillDamageScale
        {
            public bool TryRead(FightSkillLogicRuntime cast, FightBattleUnitState target,
                out DamageScaleSelection selection)
            {
                selection = null;
                var filter = Object(cast.flow, "FilterCondition");
                if (String(filter, "_type") != "EntityHpPercentByEntityKeyFilter"
                    || !IsPort(filter, "EntityKey", 2002, false)
                    || Int(filter, "Relation") != 4 || target.MaxHp <= 0
                    || !filter.TryGetValue("HpPercent", out object rawThreshold)) return false;
                double threshold = Convert.ToDouble(rawThreshold);
                // Preserve the serialized official float threshold, including its
                // original LT relation. Re-read HP for every body-cell occurrence.
                if (double.IsNaN(threshold) || threshold < 0d || threshold > 1d
                    || (double)target.Hp / target.MaxHp >= threshold) return false;
                return TryReadConditionalScale(cast, out selection);
            }
        }

        private sealed class OwnerStatusScale : IFightSkillDamageScale
        {
            public bool TryRead(FightSkillLogicRuntime cast, FightBattleUnitState target,
                out DamageScaleSelection selection)
            {
                selection = null;
                var filter = Object(cast.flow, "FilterCondition");
                var ids = Array(filter, "StatusClassIds");
                if (String(filter, "_type") != "EntityHasAnyStatusByEntityKeyFilter"
                    || !IsPort(filter, "EntityKey", 1, false) || ids.Count != 1
                    || Convert.ToInt32(ids[0]) != 24721) return false;
                foreach (var status in cast.actor.Statuses)
                    if (status != null && status.ClassId == 24721
                        && status.ForbiddenType == 0 && status.StackCount > 0)
                        return TryReadConditionalScale(cast, out selection);
                return false;
            }
        }

        private static bool TryReadConditionalScale(FightSkillLogicRuntime cast,
            out DamageScaleSelection selection)
        {
            selection = null;
            var handlers = Array(cast.flow, "HandleCfgs");
            if (handlers.Count != 2 || !(handlers[0] is Dictionary<string, object> read)
                || !(handlers[1] is Dictionary<string, object> scale)
                || String(read, "_type") != "FightHandleCfgNewIntInBB"
                || !IsPort(read, "BBKey", 563722184, false) || Int(read, "Value") != 1) return false;
            selection = new DamageScaleSelection { Handle = scale, Count = 1,
                Key = 563722184, Value = 5000 };
            return true;
        }
    }
}
