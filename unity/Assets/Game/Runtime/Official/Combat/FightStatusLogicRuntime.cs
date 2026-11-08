using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // The APK ships MyFightStatusLogic as an empty client component.  This interpreter keeps the
    // original polymorphic graph in the status checkpoint and executes the data-driven subset
    // whose event, filter and handle semantics are observable from the shared component types.
    internal enum FightStatusLogicEvent
    {
        StatusSelfInit,
        StatusStart,
        StatusEnd,
        RoundStart,
        RoundEnd,
        DoDamage,
        BeDamage,
        AfterBeDamage,
        SkillEnd
    }

    internal static class FightStatusLogicRuntime
    {
        public static List<FightBattleStatusSkillInvocation> Apply(
            FightBattleUnitState owner, FightBattleStatusState status,
            FightStatusLogicEvent eventKind, FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (owner == null || status == null || !status.HasLogicComponent
                || !status.LogicDefinitionResolved || string.IsNullOrEmpty(status.LogicDefinitionJson)
                || status.ForbiddenType != 0) return result;
            Dictionary<string, object> component;
            try { component = OfficialJson.Parse(status.LogicDefinitionJson) as Dictionary<string, object>; }
            catch (Exception) { status.LogicRulesSupported = false; return result; }
            if (component == null || !(component.TryGetValue("Triggers", out object rawTriggers)
                && rawTriggers is List<object> triggers)) return result;

            foreach (object raw in triggers)
            {
                if (!(raw is Dictionary<string, object> trigger)) continue;
                string triggerType = StringField(trigger, "_type");
                if (!MatchesEvent(triggerType, eventKind)) continue;
                Dictionary<string, object> flow = Object(trigger, "Flow");
                if (flow == null) continue;
                bool supported;
                if (!MatchesFilter(Object(flow, "FilterCondition"), owner, status, actor, target,
                        skill, hit, units, random, out supported))
                {
                    if (!supported) status.LogicRulesSupported = false;
                    continue;
                }
                if (!(flow.TryGetValue("HandleCfgs", out object rawHandles)
                    && rawHandles is List<object> handles)) continue;
                foreach (object rawHandle in handles)
                {
                    if (!(rawHandle is Dictionary<string, object> handle)) continue;
                    if (!ApplyHandle(handle, owner, status, actor, target, skill, hit, units,
                        catalog, random, result)) status.LogicRulesSupported = false;
                }
                status.LogicTriggeredCount++;
            }
            return result;
        }

        private static bool MatchesEvent(string type, FightStatusLogicEvent eventKind)
        {
            switch (eventKind)
            {
                case FightStatusLogicEvent.StatusSelfInit: return type == "FightOnStatusSelfInitTriggerCfg";
                case FightStatusLogicEvent.StatusStart: return type == "FightOnStatusStartTriggerCfg";
                case FightStatusLogicEvent.StatusEnd: return type == "FightOnStatusEndTriggerCfg";
                case FightStatusLogicEvent.RoundStart: return type == "FightOnRoundStartTriggerCfg";
                case FightStatusLogicEvent.RoundEnd: return type == "FightOnRoundEndTriggerCfg";
                case FightStatusLogicEvent.DoDamage: return type == "FightOnDoDamageTriggerCfg";
                case FightStatusLogicEvent.BeDamage: return type == "FightOnBeDamageTriggerCfg";
                case FightStatusLogicEvent.AfterBeDamage: return type == "FightOnAfterBeDamageTriggerCfg";
                case FightStatusLogicEvent.SkillEnd: return type == "FightOnSkillEndTriggerCfg";
                default: return false;
            }
        }

        private static bool MatchesFilter(Dictionary<string, object> filter,
            FightBattleUnitState owner, FightBattleStatusState status,
            FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            out bool supported)
        {
            supported = true;
            if (filter == null) return true;
            string type = StringField(filter, "_type");
            if (type == "LogicFilter")
            {
                List<object> sub = Array(filter, "SubFilters");
                int logicType = Int(filter, "LogicType");
                bool value = logicType == 1 ? false : true;
                foreach (object raw in sub)
                {
                    bool childSupported;
                    bool child = MatchesFilter(raw as Dictionary<string, object>, owner, status, actor,
                        target, skill, hit, units, random, out childSupported);
                    supported &= childSupported;
                    value = logicType == 1 ? value || child : value && child;
                }
                return value;
            }
            if (type == "LogicNotFilter")
            {
                Dictionary<string, object> nested = Object(filter, "SubFilter")
                    ?? Object(filter, "FilterCondition");
                bool childSupported;
                bool child = MatchesFilter(nested, owner, status, actor, target, skill, hit,
                    units, random, out childSupported);
                supported = childSupported;
                return !child;
            }
            if (type == "RateFilter") return random == null || random.Next() < Double(filter, "Rate", 1d);
            if (type == "CounterLessFilter" || type == "CounterLessEqualFilter"
                || type == "CounterEqualFilter" || type == "CounterGreaterFilter"
                || type == "CounterGreaterEqualFilter")
            {
                int key = KeyId(filter, "CounterBBKey");
                long value = Blackboard(status, key);
                long expected = Long(filter, "Value");
                if (type == "CounterLessFilter") return value < expected;
                if (type == "CounterLessEqualFilter") return value <= expected;
                if (type == "CounterEqualFilter") return value == expected;
                if (type == "CounterGreaterFilter") return value > expected;
                return value >= expected;
            }
            if (type == "BBIntEqualFilter" || type == "BBIntNotEqualFilter"
                || type == "BBIntLessFilter" || type == "BBIntLessEqualFilter"
                || type == "BBIntGreaterFilter" || type == "BBIntGreaterEqualFilter")
            {
                int key = KeyId(filter, "BBKey");
                long value = Blackboard(status, key);
                long expected = Long(filter, "Value");
                if (type == "BBIntEqualFilter") return value == expected;
                if (type == "BBIntNotEqualFilter") return value != expected;
                if (type == "BBIntLessFilter") return value < expected;
                if (type == "BBIntLessEqualFilter") return value <= expected;
                if (type == "BBIntGreaterFilter") return value > expected;
                return value >= expected;
            }
            if (type == "EntityHpPercentFilter")
            {
                FightBattleUnitState entity = ResolveEntity(Object(filter, "EntityKey"), owner, actor, target, units);
                if (entity == null || entity.MaxHp <= 0) return false;
                double current = (double)Math.Max(0L, entity.Hp) / entity.MaxHp;
                return Compare(current, Double(filter, "HpPercent", 0d), Int(filter, "Relation", 4));
            }
            if (type == "ChangedStatusClassIdFilter")
                return status != null && status.ClassId == Int(filter, "StatusClassId");
            if (type == "StatusClassIdFilter" || type == "IteratingAgentStatusClassIdFilter")
                return status != null && status.ClassId == Int(filter, "StatusClassId");
            if (type == "EntityClassIdFilter")
            {
                FightBattleUnitState entity = ResolveEntity(Object(filter, "EntityKey"), owner, actor, target, units);
                return entity != null && (entity.MonsterTemplateId == Int(filter, "EntityClassId")
                    || entity.NpcClassId == Int(filter, "EntityClassId")
                    || entity.RoleEntityId == Int(filter, "EntityClassId"));
            }
            supported = false;
            return false;
        }

        private static bool ApplyHandle(Dictionary<string, object> handle,
            FightBattleUnitState owner, FightBattleStatusState status,
            FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random, List<FightBattleStatusSkillInvocation> output)
        {
            string type = StringField(handle, "_type");
            if (type == "FightHandleCfgCounterAdd")
            {
                AddBlackboard(status, KeyId(handle, "CounterBBKey"), Long(handle, "DeltaValue"));
                return true;
            }
            if (type == "FightHandleCfgCounterReset")
            {
                SetBlackboard(status, KeyId(handle, "CounterBBKey"), Long(handle, "ResetValue"));
                return true;
            }
            if (type == "FightHandleCfgNewIntInBB" || type == "FightHandleCfgNewLongInBB")
            {
                SetBlackboard(status, KeyId(handle, "BBKey"), Long(handle, "Value"));
                return true;
            }
            if (type == "FightHandleCfgNewBoolInBB")
            {
                SetBlackboard(status, KeyId(handle, "BBKey"), Bool(handle, "Value") ? 1 : 0);
                return true;
            }
            if (type == "FightHandleCfgTriggerSkills")
            {
                foreach (object raw in Array(handle, "TriggerSkillCfgs"))
                {
                    Dictionary<string, object> cfg = raw as Dictionary<string, object>;
                    int skillId = Int(cfg, "SkillId");
                    if (skillId <= 0 || !Roll(random, Float(cfg, "BasePercent", 1f))) continue;
                    FightBattleStatusStateTrigger trigger = TriggerSkill(cfg, status);
                    FightBattleUnitState source = ResolveEntity(Object(handle, "SourceEntityKey"), owner, actor,
                        target, units) ?? owner;
                    FightBattleUnitState destination = ResolveEntity(Object(handle, "TargetEntityKey"), owner,
                        actor, target, units) ?? target ?? owner;
                    output.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = trigger,
                        SourceUnitId = source.Id,
                        TargetUnitId = destination.Id,
                        TargetPositionUnitId = destination.Id,
                        SelectTargets = trigger.Target == 0 || trigger.Target == 8,
                        UseTargetPositionWithSelection = trigger.TargetPos == 1
                    });
                }
                return true;
            }
            if (type == "FightHandleCfgTriggerStatuss")
            {
                if (catalog == null) return false;
                FightBattleUnitState destination = ResolveEntity(Object(handle, "TargetEntityKey"), owner,
                    actor, target, units) ?? owner;
                foreach (object raw in Array(handle, "TriggerStatusCfgs"))
                {
                    Dictionary<string, object> cfg = raw as Dictionary<string, object>;
                    int statusId = Int(cfg, "StatusId");
                    if (statusId <= 0 || !Roll(random, Float(cfg, "BasePercent", 1f))) continue;
                    var applied = catalog.CreateStatus(statusId, destination.Id, status.CreatorId,
                        rootSkillId: status.RootSkillId, applyingSkillItemId: status.ApplyingSkillItemId,
                        sourceSkillRank: status.SourceSkillRank, sourceSkillLevel: status.SourceSkillLevel,
                        sourceSubRank: status.SourceSubRank, sourceProperties: status.SourceProperties,
                        replacementProperties: status.ReplacementProperties,
                        blessPropertyScales: status.BlessPropertyScales);
                    FightStatusLifecycle.Apply(destination, applied, destination.SkillState.Round,
                        applied.InstanceId);
                }
                return true;
            }
            if (type == "FightHandleRemoveSelfStatusCfg")
            {
                FightStatusLifecycle.End(owner, status);
                return true;
            }
            if (type == "FightHandleRemoveStatusCfg")
            {
                FightBattleUnitState destination = ResolveEntity(Object(handle, "TargetEntityKey"), owner,
                    actor, target, units) ?? owner;
                Dictionary<string, object> filter = Object(handle, "FilterCondition");
                foreach (FightBattleStatusState candidate in new List<FightBattleStatusState>(destination.Statuses))
                {
                    bool supported;
                    if (MatchesFilter(filter, destination, candidate, actor, target, skill, hit, units,
                        random, out supported) && (filter == null || supported))
                        FightStatusLifecycle.End(destination, candidate);
                }
                return true;
            }
            if (type == "FightHandleCfgChangeStatusStack")
            {
                FightBattleUnitState destination = ResolveEntity(Object(handle, "TargetEntityKey"), owner,
                    actor, target, units) ?? owner;
                int statusId = Int(handle, "CreateStatusClassId");
                int delta = (int)Blackboard(status, KeyId(handle, "ChangeValueKey"));
                foreach (FightBattleStatusState candidate in destination.Statuses)
                    if (candidate != null && candidate.ClassId == statusId) FightStatusLifecycle.AddStack(candidate, delta);
                return true;
            }
            return false;
        }

        private static FightBattleStatusStateTrigger TriggerSkill(Dictionary<string, object> cfg,
            FightBattleStatusState source)
        {
            var result = new FightBattleStatusStateTrigger
            {
                EntityId = Int(cfg, "SkillId"), BasePercent = Float(cfg, "BasePercent", 1f),
                AffectedByProp = Bool(cfg, "AffectedByProp"), Source = Int(cfg, "Source"),
                Target = Int(cfg, "Target"), UseAI = Bool(cfg, "UseAI"), TargetPos = Int(cfg, "TargetPos"),
                StackedCountToDamageFactor = Bool(cfg, "StackedCountToDamageFactor"),
                TargetWithoutStatusIds = IntArray(cfg, "TargetWithoutStatusIds")
            };
            result.Origin = FightBattleStatusOrigin.Resolve(source).Copy();
            result.SourceSkillItemId = FightBattleStatusReportSource.Resolve(source).SkillItemId;
            result.SourceSkillRank = source.SourceSkillRank;
            result.SourceSkillLevel = source.SourceSkillLevel;
            result.SourceSubRank = source.SourceSubRank;
            return result;
        }

        private static FightBattleUnitState ResolveEntity(Dictionary<string, object> key,
            FightBattleUnitState owner, FightBattleUnitState actor, FightBattleUnitState target,
            IReadOnlyList<FightBattleUnitState> units)
        {
            int id = KeyId(key, null);
            if (id == 1) return owner ?? actor;
            if (id == 1022 || id == 1023) return target;
            if (id == 2) return owner;
            return actor ?? owner ?? target;
        }

        private static long Blackboard(FightBattleStatusState status, int key)
        {
            if (status?.LogicBlackboard == null) return 0;
            foreach (FightBattleLogicBlackboardValue value in status.LogicBlackboard)
                if (value != null && value.KeyId == key) return value.LongValue;
            return 0;
        }

        private static void SetBlackboard(FightBattleStatusState status, int key, long value)
        {
            if (status == null || key == 0) return;
            if (status.LogicBlackboard == null) status.LogicBlackboard = new List<FightBattleLogicBlackboardValue>();
            foreach (FightBattleLogicBlackboardValue current in status.LogicBlackboard)
                if (current != null && current.KeyId == key) { current.LongValue = value; return; }
            status.LogicBlackboard.Add(new FightBattleLogicBlackboardValue { KeyId = key, LongValue = value });
        }

        private static void AddBlackboard(FightBattleStatusState status, int key, long value)
            => SetBlackboard(status, key, Blackboard(status, key) + value);

        private static bool Compare(double left, double right, int relation)
        {
            switch (relation)
            {
                case 0: return Math.Abs(left - right) < 0.0000001d;
                case 1: return left >= right;
                case 2: return left > right;
                case 3: return left <= right;
                default: return left < right;
            }
        }

        private static bool Roll(FightRandomStream random, float rate)
            => rate >= 1f || random != null && random.Next() < Math.Max(0f, rate);

        private static Dictionary<string, object> Object(Dictionary<string, object> source, string key)
            => source != null && source.TryGetValue(key, out object value)
                ? value as Dictionary<string, object> : null;

        private static List<object> Array(Dictionary<string, object> source, string key)
            => source != null && source.TryGetValue(key, out object value) && value is List<object> list
                ? list : new List<object>();

        private static string StringField(Dictionary<string, object> source, string key)
            => source != null && source.TryGetValue(key, out object value) ? value as string : null;

        private static int KeyId(Dictionary<string, object> source, string key)
        {
            Dictionary<string, object> value = key == null ? source : Object(source, key);
            return Int(value, "Id");
        }

        private static int[] IntArray(Dictionary<string, object> source, string key)
        {
            var result = new List<int>();
            foreach (object value in Array(source, key)) result.Add((int)LongValue(value));
            return result.ToArray();
        }

        private static int Int(Dictionary<string, object> source, string key, int fallback = 0)
            => source != null && source.TryGetValue(key, out object value) ? (int)LongValue(value) : fallback;

        private static long Long(Dictionary<string, object> source, string key, long fallback = 0)
            => source != null && source.TryGetValue(key, out object value) ? LongValue(value) : fallback;

        private static double Double(Dictionary<string, object> source, string key, double fallback = 0d)
            => source != null && source.TryGetValue(key, out object value) ? DoubleValue(value) : fallback;

        private static float Float(Dictionary<string, object> source, string key, float fallback = 0f)
            => (float)Double(source, key, fallback);

        private static bool Bool(Dictionary<string, object> source, string key, bool fallback = false)
            => source != null && source.TryGetValue(key, out object value) ? value is bool b && b : fallback;

        private static long LongValue(object value)
            => value is long l ? l : value is int i ? i : value is double d ? (long)d : 0L;

        private static double DoubleValue(object value)
            => value is double d ? d : value is long l ? l : value is int i ? i : 0d;
    }
}
