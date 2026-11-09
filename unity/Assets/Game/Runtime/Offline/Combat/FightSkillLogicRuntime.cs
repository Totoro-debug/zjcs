using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // MyFightSkillLogic has no server implementation in the APK. Only the
    // explicitly confirmed 13830/13840 transient stack flows are enabled here.
    internal sealed class FightSkillLogicRuntime
    {
        private readonly FightBattleUnitState actor;
        private readonly FightBattleSkillDefinition skill;
        private readonly IReadOnlyList<FightBattleUnitState> units;
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightRandomStream random;
        private readonly Dictionary<string, object> flow;
        private readonly Dictionary<int, long> blackboard = new Dictionary<int, long>();

        private FightSkillLogicRuntime(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random, Dictionary<string, object> flow)
        {
            this.actor = actor; this.skill = skill; this.units = units;
            this.catalog = catalog; this.random = random; this.flow = flow;
        }

        internal static FightSkillLogicRuntime Create(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (actor == null || skill == null || catalog == null
                || (skill.EntityId != 13830 && skill.EntityId != 13840)) return null;
            catalog.RestoreSkillLogicDefinition(skill);
            if (!skill.HasLogicComponent || string.IsNullOrEmpty(skill.LogicDefinitionJson)) return null;
            var component = OfficialJson.Parse(skill.LogicDefinitionJson) as Dictionary<string, object>;
            var flow = Object(component, "OnDoHitTransient");
            return flow == null ? null : new FightSkillLogicRuntime(actor, skill, units, catalog, random, flow);
        }

        internal void OnDoHitTransient(FightBattleUnitState target, FightBattleHitDefinition hit,
            FightDamageTypeData type)
        {
            if (target == null || type.Dodge || type.Blinding) return;
            var filter = Object(flow, "FilterCondition");
            if (flow.TryGetValue("FilterCondition", out object rawFilter)
                && rawFilter != null && filter == null) return;
            if (filter != null)
            {
                if (String(filter, "_type") != "EntityHasAnyStatusByEntityKeyFilter"
                    || Port(filter, "EntityKey") != 2002) return;
                bool found = false;
                foreach (object id in Array(filter, "StatusClassIds"))
                    foreach (var status in target.Statuses)
                        if (status != null && status.ClassId == Convert.ToInt32(id)
                            && status.ForbiddenType == 0 && status.StackCount > 0) found = true;
                if (!found) return;
            }
            foreach (object value in Array(flow, "HandleCfgs"))
            {
                if (!(value is Dictionary<string, object> handle)) return;
                string kind = String(handle, "_type");
                if (kind == "FightHandleCfgNewIntInBB")
                {
                    int key = Port(handle, "BBKey");
                    if (key == 0 || Persistent(handle, "BBKey")) return;
                    blackboard[key] = Int(handle, "Value");
                }
                else if (kind == "FightHandleCfgChangeStatusStack")
                {
                    var statusFilter = Object(handle, "FilterCondition");
                    // Other graph entity ports and filters need their own bindings.
                    if (Port(handle, "TargetEntityKey") != 1 || statusFilter == null
                        || String(statusFilter, "_type") != "IteratingAgentStatusClassIdFilter"
                        || !blackboard.TryGetValue(Port(handle, "ChangeValueKey"), out long delta)) return;
                    int statusId = Int(statusFilter, "StatusClassId");
                    if (statusId <= 0) return;
                    var template = CreateStatus(statusId, 0);
                    if (!FightSkillTriggerPolicy.AllowsStatus(skill, template)) return;
                    if (!FightStatusLogicRuntime.ChangeStatusStacks(handle, actor, null, actor, target,
                        skill, hit, units, catalog, random, delta, CreateStatus)) return;
                }
                else return;
            }
        }

        private FightBattleStatusState CreateStatus(int statusId, int instanceId)
        {
            var status = catalog.CreateStatus(statusId, actor.Id, actor.Id, instanceId,
                skill.EntityId, skill.ItemId, skill.Rank, skill.Level, skill.SubRank,
                actor.Properties, 0, null, skill.BlessPropertyScales);
            status.Origin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy();
            return status;
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> value, string key)
            => value != null && value.TryGetValue(key, out object raw) ? raw as Dictionary<string, object> : null;
        private static List<object> Array(Dictionary<string, object> value, string key)
            => value != null && value.TryGetValue(key, out object raw) && raw is List<object> array
                ? array : new List<object>();
        private static string String(Dictionary<string, object> value, string key)
            => value != null && value.TryGetValue(key, out object raw) ? Convert.ToString(raw) : null;
        private static int Int(Dictionary<string, object> value, string key)
            => value != null && value.TryGetValue(key, out object raw) ? Convert.ToInt32(raw) : 0;
        private static int Port(Dictionary<string, object> value, string key) => Int(Object(value, key), "Id");
        private static bool Persistent(Dictionary<string, object> value, string key)
        {
            var port = Object(value, key);
            return port != null && port.TryGetValue("IsPersistentBlackboard", out object raw)
                && Convert.ToBoolean(raw);
        }
    }
}
