using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // MyFightSkillLogic has no server implementation in the APK. Only the
    // explicitly confirmed charge, transient stack and burning-grid flows are enabled here.
    internal sealed class FightSkillLogicRuntime
    {
        private readonly FightBattleUnitState actor;
        private readonly FightBattleSkillDefinition skill;
        private readonly IReadOnlyList<FightBattleUnitState> units;
        private readonly IReadOnlyList<FightBattleGridItemState> gridItems;
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightRandomStream random;
        private readonly Dictionary<string, object> flow;
        private readonly Dictionary<string, object> startFlow;
        private readonly Dictionary<string, object> hitFlow;
        private readonly Dictionary<int, long> persistentBlackboard = new Dictionary<int, long>();
        private bool startHandled;
        private readonly Dictionary<int, long> blackboard = new Dictionary<int, long>();

        private FightSkillLogicRuntime(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random, Dictionary<string, object> flow, Dictionary<string, object> startFlow,
            Dictionary<string, object> hitFlow,
            IReadOnlyList<FightBattleGridItemState> gridItems)
        {
            this.actor = actor; this.skill = skill; this.units = units;
            this.catalog = catalog; this.random = random; this.flow = flow; this.startFlow = startFlow;
            this.gridItems = gridItems;
            this.hitFlow = hitFlow;
        }

        internal static FightSkillLogicRuntime Create(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random,
            IReadOnlyList<FightBattleGridItemState> gridItems = null)
        {
            if (actor == null || skill == null || catalog == null
                || (skill.EntityId != 13730 && skill.EntityId != 13740 && skill.EntityId != 13820 && skill.EntityId != 13830 && skill.EntityId != 13840 && skill.EntityId != 50210)) return null;
            catalog.RestoreSkillLogicDefinition(skill);
            if (!skill.HasLogicComponent || string.IsNullOrEmpty(skill.LogicDefinitionJson)) return null;
            var component = OfficialJson.Parse(skill.LogicDefinitionJson) as Dictionary<string, object>;
            var flow = Object(component, "OnDoHitTransient");
            return flow == null && Object(component, "OnDoHit") == null ? null : new FightSkillLogicRuntime(actor, skill, units, catalog, random, flow,
                Object(component, "OnSkillStart"), Object(component, "OnDoHit"), gridItems);
        }

        internal void OnDoHitTransient(FightBattleUnitState target, FightBattleHitDefinition hit,
            FightDamageTypeData type)
        {
            if (skill.EntityId == 13730 || skill.EntityId == 13740 || skill.EntityId == 13820 || skill.EntityId == 50210 || target == null || type.Dodge || type.Blinding) return;
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

        // Validate all three original handlers before consuming any state. The
        // snapshot belongs to this cast and is independent of later start triggers.
        internal bool OnSkillStart()
        {
            if (skill.EntityId != 13820 || startHandled || startFlow == null
                || !EmptyFilter(startFlow)) return false;
            var handlers = Array(startFlow, "HandleCfgs");
            if (handlers.Count != 3) return false;
            var initialize = handlers[0] as Dictionary<string, object>;
            var read = handlers[1] as Dictionary<string, object>;
            var consume = handlers[2] as Dictionary<string, object>;
            const int chargeKey = 884090696;
            if (String(initialize, "_type") != "FightHandleCfgNewIntInBB"
                || !IsPort(initialize, "BBKey", chargeKey, true) || Int(initialize, "Value") != 0
                || String(read, "_type") != "FightHandleCfgGetStatusStackCount"
                || !IsPort(read, "SourceEntityKey", 1, false)
                || !IsPort(read, "StackCountKey", chargeKey, true) || Int(read, "StatusId") != 24031
                || String(consume, "_type") != "FightHandleCfgChangeStatusStack"
                || !IsPort(consume, "TargetEntityKey", 1, false)
                || !IsPort(consume, "ChangeValueKey", chargeKey, true)
                || Int(consume, "Multiplier") != -1 || Int(consume, "CreateStatusClassId") != 0
                || !Bool(consume, "InvokeConsumeEvent") || Bool(consume, "UseDefaultSourceInfoOnCreate")) return false;
            var filter = Object(consume, "FilterCondition");
            if (String(filter, "_type") != "IteratingAgentStatusClassIdFilter"
                || Int(filter, "StatusClassId") != 24031) return false;
            var snapshot = new Dictionary<FightBattleStatusState, int>();
            long count = 0;
            foreach (var status in actor.Statuses)
                if (status != null && status.ClassId == 24031
                    && status.ForbiddenType == 0 && status.StackCount > 0)
                {
                    count += status.StackCount;
                    if (count > int.MaxValue) return false;
                    snapshot[status] = status.StackCount;
                }
            persistentBlackboard[chargeKey] = count;
            startHandled = true;
            return FightStatusLogicRuntime.ChangeStatusStacks(consume, actor, null, actor, null,
                skill, null, units, catalog, random, count, null, snapshot);
        }

        internal void OnTransientDamage(FightBattleUnitState target, FightBattleHitDefinition hit,
            FightBattleTransientDamageContext damage)
        {
            if (target == null || damage == null || damage.IsCure || !EmptyFilter(flow)) return;
            var handlers = Array(flow, "HandleCfgs");
            Dictionary<string, object> scale;
            long count;
            int scaleKey, scaleValue;
            bool persistent;
            if (skill.EntityId == 13820)
            {
                if (!startHandled || handlers.Count != 1
                    || !(handlers[0] is Dictionary<string, object> chargedScale)
                    || !persistentBlackboard.TryGetValue(884090696, out count)) return;
                scale = chargedScale; scaleKey = 884090696; scaleValue = 700; persistent = true;
            }
            else if (skill.EntityId == 50210)
            {
                if (handlers.Count != 2 || !(handlers[0] is Dictionary<string, object> read)
                    || !(handlers[1] is Dictionary<string, object> stackedScale)
                    || String(read, "_type") != "FightHandleCfgGetStatusStackCount"
                    || !IsPort(read, "SourceEntityKey", 1, false)
                    || !IsPort(read, "StackCountKey", 719180550, false)
                    || Int(read, "StatusId") != 24823) return;
                scale = stackedScale; scaleKey = 719180550; scaleValue = 2000; persistent = false;
                count = 0;
                foreach (var status in actor.Statuses)
                    if (status != null && status.ClassId == 24823
                        && status.ForbiddenType == 0 && status.StackCount > 0)
                    {
                        count += status.StackCount;
                        if (count > int.MaxValue) return; // Original output is an integer blackboard port.
                    }
            }
            else if (skill.EntityId == 13730)
            {
                if (!TryCountBurningTiles(target, handlers, out count, out scale)) return;
                scaleKey = 511879177; scaleValue = 500; persistent = false;
            }
            else return;
            if (String(scale, "_type") != "FightHandleCfgDamageScaleByScale"
                || !IsPort(scale, "DamageKey", 2004, false)
                || !IsPort(scale, "StorageDamageKey", 2004, false)
                || !IsPort(scale, "BaseScaleKey", 774082833, false)
                || !IsPort(scale, "DeltaDamageKey", 122229964, false)
                || !IsPort(scale, "ScaleCountKey", scaleKey, persistent)
                || Int(scale, "ScaleValue") != scaleValue || !Bool(scale, "StorageAsAbs")) return;
            if (!persistent)
            {
                blackboard.Clear();
                blackboard[scaleKey] = count;
            }
            long previous = damage.Damage;
            decimal factor = 10000m + (decimal)scaleValue * count;
            damage.Damage = ScaleDamage(previous, factor);
            damage.AbsoluteDamage = ScaleDamage(damage.AbsoluteDamage, factor);
            blackboard[2004] = damage.Damage;
            decimal difference = Math.Abs((decimal)damage.Damage - previous);
            blackboard[122229964] = difference >= long.MaxValue ? long.MaxValue : (long)difference;
        }

        private bool TryCountBurningTiles(FightBattleUnitState target, List<object> handlers,
            out long count, out Dictionary<string, object> scale)
        {
            count = 0; scale = null;
            if (handlers.Count != 3 || !(handlers[0] is Dictionary<string, object> collect)
                || !(handlers[1] is Dictionary<string, object> read)
                || !(handlers[2] is Dictionary<string, object> multiply)
                || String(collect, "_type") != "FightHandleCfgCollectionEntitiesByRoleRoot"
                || Int(collect, "CharacterType") != 6 || !IsPort(collect, "SourceEntityKey", 1, false)
                || !IsPort(collect, "ResultEntityArrayKey", 406339594, false)
                || String(read, "_type") != "FightHandleCfgBlackboardReadEcIdArrayCountAndStorage"
                || !IsPort(read, "ListBBKey", 406339594, false)
                || !IsPort(read, "StorageBBKey", 511879177, false)) return false;
            var filter = Object(collect, "FilterCondition");
            var filters = Array(filter, "SubFilters");
            if (String(filter, "_type") != "LogicFilter" || Int(filter, "LogicType") != 0
                || filters.Count != 3 || !(filters[0] is Dictionary<string, object> entity)
                || !(filters[1] is Dictionary<string, object> faction)
                || !(filters[2] is Dictionary<string, object> range)
                || String(entity, "_type") != "EntityClassIdFilter" || Int(entity, "ClassId") != 3051
                || !IsPort(entity, "EntityKey", 1, false)
                || String(faction, "_type") != "SameFactionFilter"
                || !IsPort(faction, "CheckEntityKey", 3, false) || !IsPort(faction, "MainEntityKey", 1, false)
                || String(range, "_type") != "RangeFilter" || !IsPort(range, "CenterEntity", 2002, false)
                || !IsPort(range, "TargetEntityKey", 1, false)
                || Bool(range, "UseWorldPosByCenterEntityTransform") || !Bool(range, "ConsiderTargetBodySize")) return false;
            var offsets = new HashSet<FightGridPoint>();
            foreach (object value in Array(range, "Range"))
            {
                if (!(value is Dictionary<string, object> point) || !point.ContainsKey("X") || !point.ContainsKey("Y")
                    || !offsets.Add(new FightGridPoint(Int(point, "X"), Int(point, "Y")))) return false;
            }
            if (offsets.Count != 13 || !offsets.Contains(new FightGridPoint(0, 0))) return false;
            // Each live grid entity contributes once. Its stored faction is the
            // confirmed creation snapshot, independently of its creator's current faction.
            var seen = new HashSet<int>();
            if (gridItems != null) foreach (var item in gridItems)
                if (item != null && !item.Ended && item.EntityClassId == 3051
                    && item.HasRuntime && item.Runtime != null && !item.Runtime.Ended
                    && item.Runtime.Side == actor.Side
                    && offsets.Contains(new FightGridPoint(item.X - target.X, item.Y - target.Y))
                    && seen.Add(item.Id)) count++;
            scale = multiply;
            return true;
        }

        // The approved OnDoHit binding belongs to the hit entity, once before
        // iterating its victims. Snapshot positions so nested effects cannot change the collection.
        internal List<FightGridPoint> CollectBurningExplosions(FightBattleHitDefinition hit,
            FightBattleUnitState primary, FightBattleSkillCastPresentation cast)
        {
            var result = new List<FightGridPoint>();
            if (skill.EntityId != 13740 || !skill.CanTriggerChild || hit == null
                || hit.EntityId != 13741 || !EmptyFilter(hitFlow)) return result;
            var handlers = Array(hitFlow, "HandleCfgs");
            if (handlers.Count != 2 || !(handlers[0] is Dictionary<string, object> collect)
                || !(handlers[1] is Dictionary<string, object> each)
                || String(collect, "_type") != "FightHandleCfgCollectionEntitiesByRoleRoot"
                || Int(collect, "CharacterType") != 6 || !IsPort(collect, "SourceEntityKey", 1, false)
                || !IsPort(collect, "ResultEntityArrayKey", 406339594, false)
                || String(each, "_type") != "FightHandleCfgForeachEntities"
                || !IsPort(each, "EntityArrayKey", 406339594, false) || Int(each, "ForeachCount") != -1) return result;
            var nested = Array(each, "HandleCfgs");
            if (nested.Count != 1 || !(nested[0] is Dictionary<string, object> trigger)
                || String(trigger, "_type") != "FightHandleCfgTriggerSkillBySkill"
                || Int(trigger, "SkillEntityClassId") != 13745
                || !trigger.TryGetValue("Rate", out object rate) || Convert.ToDouble(rate) != 1.0
                || !IsPort(trigger, "SourceEntityKey", 3, false)
                || !IsPort(trigger, "TargetEntityKey", 1, false)) return result;
            var filter = Object(collect, "FilterCondition");
            var filters = Array(filter, "SubFilters");
            if (String(filter, "_type") != "LogicFilter" || Int(filter, "LogicType") != 0
                || filters.Count != 3 || !(filters[0] is Dictionary<string, object> faction)
                || !(filters[1] is Dictionary<string, object> entity)
                || !(filters[2] is Dictionary<string, object> range)
                || String(faction, "_type") != "SameFactionFilter"
                || !IsPort(faction, "CheckEntityKey", 3, false) || !IsPort(faction, "MainEntityKey", 1, false)
                || String(entity, "_type") != "EntityClassIdFilter" || Int(entity, "ClassId") != 3051
                || !IsPort(entity, "EntityKey", 1, false)
                || String(range, "_type") != "RangeFilter" || !IsPort(range, "CenterEntity", 2001, false)
                || !IsPort(range, "TargetEntityKey", 1, false)
                || !Bool(range, "UseWorldPosByCenterEntityTransform") || !Bool(range, "ConsiderTargetBodySize")) return result;
            var local = new List<FightGridPoint>();
            var unique = new HashSet<FightGridPoint>();
            foreach (object value in Array(range, "Range"))
            {
                if (!(value is Dictionary<string, object> point) || !point.ContainsKey("X") || !point.ContainsKey("Y")) return result;
                var offset = new FightGridPoint(Int(point, "X"), Int(point, "Y"));
                if (offset.X < -1 || offset.X > 1 || offset.Y < 1 || offset.Y > 4 || !unique.Add(offset)) return result;
                local.Add(offset);
            }
            if (unique.Count != 12) return result;
            var offsets = new HashSet<FightGridPoint>(FightBattleTargeting.TransformScope(local, hit.ScopeTransform, actor, cast));
            var center = FightHitAnchorExtensions.ResolveCenter(hit.Anchor, actor, primary, cast);
            var seen = new HashSet<int>();
            if (gridItems != null) foreach (var item in gridItems)
                if (item != null && !item.Ended && item.EntityClassId == 3051
                    && item.HasRuntime && item.Runtime != null && !item.Runtime.Ended
                    && item.Runtime.Side == actor.Side && seen.Add(item.Id)
                    && offsets.Contains(new FightGridPoint(item.X - center.X, item.Y - center.Y)))
                    result.Add(new FightGridPoint(item.X, item.Y));
            return result;
        }

        private static long ScaleDamage(long value, decimal factor)
        {
            if (value == 0 || factor == 10000m) return value;
            decimal magnitude = Math.Abs((decimal)value);
            if (factor > 0 && magnitude > decimal.MaxValue / factor)
                return value < 0 ? long.MinValue : long.MaxValue;
            decimal result = decimal.Truncate(value * factor / 10000m);
            return result >= long.MaxValue ? long.MaxValue
                : result <= long.MinValue ? long.MinValue : (long)result;
        }

        private static bool EmptyFilter(Dictionary<string, object> value)
            => value != null && (!value.TryGetValue("FilterCondition", out object raw) || raw == null);
        private static bool IsPort(Dictionary<string, object> value, string key, int id, bool persistent)
            => Port(value, key) == id && Persistent(value, key) == persistent;
        private static bool Bool(Dictionary<string, object> value, string key)
            => value != null && value.TryGetValue(key, out object raw) && Convert.ToBoolean(raw);

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
