using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    internal static partial class FightStatusLogicRuntime
    {
        private sealed class AfterDamageContext
        {
            public FightBattleUnitState Owner, Source, Target;
            public FightBattleStatusState Status;
            public FightBattleSkillDefinition Skill;
            public FightBattleHitDefinition Hit;
            public FightBattleDamageEvent Damage;
            public IReadOnlyList<FightBattleUnitState> Units;
        }

        private static bool SupportsAfterDamageFlow(Dictionary<string, object> flow, AfterDamageContext context)
        {
            var condition = Object(flow, "FilterCondition");
            var filters = Array(condition, "SubFilters").OfType<Dictionary<string, object>>().ToArray();
            var handles = Array(flow, "HandleCfgs").OfType<Dictionary<string, object>>().ToArray();
            if (context.Source == null || StringField(condition, "_type") != "LogicFilter"
                || Int(condition, "LogicType") != 0 || filters.Length != 3
                || Array(condition, "SubFilters").Count != 3 || handles.Length != 5
                || Array(flow, "HandleCfgs").Count != 5
                || StringField(filters[0], "_type") != "DamageResultTypeFilter"
                || StringField(filters[1], "_type") != "DamageSkillCanTriggerStatusFilter"
                || StringField(filters[2], "_type") != "LogicNotFilter"
                || StringField(Object(filters[2], "TargetFilter"), "_type") != "EntityHasActionTypeByEntityKeyFilter"
                || StringField(handles[0], "_type") != "FightHandleCfgBBEcId2EcEntityAndStore"
                || KeyId(handles[0], "ValueBBKey") != 1021 || KeyId(handles[0], "StorageBBKey") == 0
                || Bool(Object(handles[0], "StorageBBKey"), "IsPersistentBlackboard")
                || StringField(handles[1], "_type") != "FightHandleCfgFireTriggerStatus"
                || KeyId(handles[1], "StatusEntityKey") != 2
                || StringField(handles[2], "_type") != "FightHandleCfgTriggerSkills"
                || KeyId(handles[2], "SourceEntityKey") != KeyId(handles[0], "StorageBBKey")
                || KeyId(handles[2], "TargetEntityKey") != 1 || Bool(handles[2], "UseDamagePos")
                || StringField(handles[3], "_type") != "FightHandleCfgNewIntInBB"
                || StringField(handles[4], "_type") != "FightHandleCfgChangeStatusStack"
                || KeyId(handles[3], "BBKey") != KeyId(handles[4], "ChangeValueKey")
                || KeyId(handles[4], "TargetEntityKey") != 1 || Int(handles[4], "CreateStatusClassId") != 0
                || Bool(handles[4], "InvokeConsumeEvent") || Bool(handles[4], "UseDefaultSourceInfoOnCreate")
                || StringField(Object(handles[4], "FilterCondition"), "_type") != "IteratingAgentStatusClassIdFilter")
                return false;
            var configs = Array(handles[2], "TriggerSkillCfgs").OfType<Dictionary<string, object>>().ToArray();
            if (configs.Length == 0 || configs.Length != Array(handles[2], "TriggerSkillCfgs").Count) return false;
            return configs.All(cfg => SupportsAfterDamageSkill(cfg, context, context.Source, context.Owner));
        }

        private static bool SupportsAfterDamageSkill(Dictionary<string, object> cfg, AfterDamageContext context,
            FightBattleUnitState source, FightBattleUnitState target)
        {
            var custom = Object(cfg, "CustomDamageInfo");
            return !Bool(cfg, "UseAI") && !Bool(cfg, "AffectedByProp") && Int(cfg, "TargetPos") == 1
                && !Bool(cfg, "StackedCountToDamageFactor")
                && Array(cfg, "StatusReplacePropertyValues").Count == 0
                && Array(cfg, "TargetWithoutStatusIds").Count == 0
                && custom != null && custom.Values.All(value => LongValue(value) == 0)
                && AfterDamageSubject(Int(cfg, "Source"), context, source, target) != null
                && AfterDamageSubject(Int(cfg, "Target"), context, source, target) != null;
        }

        private static bool MatchAfterDamageFilter(Dictionary<string, object> filter,
            AfterDamageContext context, out bool matched, out bool supported)
        {
            string type = StringField(filter, "_type");
            matched = false; supported = true;
            if (type == "DamageResultTypeFilter")
            {
                supported = KeyId(filter, "DamageEntityKey") == 1023 && Int(filter, "ResultType") == 1;
                var damage = context.Damage;
                matched = supported && damage != null && !damage.IsCure && damage.HasDamageValue
                    && !damage.Dodge && !damage.Blinding && !damage.Immunity;
                // Buff graphs can use a zero-valued self hit to install the very
                // status being evaluated. Preserve all four opening charges.
                if (matched && context.Source == context.Owner && context.Target == context.Owner
                    && context.Skill?.SkillType == 2 && damage.Damage == 0 && damage.ShieldDamage == 0)
                    matched = false;
                return true;
            }
            if (type == "DamageSkillCanTriggerStatusFilter")
            {
                supported = KeyId(filter, "StatusEntityKey") == 2 && KeyId(filter, "SkillEntityIdKey") == 1025;
                matched = supported && context.Skill != null && context.Hit != null
                    && FightSkillTriggerPolicy.AllowsStatus(context.Skill, context.Status);
                return true;
            }
            if (type == "EntityHasActionTypeByEntityKeyFilter")
            {
                supported = KeyId(filter, "EntityKey") == 1 && Int(filter, "ActionType") == 3;
                matched = supported && (context.Owner.Hp <= 0 || context.Owner.Ended
                    || context.Owner.Statuses.Any(status => status != null && status.ActionType == 3));
                return true;
            }
            return false;
        }

        private static FightBattleUnitState AfterDamageEntity(Dictionary<string, object> key,
            AfterDamageContext context, Dictionary<int, FightBattleLogicBlackboardValue> runtime)
        {
            int id = KeyId(key, null);
            if (id == 1) return context.Owner;
            if (id == 1021) return context.Source;
            if (id == 1022) return context.Target;
            long stored = Blackboard(context.Status, key, runtime);
            return stored <= 0 ? null : context.Units.FirstOrDefault(unit => unit.Id == stored);
        }

        private static FightBattleUnitState AfterDamageSubject(int kind, AfterDamageContext context,
            FightBattleUnitState source, FightBattleUnitState target)
        {
            // Common.StatusTriggerTarget: TriggerSource, Applicator, Creator, TriggerTarget.
            if (kind == 1) return source;
            if (kind == 2) return context.Owner;
            if (kind == 3) return context.Units.FirstOrDefault(unit => unit.Id == context.Status.CreatorId);
            if (kind == 5) return target;
            return null;
        }

        private static bool ApplyAfterDamageHandle(Dictionary<string, object> handle,
            AfterDamageContext context, FightRandomStream random,
            Dictionary<int, FightBattleLogicBlackboardValue> runtime,
            List<FightBattleStatusSkillInvocation> output, out bool supported)
        {
            string type = StringField(handle, "_type");
            supported = true;
            if (type == "FightHandleCfgBBEcId2EcEntityAndStore")
            {
                var storage = Object(handle, "StorageBBKey");
                supported = KeyId(handle, "ValueBBKey") == 1021 && context.Source != null
                    && KeyId(storage, null) != 0 && !Bool(storage, "IsPersistentBlackboard");
                if (supported) SetBlackboard(context.Status, storage, runtime, context.Source.Id);
                return true;
            }
            if (type == "FightHandleCfgFireTriggerStatus")
            {
                // Apply records the successful flow once after its ordered handles.
                supported = KeyId(handle, "StatusEntityKey") == 2;
                return true;
            }
            if (type != "FightHandleCfgTriggerSkills") return false;
            var source = AfterDamageEntity(Object(handle, "SourceEntityKey"), context, runtime);
            var target = AfterDamageEntity(Object(handle, "TargetEntityKey"), context, runtime);
            if (source == null || target == null || Bool(handle, "UseDamagePos"))
            { supported = false; return true; }
            var configs = Array(handle, "TriggerSkillCfgs").OfType<Dictionary<string, object>>().ToArray();
            foreach (var cfg in configs)
            {
                if (!SupportsAfterDamageSkill(cfg, context, source, target))
                { supported = false; return true; }
            }
            foreach (var cfg in configs)
            {
                if (Int(cfg, "SkillId") <= 0 || !Roll(random, Float(cfg, "BasePercent", 1f))) continue;
                var caster = AfterDamageSubject(Int(cfg, "Source"), context, source, target);
                var destination = AfterDamageSubject(Int(cfg, "Target"), context, source, target);
                output.Add(new FightBattleStatusSkillInvocation {
                    Trigger = TriggerSkill(cfg, context.Status), SourceUnitId = caster.Id,
                    TargetUnitId = destination.Id, TargetPositionUnitId = destination.Id,
                    SourceRootSkillId = context.Status.RootSkillId, Delay = context.Damage.Delay,
                    DamageFactor = DoubleBlackboard(context.Status, Object(handle, "DamageFactorKey"), runtime, 1d)
                });
            }
            return true;
        }
    }
}
