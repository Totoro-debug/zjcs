using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    internal static partial class FightStatusLogicRuntime
    {
        // The APK contains the authored graph and shared formula, but no server
        // handlers. This adapter implements the confirmed 10567/25035 mapping.
        private static List<FightBattleStatusSkillInvocation> ApplyReflectionArmor(
            FightBattleUnitState owner, FightBattleStatusState status, FightBattleUnitState attacker,
            FightBattleUnitState target, FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            FightBattleDamageEvent damage, List<object> triggers, FightDamageFormula formula)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            var trigger = triggers.Count == 1 ? triggers[0] as Dictionary<string, object> : null;
            var flow = Object(trigger, "Flow");
            var condition = Object(flow, "FilterCondition");
            var filters = Array(condition, "SubFilters").OfType<Dictionary<string, object>>().ToArray();
            var handles = Array(flow, "HandleCfgs").OfType<Dictionary<string, object>>().ToArray();
            var nonzero = filters.Length == 5 ? Object(filters[2], "TargetFilter") : null;
            var notDead = filters.Length == 5 ? Object(filters[4], "TargetFilter") : null;
            var skills = handles.Length == 3 ? Array(handles[2], "TriggerSkillCfgs") : new List<object>();
            var cfg = skills.Count == 1 ? skills[0] as Dictionary<string, object> : null;
            var custom = Object(cfg, "CustomDamageInfo");
            // Validate the entire supported graph before changing counters or
            // evaluating its formula. Unknown authored changes stay unresolved.
            bool supported = StringField(trigger, "_type") == "FightOnAfterBeDamageTriggerCfg"
                && StringField(condition, "_type") == "LogicFilter" && Int(condition, "LogicType") == 0
                && filters.Length == 5 && Array(condition, "SubFilters").Count == 5
                && StringField(filters[0], "_type") == "DamageResultTypeFilter"
                && Int(filters[0], "ResultType") == 1 && KeyId(filters[0], "DamageEntityKey") == 1023
                && StringField(filters[1], "_type") == "DamageShieldValueGreaterThanZeroFilter"
                && KeyId(filters[1], "DamageShieldValueKey") == 1030
                && StringField(filters[2], "_type") == "LogicNotFilter"
                && StringField(nonzero, "_type") == "DamageResultValueTypeCompareFilter"
                && Int(nonzero, "DamageValueType") == 1 && Long(nonzero, "Value") == 0
                && Int(nonzero, "Relation") == 0 && KeyId(nonzero, "DamageEntityKey") == 1023
                && StringField(filters[3], "_type") == "DamageSkillCanTriggerStatusFilter"
                && KeyId(filters[3], "StatusEntityKey") == 2 && KeyId(filters[3], "SkillEntityIdKey") == 1025
                && StringField(filters[4], "_type") == "LogicNotFilter"
                && StringField(notDead, "_type") == "EntityHasActionTypeByEntityKeyFilter"
                && KeyId(notDead, "EntityKey") == 1 && Int(notDead, "ActionType") == 3
                && handles.Length == 3 && Array(flow, "HandleCfgs").Count == 3
                && StringField(handles[0], "_type") == "FightHandleCfgBBEcId2EcEntityAndStore"
                && KeyId(handles[0], "ValueBBKey") == 1021 && KeyId(handles[0], "StorageBBKey") != 0
                && !Bool(Object(handles[0], "StorageBBKey"), "IsPersistentBlackboard")
                && StringField(handles[1], "_type") == "FightHandleCfgStatusDamageByDamageResultValueAndStore"
                && Int(handles[1], "DamageResultValueType") == 1 && KeyId(handles[1], "DamageEntityKey") == 1023
                && KeyId(handles[1], "StatusEntityKey") == 2
                && Int(handles[1], "DamagePropType") == 233 && Int(handles[1], "FixedDamagePropType") == 238
                && Long(handles[1], "MaxValue") == 0 && KeyId(handles[1], "CustomDamageKey") != 0
                && KeyId(handles[1], "CustomDamageKey") != KeyId(handles[0], "StorageBBKey")
                && !Bool(Object(handles[1], "CustomDamageKey"), "IsPersistentBlackboard")
                && StringField(handles[2], "_type") == "FightHandleCfgTriggerSkillWithCustomDamageByStatus"
                && KeyId(handles[2], "SourceEntityKey") == KeyId(handles[0], "StorageBBKey")
                && KeyId(handles[2], "TargetEntityKey") == 1
                && KeyId(handles[2], "CustomDamageKey") == KeyId(handles[1], "CustomDamageKey")
                && Int(cfg, "SkillId") == 11480 && Int(cfg, "Source") == 2 && Int(cfg, "Target") == 1
                && Int(cfg, "TargetPos") == 1 && !Bool(cfg, "UseAI") && !Bool(cfg, "AffectedByProp")
                && Float(cfg, "BasePercent") == 1f && !Bool(cfg, "StackedCountToDamageFactor")
                && Array(cfg, "StatusReplacePropertyValues").Count == 0 && Array(cfg, "TargetWithoutStatusIds").Count == 0
                && custom != null && custom.Values.All(value => LongValue(value) == 0);
            if (!supported || formula == null) { status.LogicRulesSupported = false; return result; }
            if (attacker == null || target != owner || skill == null || hit == null || damage == null
                || damage.IsCure || damage.Dodge || damage.Blinding || damage.Immunity
                || !damage.HasDamageValue || damage.ShieldDamage <= 0
                || damage.ShieldAddFinalDamageValue == 0 || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                || owner.Hp <= 0 || owner.Statuses.Any(value => value != null && value.ActionType == 3)) return result;

            var props = new Dictionary<int, long>();
            if (status.StatusProperties != null)
                foreach (var prop in status.StatusProperties)
                    if (prop != null) props[prop.Id] = prop.Value;
            long signedDamage = formula.CalculateStatusDamage(props, Int(handles[1], "DamagePropType"),
                Int(handles[1], "FixedDamagePropType"), damage.ShieldAddFinalDamageValue, Long(handles[1], "MaxValue"));
            result.Add(new FightBattleStatusSkillInvocation
            {
                Trigger = TriggerSkill(cfg, status).WithSource(status),
                SourceUnitId = owner.Id, TargetUnitId = attacker.Id, TargetPositionUnitId = attacker.Id,
                SourceRootSkillId = status.RootSkillId, Delay = damage.Delay,
                // StatusDamage returns signed HP change. The normal damage
                // formula's custom base expects the corresponding magnitude.
                HasCustomDamage = true, CustomDamage = Math.Abs(signedDamage)
            });
            status.LogicTriggeredCount++;
            return result;
        }
    }
}
