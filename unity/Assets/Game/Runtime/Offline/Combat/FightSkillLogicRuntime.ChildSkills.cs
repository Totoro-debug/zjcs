using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        // Producers interpret their own graph and bindings. Execution, cultivation
        // inheritance and event scheduling belong to the executor, not the graph.
        private interface IFightSkillChildProvider
        {
            List<ChildSkillRequest> Collect(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, FightBattleUnitState target,
                FightBattleSkillCastPresentation presentation);
        }

        internal sealed class ChildSkillRequest
        {
            internal int SkillId;
            internal FightBattleUnitState Target;
            internal FightGridPoint Position;
            internal float Delay;
        }

        private readonly IFightSkillChildProvider childSkillSource;

        private static IFightSkillChildProvider CreateChildSkillProvider(int skillId)
        {
            switch (skillId)
            {
                case 13740: return new BurningTileChildSkills();
                case 54370: return new MasterChildSkill();
                case 54373: return new TargetChildSkill();
                default: return null;
            }
        }

        internal List<ChildSkillRequest> CollectChildSkills(FightBattleHitDefinition hit,
            FightBattleUnitState target, FightBattleSkillCastPresentation presentation)
        {
            if (!skill.CanTriggerChild || childSkillSource == null)
                return new List<ChildSkillRequest>();
            return childSkillSource.Collect(this, hit, target, presentation);
        }

        private sealed class BurningTileChildSkills : IFightSkillChildProvider
        {
            public List<ChildSkillRequest> Collect(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, FightBattleUnitState target,
                FightBattleSkillCastPresentation presentation)
            {
                var result = new List<ChildSkillRequest>();
                foreach (var position in context.CollectBurningExplosions(hit, target, presentation))
                    result.Add(new ChildSkillRequest { SkillId = 13745, Position = position,
                        Delay = Math.Max(0f, hit.Delay) });
                return result;
            }
        }

        private sealed class MasterChildSkill : IFightSkillChildProvider
        {
            public List<ChildSkillRequest> Collect(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, FightBattleUnitState target,
                FightBattleSkillCastPresentation presentation)
            {
                var result = new List<ChildSkillRequest>();
                if (hit != null || context.startHandled || !EmptyFilter(context.startFlow)) return result;
                var handles = Array(context.startFlow, "HandleCfgs");
                if (handles.Count != 2 || !(handles[0] is Dictionary<string, object> read)
                    || !(handles[1] is Dictionary<string, object> trigger)
                    || String(read, "_type") != "FightHandleCfgGetMasterEntityAndStore"
                    || !IsPort(read, "EntityKey", 1, false)
                    || !IsPort(read, "StorageBBKey", 796681548, false)
                    || !IsChildTrigger(trigger, 54373, 796681548)) return result;
                context.startHandled = true;
                // The official handle specifies presence in the battlefield; HP
                // is not a replacement for presence or the child's own conditions.
                if (context.actor.MasterId == 0 || context.units == null) return result;
                foreach (var unit in context.units)
                    if (unit != null && !unit.Ended && unit.Id == context.actor.MasterId)
                    {
                        result.Add(Request(54373, unit, 0f));
                        break;
                    }
                return result;
            }
        }

        private sealed class TargetChildSkill : IFightSkillChildProvider
        {
            public List<ChildSkillRequest> Collect(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, FightBattleUnitState target,
                FightBattleSkillCastPresentation presentation)
            {
                var result = new List<ChildSkillRequest>();
                if (hit == null || hit.EntityId != 54374 || target == null
                    || target.Ended || !EmptyFilter(context.hitFlow)) return result;
                var handles = Array(context.hitFlow, "HandleCfgs");
                if (handles.Count != 1 || !(handles[0] is Dictionary<string, object> trigger)
                    || !IsChildTrigger(trigger, 54375, 2003)) return result;
                // SkillTargetEntity binds the cast target, not a newly selected AI
                // victim and not each cell of a large target's body.
                if (context.units != null) foreach (var unit in context.units)
                    if (unit != null && !unit.Ended && unit.Id == target.Id)
                    {
                        result.Add(Request(54375, unit, Math.Max(0f, hit.Delay)));
                        break;
                    }
                return result;
            }
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

        private static ChildSkillRequest Request(int skillId, FightBattleUnitState target, float delay)
            => new ChildSkillRequest { SkillId = skillId, Target = target,
                Position = new FightGridPoint(target.X, target.Y), Delay = delay };

        private static bool IsChildTrigger(Dictionary<string, object> handle, int skillId, int targetPort)
            => String(handle, "_type") == "FightHandleCfgTriggerSkillBySkill"
                && Int(handle, "SkillEntityClassId") == skillId
                && handle.TryGetValue("Rate", out object rate) && Convert.ToDouble(rate) == 1.0
                && IsPort(handle, "SourceEntityKey", 1, false)
                && IsPort(handle, "TargetEntityKey", targetPort, false);
    }
}
