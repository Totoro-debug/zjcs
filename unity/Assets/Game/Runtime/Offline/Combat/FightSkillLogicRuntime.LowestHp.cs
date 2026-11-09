using System;
using System.Collections.Generic;
using System.Numerics;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        private interface IFightSkillCureAvailability
        {
            bool HasInjuredTarget(FightSkillLogicRuntime context);
        }

        internal static bool? CureAvailability(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog)
        {
            var context = Create(actor, skill, units, catalog, null);
            return context?.childSkillSource is IFightSkillCureAvailability availability
                ? availability.HasInjuredTarget(context) : (bool?)null;
        }

        private sealed class LowestHpChildSkills : IFightSkillChildProvider, IFightSkillCureAvailability
        {
            private sealed class Candidate
            {
                internal FightBattleUnitState Unit;
                internal long Hp, MaxHp;
                internal int Order;
            }

            public bool HasInjuredTarget(FightSkillLogicRuntime context)
            {
                // Availability belongs to this collection graph. Its primary
                // anchor may be the full-HP caster while its children cure allies.
                foreach (var request in Collect(context,
                    new FightBattleHitDefinition { EntityId = 13641 }, null, null))
                    if (request.Target.Hp < request.Target.MaxHp) return true;
                return false;
            }

            public List<ChildSkillRequest> Collect(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, FightBattleUnitState target,
                FightBattleSkillCastPresentation presentation)
            {
                var result = new List<ChildSkillRequest>();
                if (!context.skill.CanTriggerChild || hit == null || hit.EntityId != 13641
                    || context.units == null || !TryReadRange(context.hitFlow, out var range)) return result;
                var selected = new List<Candidate>();
                var seen = new HashSet<int>();
                for (int i = 0; i < context.units.Count; i++)
                {
                    var unit = context.units[i];
                    if (!FightBattleTargeting.IsEligibleSkillTarget(context.actor, unit, context.skill)
                        || unit.MaxHp <= 0 || !seen.Add(unit.Id) || !InRange(context.actor, unit, range)) continue;
                    // The approved cure-target rule also keeps full-HP candidates.
                    // Snapshot before dispatching either child; a first recovery
                    // must not change the second target or the original tie order.
                    selected.Add(new Candidate { Unit = unit, Hp = unit.Hp,
                        MaxHp = unit.MaxHp, Order = i });
                }
                selected.Sort((left, right) => {
                    int comparison = ((BigInteger)left.Hp * right.MaxHp)
                        .CompareTo((BigInteger)right.Hp * left.MaxHp);
                    return comparison != 0 ? comparison : left.Order.CompareTo(right.Order);
                });
                for (int i = 0; i < selected.Count && i < 2; i++)
                    result.Add(Request(13643, selected[i].Unit, Math.Max(0f, hit.Delay)));
                return result;
            }

            private static bool InRange(FightBattleUnitState actor, FightBattleUnitState target,
                HashSet<FightGridPoint> range)
            {
                if (target.BodyOffsets == null || target.BodyOffsets.Count == 0)
                    return range.Contains(new FightGridPoint(target.X - actor.X, target.Y - actor.Y));
                foreach (var offset in target.BodyOffsets)
                    if (range.Contains(new FightGridPoint(target.X + offset.X - actor.X,
                        target.Y + offset.Y - actor.Y))) return true;
                return false;
            }

            private static bool TryReadRange(Dictionary<string, object> flow,
                out HashSet<FightGridPoint> range)
            {
                range = null;
                var handles = Array(flow, "HandleCfgs");
                if (!EmptyFilter(flow) || handles.Count != 3
                    || !(handles[0] is Dictionary<string, object> collect)
                    || !(handles[1] is Dictionary<string, object> sort)
                    || !(handles[2] is Dictionary<string, object> each)
                    || String(collect, "_type") != "FightHandleCfgCollectionEntitiesByFaction2Battle"
                    || !IsPort(collect, "SourceEntityKey", 1, false)
                    || !IsPort(collect, "ResultEntityArrayKey", 406339594, false)
                    || String(sort, "_type") != "FightHandleCfgSortEntitiesByHpPercent"
                    || !IsPort(sort, "ReadEntityArrayKey", 406339594, false)
                    || !IsPort(sort, "SaveEntityArrayKey", 1359978515, false)
                    || !Bool(sort, "IsAscendingOrder")
                    || String(each, "_type") != "FightHandleCfgForeachEntities"
                    || !IsPort(each, "EntityArrayKey", 1359978515, false)
                    || Int(each, "ForeachCount") != 2) return false;
                var nested = Array(each, "HandleCfgs");
                if (nested.Count != 1 || !(nested[0] is Dictionary<string, object> trigger)
                    || String(trigger, "_type") != "FightHandleCfgTriggerSkillBySkill"
                    || Int(trigger, "SkillEntityClassId") != 13643
                    || !trigger.TryGetValue("Rate", out object rate) || Convert.ToDouble(rate) != 1d
                    || !IsPort(trigger, "SourceEntityKey", 3, false)
                    || !IsPort(trigger, "TargetEntityKey", 1, false)) return false;
                var filter = Object(collect, "FilterCondition");
                var filters = Array(filter, "SubFilters");
                if (String(filter, "_type") != "LogicFilter" || Int(filter, "LogicType") != 0
                    || filters.Count != 2 || !(filters[0] is Dictionary<string, object> area)
                    || !(filters[1] is Dictionary<string, object> eligible)
                    || String(area, "_type") != "RangeFilter"
                    || !IsPort(area, "CenterEntity", 3, false)
                    || !IsPort(area, "TargetEntityKey", 1, false)
                    || Bool(area, "UseWorldPosByCenterEntityTransform") || !Bool(area, "ConsiderTargetBodySize")
                    || String(eligible, "_type") != "IsSkillTargetFilter"
                    || !IsPort(eligible, "MeEntityKey", 3, false)
                    || !IsPort(eligible, "TargetEntityKey", 1, false)
                    || !IsPort(eligible, "SkillEntityKey", 4, false)) return false;
                var points = new HashSet<FightGridPoint>();
                foreach (object value in Array(area, "Range"))
                {
                    if (!(value is Dictionary<string, object> point)
                        || !point.ContainsKey("X") || !point.ContainsKey("Y")) return false;
                    int x = Int(point, "X"), y = Int(point, "Y");
                    if (x < -7 || x > 7 || y < -7 || y > 7 || Math.Abs(x) + Math.Abs(y) > 7
                        || !points.Add(new FightGridPoint(x, y))) return false;
                }
                if (points.Count != 113) return false;
                range = points;
                return true;
            }
        }
    }
}
