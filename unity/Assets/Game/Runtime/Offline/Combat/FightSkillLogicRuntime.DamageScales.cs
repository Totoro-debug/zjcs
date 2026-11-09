using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        // Each adapter interprets a different original graph. The enclosing cast owns
        // blackboards and applies the shared, confirmed scaling stage after selection.
        private interface IFightSkillDamageScale
        {
            bool TryRead(FightSkillLogicRuntime cast, FightBattleUnitState target, out DamageScaleSelection selection);
        }

        private sealed class DamageScaleSelection
        {
            internal Dictionary<string, object> Handle;
            internal long Count;
            internal int Key, Value;
            internal bool Persistent;
        }

        private static IFightSkillDamageScale CreateDamageScale(int skillId)
        {
            switch (skillId)
            {
                case 13820: return new ChargeSnapshotScale();
                case 50210: return new CurrentStatusStackScale();
                case 13730: return new BurningGridScale();
                case 13870: return new TargetHpScale();
                case 13970: return new OwnerStatusScale();
                default: return null;
            }
        }

        private sealed class ChargeSnapshotScale : IFightSkillDamageScale
        {
            public bool TryRead(FightSkillLogicRuntime cast, FightBattleUnitState target, out DamageScaleSelection selection)
            {
                selection = null;
                var handlers = Array(cast.flow, "HandleCfgs");
                if (!EmptyFilter(cast.flow) || !cast.startHandled || handlers.Count != 1
                    || !(handlers[0] is Dictionary<string, object> scale)
                    || !cast.persistentBlackboard.TryGetValue(884090696, out long count)) return false;
                selection = new DamageScaleSelection { Handle = scale, Count = count,
                    Key = 884090696, Value = 700, Persistent = true };
                return true;
            }
        }

        private sealed class CurrentStatusStackScale : IFightSkillDamageScale
        {
            public bool TryRead(FightSkillLogicRuntime cast, FightBattleUnitState target, out DamageScaleSelection selection)
            {
                selection = null;
                var handlers = Array(cast.flow, "HandleCfgs");
                if (!EmptyFilter(cast.flow) || handlers.Count != 2 || !(handlers[0] is Dictionary<string, object> read)
                    || !(handlers[1] is Dictionary<string, object> scale)
                    || String(read, "_type") != "FightHandleCfgGetStatusStackCount"
                    || !IsPort(read, "SourceEntityKey", 1, false)
                    || !IsPort(read, "StackCountKey", 719180550, false)
                    || Int(read, "StatusId") != 24823) return false;
                long count = 0;
                foreach (var status in cast.actor.Statuses)
                    if (status != null && status.ClassId == 24823
                        && status.ForbiddenType == 0 && status.StackCount > 0)
                    {
                        count += status.StackCount;
                        if (count > int.MaxValue) return false;
                    }
                selection = new DamageScaleSelection { Handle = scale, Count = count,
                    Key = 719180550, Value = 2000 };
                return true;
            }
        }

        private sealed class BurningGridScale : IFightSkillDamageScale
        {
            public bool TryRead(FightSkillLogicRuntime cast, FightBattleUnitState target, out DamageScaleSelection selection)
            {
                selection = null;
                if (!EmptyFilter(cast.flow) || !TryCount(cast, target, Array(cast.flow, "HandleCfgs"), out long count, out var scale)) return false;
                selection = new DamageScaleSelection { Handle = scale, Count = count,
                    Key = 511879177, Value = 500 };
                return true;
            }
            private static bool TryCount(FightSkillLogicRuntime cast, FightBattleUnitState target, List<object> handlers,
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
                if (cast.gridItems != null) foreach (var item in cast.gridItems)
                    if (item != null && !item.Ended && item.EntityClassId == 3051
                        && item.HasRuntime && item.Runtime != null && !item.Runtime.Ended
                        && item.Runtime.Side == cast.actor.Side
                        && offsets.Contains(new FightGridPoint(item.X - target.X, item.Y - target.Y))
                        && seen.Add(item.Id)) count++;
                scale = multiply;
                return true;
            }
    
        }
    }
}
