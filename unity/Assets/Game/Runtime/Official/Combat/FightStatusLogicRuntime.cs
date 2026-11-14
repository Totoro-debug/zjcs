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

    internal static partial class FightStatusLogicRuntime
    {
        public static List<FightBattleStatusSkillInvocation> Apply(
            FightBattleUnitState owner, FightBattleStatusState status,
            FightStatusLogicEvent eventKind, FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random,
            FightBattleDamageEvent damageEvent = null, FightDamageFormula formula = null)
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

            RemoveLegacyRuntimeValues(status, triggers);
            if (status.ClassId == 25021 && eventKind == FightStatusLogicEvent.AfterBeDamage)
                return ApplyVectorReflection(owner, status, actor, target, skill, hit,
                    damageEvent, triggers);
            if (status.ClassId == 25017 && eventKind == FightStatusLogicEvent.AfterBeDamage)
                return ApplySwordCounterattack(owner, status, actor, target, skill, hit,
                    damageEvent, triggers, random);
            if (status.ClassId == 25035 && eventKind == FightStatusLogicEvent.AfterBeDamage)
                return ApplyReflectionArmor(owner, status, actor, target, skill, hit,
                    damageEvent, triggers, formula);
            // User-confirmed offline rule: one Runtime board per event, shared by its flows.
            var runtime = new Dictionary<int, FightBattleLogicBlackboardValue>();
            foreach (object raw in triggers)
            {
                if (!(raw is Dictionary<string, object> trigger)) continue;
                string triggerType = StringField(trigger, "_type");
                if (!MatchesEvent(triggerType, eventKind)) continue;
                Dictionary<string, object> flow = Object(trigger, "Flow");
                if (flow == null) continue;
                bool supported;
                bool matched = MatchesFilter(Object(flow, "FilterCondition"), owner, status, actor, target,
                    skill, hit, units, random, out supported, runtime: runtime);
                if (!supported || !matched)
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
                        catalog, random, result, runtime))
                    {
                        status.LogicRulesSupported = false;
                        // A later handle may depend on the missing handle's blackboard outputs.
                        // Stop this flow rather than executing it with default/old values.
                        break;
                    }
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
            out bool supported, StatusIteration iteration = null,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime = null)
        {
            supported = true;
            if (filter == null) return true;
            string type = StringField(filter, "_type");
            if (type == "LogicFilter")
            {
                List<object> sub = Array(filter, "SubFilters");
                int logicType = Int(filter, "LogicType");
                if (logicType != 0 && logicType != 1) { supported = false; return false; }
                bool value = logicType == 1 ? false : true;
                foreach (object raw in sub)
                {
                    bool childSupported;
                    if (!(raw is Dictionary<string, object> childFilter))
                    {
                        supported = false;
                        continue;
                    }
                    bool child = MatchesFilter(childFilter, owner, status, actor,
                        target, skill, hit, units, random, out childSupported, iteration, runtime);
                    supported &= childSupported;
                    value = logicType == 1 ? value || child : value && child;
                }
                return supported && value;
            }
            if (type == "LogicNotFilter")
            {
                Dictionary<string, object> nested = Object(filter, "SubFilter")
                    ?? Object(filter, "FilterCondition") ?? Object(filter, "TargetFilter");
                if (nested == null) { supported = false; return false; }
                bool childSupported;
                bool child = MatchesFilter(nested, owner, status, actor, target, skill, hit,
                    units, random, out childSupported, iteration, runtime);
                supported = childSupported;
                return supported && !child;
            }
            if (type == "RateFilter") return random == null || random.Next() < Double(filter, "Rate", 1d);
            if (type == "SkillClassIdByEcIdKeyFilter")
            {
                // Confirmed binding: DamageSkill is this hit's actual skill entity, not its root.
                if (KeyId(filter, "SkillEntityIdKey") != 1025 || skill == null || hit == null)
                { supported = false; return false; }
                return skill.EntityId == Int(filter, "SkillClassId");
            }
            if (type == "CounterLessFilter" || type == "CounterLessEqualFilter"
                || type == "CounterEqualFilter" || type == "CounterGreaterFilter"
                || type == "CounterGreaterEqualFilter")
            {
                long value = Blackboard(status, Object(filter, "CounterBBKey"), runtime);
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
                long value = Blackboard(status, Object(filter, "LhsBBKey"), runtime);
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
                int port = KeyId(filter, "EntityKey");
                FightBattleUnitState entity = port == 1 ? owner : port == 1022 && hit != null ? target : null;
                if (entity == null) { supported = false; return false; }
                if (entity.MaxHp <= 0) return false;
                double current = (double)Math.Max(0L, entity.Hp) / entity.MaxHp;
                return Compare(current, Double(filter, "HpPercent", 0d), Int(filter, "Relation", 4));
            }
            if (type == "ChangedStatusClassIdFilter")
            {
                if (iteration != null) { supported = false; return false; }
                return status != null && status.ClassId == Int(filter, "StatusClassId");
            }
            if (type == "StatusClassIdFilter" || type == "IteratingAgentStatusClassIdFilter")
            {
                if (type == "IteratingAgentStatusClassIdFilter" && iteration == null)
                { supported = false; return false; }
                FightBattleStatusState current = iteration == null ? status : iteration.Candidate;
                return current != null && current.ClassId == Int(filter, "StatusClassId");
            }
            if (type == "EntityKeyEqualsEntityFilter" && iteration != null)
            {
                object left, right;
                if (!TryIterationEntity(KeyId(filter, "LhsEntityKey"), owner, status, iteration, out left)
                    || !TryIterationEntity(KeyId(filter, "RhsEntityKey"), owner, status, iteration, out right))
                { supported = false; return false; }
                return left != null && right != null && ReferenceEquals(left, right);
            }
            if (type == "EntityClassIdFilter")
            {
                if (iteration != null && KeyId(filter, "EntityKey") == 1072)
                    return iteration.Candidate != null
                        && iteration.Candidate.ClassId == Int(filter, "ClassId");
                // Status/pet/summon/removed-entity ports require their own event context.
                // Do not silently reinterpret them as the current damage actor.
                if (KeyId(filter, "EntityKey") != 1) { supported = false; return false; }
                FightBattleUnitState entity = ResolveEntity(Object(filter, "EntityKey"), owner, actor, target, units);
                int classId = Int(filter, "ClassId");
                return entity != null && classId > 0 && (entity.MonsterTemplateId == classId
                    || entity.NpcClassId == classId || entity.RoleEntityId == classId);
            }
            supported = false;
            return false;
        }

        private static bool ApplyHandle(Dictionary<string, object> handle,
            FightBattleUnitState owner, FightBattleStatusState status,
            FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random, List<FightBattleStatusSkillInvocation> output,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime)
        {
            string type = StringField(handle, "_type");
            if (type == "FightHandleCfgCounterAdd")
            {
                SetBlackboard(status, Object(handle, "CounterBBKey"), runtime,
                    Blackboard(status, Object(handle, "CounterBBKey"), runtime) + Long(handle, "DeltaValue"));
                return true;
            }
            if (type == "FightHandleCfgCounterReset")
            {
                SetBlackboard(status, Object(handle, "CounterBBKey"), runtime, Long(handle, "ResetValue"));
                return true;
            }
            if (type == "FightHandleCfgNewIntInBB" || type == "FightHandleCfgNewLongInBB")
            {
                SetBlackboard(status, Object(handle, "BBKey"), runtime, Long(handle, "Value"));
                return true;
            }
            if (type == "FightHandleCfgNewBoolInBB")
            {
                SetBlackboard(status, Object(handle, "BBKey"), runtime, Bool(handle, "Value") ? 1 : 0);
                return true;
            }
            if (type == "FightHandleCfgBBPickFloatByIndex")
            {
                List<object> values = Array(handle, "Values");
                long index = Blackboard(status, Object(handle, "IndexKey"), runtime);
                // Official description: an out-of-range index uses the last value; empty uses 1.
                double value = values.Count == 0 ? 1d : DoubleValue(values[
                    index < 0 || index >= values.Count ? values.Count - 1 : (int)index]);
                SetDoubleBlackboard(status, Object(handle, "StorageKey"), runtime, value);
                return true;
            }
            if (type == "FightHandleCfgTriggerSkills")
            {
                // DamageResult.Pos needs its own damage-entity context; the unit target
                // cannot substitute for that authored position port.
                if (Bool(handle, "UseDamagePos")) return false;
                FightBattleUnitState source = ResolveEntity(Object(handle, "SourceEntityKey"), owner, actor,
                    target, units);
                FightBattleUnitState destination = ResolveEntity(Object(handle, "TargetEntityKey"), owner,
                    actor, target, units);
                if (source == null || destination == null) return false;
                foreach (object raw in Array(handle, "TriggerSkillCfgs"))
                {
                    Dictionary<string, object> cfg = raw as Dictionary<string, object>;
                    int skillId = Int(cfg, "SkillId");
                    if (skillId <= 0 || !Roll(random, Float(cfg, "BasePercent", 1f))) continue;
                    FightBattleStatusStateTrigger trigger = TriggerSkill(cfg, status);
                    output.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = trigger,
                        DamageFactor = DoubleBlackboard(status, Object(handle, "DamageFactorKey"), runtime, 1d),
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
                if (ResolveEntity(Object(handle, "SourceEntityKey"), owner, actor, target, units) == null)
                    return false;
                FightBattleUnitState destination = ResolveEntity(Object(handle, "TargetEntityKey"), owner,
                    actor, target, units);
                if (destination == null) return false;
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
                    actor, target, units);
                if (destination == null || !TryMatchStatuses(destination, Object(handle, "FilterCondition"),
                    owner, status, actor, target, skill, hit, units, null,
                    out _, runtime)) return false;
                // Preflight only support/bindings, without random draws. Preserve the
                // existing interleaving of real filtering and lifecycle end callbacks.
                foreach (FightBattleStatusState candidate in new List<FightBattleStatusState>(destination.Statuses))
                {
                    bool supported;
                    if (MatchesFilter(Object(handle, "FilterCondition"), owner, status, actor, target,
                        skill, hit, units, random, out supported,
                        new StatusIteration { Candidate = candidate }, runtime) && supported)
                        FightStatusLifecycle.End(destination, candidate);
                }
                return true;
            }
            if (type == "FightHandleCfgChangeStatusStack")
                return ChangeStatusStacks(handle, owner, status, actor, target, skill, hit,
                    units, catalog, random, runtime: runtime);
            if (type == "FightHandleCfgAbsorbStatusStack")
                return AbsorbStatusStacks(handle, owner, status, actor, target, skill, hit,
                    units, random, runtime);
            return false;
        }

        private sealed class StatusIteration
        {
            public FightBattleStatusState Candidate;
        }

        private static bool TryIterationEntity(int key, FightBattleUnitState owner,
            FightBattleStatusState mainStatus, StatusIteration iteration, out object entity)
        {
            entity = key == 1 ? (object)owner : key == 2 ? mainStatus
                : key == 1072 ? iteration.Candidate : null;
            return key == 1 || key == 2 || key == 1072;
        }

        // User-confirmed offline semantics for the server-only FightHandleChangeStatusStack.
        // Keep the main status/blackboard distinct from the status currently being filtered.
        internal static bool ChangeStatusStacks(Dictionary<string, object> handle,
            FightBattleUnitState owner, FightBattleStatusState mainStatus,
            FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random, long? suppliedValue = null,
            Func<int, int, FightBattleStatusState> createFromSkill = null,
            IReadOnlyDictionary<FightBattleStatusState, int> removalSnapshot = null,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime = null)
        {
            // PetEntity and graph-local entity ports are not bound by the supported events.
            if (KeyId(handle, "TargetEntityKey") != 1 || owner == null) return false;
            long value = suppliedValue ?? Blackboard(mainStatus, Object(handle, "ChangeValueKey"), runtime);
            if (value < int.MinValue || value > int.MaxValue) return false;
            long change = value * Int(handle, "Multiplier", 1);
            if (change < int.MinValue || change > int.MaxValue) return false;
            int delta = (int)change;
            List<FightBattleStatusState> matches;
            if (!TryMatchStatuses(owner, Object(handle, "FilterCondition"), owner, mainStatus,
                actor, target, skill, hit, units, random, out matches, runtime)) return false;
            if (matches.Count == 0 && delta > 0 && Int(handle, "CreateStatusClassId") > 0)
            {
                if (catalog == null) return false;
                int instanceId = 1;
                if (owner.Statuses != null)
                    foreach (FightBattleStatusState existing in owner.Statuses)
                        if (existing != null && existing.InstanceId >= instanceId)
                            instanceId = checked(existing.InstanceId + 1);
                bool noSource = Bool(handle, "UseDefaultSourceInfoOnCreate");
                if (!noSource && mainStatus == null && createFromSkill == null) return false;
                FightBattleStatusState created = noSource
                    ? catalog.CreateStatus(Int(handle, "CreateStatusClassId"), owner.Id, 0, instanceId)
                    : createFromSkill != null
                    ? createFromSkill(Int(handle, "CreateStatusClassId"), instanceId)
                    : catalog.CreateStatus(Int(handle, "CreateStatusClassId"), owner.Id, mainStatus.CreatorId,
                        instanceId, mainStatus.RootSkillId, mainStatus.ApplyingSkillItemId,
                        mainStatus.SourceSkillRank, mainStatus.SourceSkillLevel, mainStatus.SourceSubRank,
                        mainStatus.SourceProperties, 0, mainStatus.ReplacementProperties,
                        mainStatus.BlessPropertyScales);
                if (!noSource && createFromSkill == null)
                {
                    created.Origin = FightBattleStatusOrigin.Resolve(mainStatus).Copy();
                    created.ReportSource = FightBattleStatusReportSource.Resolve(mainStatus).Copy();
                }
                // Initialize the requested layers before Apply emits status-start events.
                if (created.IsOpenStack) created.StackCount = created.MaxStackedCount > 0
                    ? Math.Min(delta, created.MaxStackedCount) : delta;
                FightStatusLifecycle.Apply(owner, created, -1, instanceId, random);
            }
            foreach (FightBattleStatusState candidate in matches)
            {
                // Ending/consumption callbacks can remove other entries from the snapshot.
                if (!owner.Statuses.Contains(candidate)) continue;
                int appliedDelta = delta;
                if (delta < 0 && removalSnapshot != null)
                {
                    if (!removalSnapshot.TryGetValue(candidate, out int layers)) continue;
                    appliedDelta = -(int)Math.Min(-(long)delta, Math.Max(0, layers));
                }
                int changed = FightStatusLifecycle.AddStack(owner, candidate, appliedDelta);
                if (changed < 0 && candidate.StackCount == 0) FightStatusLifecycle.End(owner, candidate);
                if (changed < 0 && Bool(handle, "InvokeConsumeEvent"))
                    FightStatusTriggerRuntime.NotifyStackConsumed(owner, candidate.ClassId,
                        -changed, catalog, random, skill);
            }
            return true;
        }

        private static bool AbsorbStatusStacks(Dictionary<string, object> handle,
            FightBattleUnitState owner, FightBattleStatusState mainStatus,
            FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime)
        {
            int port = KeyId(handle, "TargetEntityKey");
            FightBattleUnitState destination = port == 1 ? owner : port == 1022 && hit != null ? target : null;
            int outputKey = KeyId(handle, "OutputCountKey");
            if (destination == null || outputKey == 0) return false;
            List<FightBattleStatusState> matches;
            if (!TryMatchStatuses(destination, Object(handle, "FilterCondition"), owner, mainStatus,
                actor, target, skill, hit, units, random, out matches, runtime)) return false;
            long count = 0;
            foreach (FightBattleStatusState candidate in matches) count += Math.Max(0, candidate.StackCount);
            if (count > int.MaxValue) return false;
            // Snapshot every layer before end callbacks can change the remaining instances.
            SetBlackboard(mainStatus, Object(handle, "OutputCountKey"), runtime, count);
            if (Bool(handle, "RemoveStatus", true))
                foreach (FightBattleStatusState candidate in matches)
                    FightStatusLifecycle.End(destination, candidate);
            // The original configuration has no consumption-notification option.
            return true;
        }

        private static bool TryMatchStatuses(FightBattleUnitState destination,
            Dictionary<string, object> filter, FightBattleUnitState owner,
            FightBattleStatusState mainStatus, FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            out List<FightBattleStatusState> matches,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime)
        {
            matches = new List<FightBattleStatusState>();
            var iteration = new StatusIteration();
            bool supported, visited = false;
            foreach (FightBattleStatusState candidate in destination.Statuses == null
                ? new List<FightBattleStatusState>() : new List<FightBattleStatusState>(destination.Statuses))
            {
                if (candidate == null) continue;
                visited = true;
                iteration.Candidate = candidate;
                bool matched = MatchesFilter(filter, owner, mainStatus, actor, target, skill, hit,
                    units, random, out supported, iteration, runtime);
                if (!supported) return false;
                if (matched) matches.Add(candidate);
            }
            // An empty collection must not bypass an unsupported condition and allow creation.
            if (visited) return true;
            MatchesFilter(filter, owner, mainStatus, actor, target, skill, hit, units,
                random, out supported, iteration, runtime);
            return supported;
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
            // EFightBlackBoardKeyValue distinguishes MainStatus (2) and DamageEntity
            // (1023) from units. Graph-local and event entity ports need explicit bindings.
            // Never reinterpret an absent/unbound port as an actor, target or status owner.
            return KeyId(key, null) == 1 ? owner : null;
        }

        private static void RemoveLegacyRuntimeValues(FightBattleStatusState status, object graph)
        {
            if (status.LogicBlackboard == null || status.LogicBlackboard.Count == 0) return;
            var transient = new HashSet<int>();
            var persistent = new HashSet<int>();
            CollectPorts(graph, transient, persistent);
            status.LogicBlackboard.RemoveAll(value => value != null
                && transient.Contains(value.KeyId) && !persistent.Contains(value.KeyId));
        }

        private static void CollectPorts(object graph, HashSet<int> transient, HashSet<int> persistent)
        {
            if (graph is Dictionary<string, object> fields)
            {
                // Only explicit port metadata authorizes migration of old persisted values.
                if (fields.ContainsKey("IsPersistentBlackboard") && fields.ContainsKey("Id"))
                    (Bool(fields, "IsPersistentBlackboard") ? persistent : transient).Add(Int(fields, "Id"));
                foreach (object value in fields.Values) CollectPorts(value, transient, persistent);
            }
            else if (graph is List<object> values)
                foreach (object value in values) CollectPorts(value, transient, persistent);
        }

        private static FightBattleLogicBlackboardValue BlackboardValue(FightBattleStatusState status,
            Dictionary<string, object> port, Dictionary<int, FightBattleLogicBlackboardValue> runtime, bool create)
        {
            int key = KeyId(port, null);
            if (key == 0) return null;
            if (runtime != null && !Bool(port, "IsPersistentBlackboard"))
            {
                if (runtime.TryGetValue(key, out var value)) return value;
                if (!create) return null;
                return runtime[key] = new FightBattleLogicBlackboardValue { KeyId = key };
            }
            if (status == null) return null;
            if (status.LogicBlackboard != null)
                foreach (var value in status.LogicBlackboard)
                    if (value != null && value.KeyId == key) return value;
            if (!create) return null;
            if (status.LogicBlackboard == null) status.LogicBlackboard = new List<FightBattleLogicBlackboardValue>();
            var added = new FightBattleLogicBlackboardValue { KeyId = key };
            status.LogicBlackboard.Add(added);
            return added;
        }

        private static long Blackboard(FightBattleStatusState status, Dictionary<string, object> port,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime)
            => BlackboardValue(status, port, runtime, false)?.LongValue ?? 0L;

        private static void SetBlackboard(FightBattleStatusState status, Dictionary<string, object> port,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime, long value)
        {
            var current = BlackboardValue(status, port, runtime, true);
            if (current != null) current.LongValue = value;
        }

        private static double DoubleBlackboard(FightBattleStatusState status, Dictionary<string, object> port,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime, double fallback)
            => BlackboardValue(status, port, runtime, false)?.DoubleValue ?? fallback;

        private static void SetDoubleBlackboard(FightBattleStatusState status, Dictionary<string, object> port,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime, double value)
        {
            var current = BlackboardValue(status, port, runtime, true);
            if (current != null) current.DoubleValue = value;
        }

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
