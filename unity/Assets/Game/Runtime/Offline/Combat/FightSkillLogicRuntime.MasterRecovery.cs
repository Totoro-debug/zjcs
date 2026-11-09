using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        private sealed class MasterRecoveryChildSkill : IFightSkillChildProvider
        {
            public List<ChildSkillRequest> Collect(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, FightBattleUnitState target,
                FightBattleSkillCastPresentation presentation)
            {
                var result = new List<ChildSkillRequest>();
                if (context.skill.EntityId != 50230 || hit?.EntityId != 50231
                    || !EmptyFilter(context.hitFlow)) return result;
                var handles = Array(context.hitFlow, "HandleCfgs");
                if (handles.Count != 2 || !(handles[0] is Dictionary<string, object> read)
                    || !(handles[1] is Dictionary<string, object> trigger)
                    || String(read, "_type") != "FightHandleCfgGetMasterEntityAndStore"
                    || !IsPort(read, "EntityKey", 1, false)
                    || !IsPort(read, "StorageBBKey", 796681548, false)
                    || !IsChildTrigger(trigger, 50235, 796681548)) return result;
                // This one explicit graph was approved independently of the
                // automatic child trigger switch; the child keeps its own policy.
                if (context.actor.MasterId <= 0 || context.units == null) return result;
                foreach (var unit in context.units)
                    if (unit != null && !unit.Ended && unit.Id == context.actor.MasterId)
                    {
                        result.Add(Request(50235, unit, Math.Max(0f, hit.Delay)));
                        break;
                    }
                return result;
            }
        }
    }
}
