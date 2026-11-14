using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    internal static partial class FightStatusLogicRuntime
    {
        // The server handler is absent. The user confirmed successful blocked
        // hits (including full shield absorption) and holder -> attacker mapping.
        private static List<FightBattleStatusSkillInvocation> ApplyVectorReflection(
            FightBattleUnitState owner, FightBattleStatusState status, FightBattleUnitState attacker,
            FightBattleUnitState target, FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            FightBattleDamageEvent damage, List<object> triggers)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            var trigger = triggers.Count == 1 ? triggers[0] as Dictionary<string, object> : null;
            var flow = Object(trigger, "Flow");
            var condition = Object(flow, "FilterCondition");
            var filters = Array(condition, "SubFilters").OfType<Dictionary<string, object>>().ToArray();
            var handles = Array(flow, "HandleCfgs").OfType<Dictionary<string, object>>().ToArray();
            var notDead = filters.Length == 4 ? Object(filters[2], "TargetFilter") : null;
            var enemy = filters.Length == 4 ? Object(filters[3], "TargetFilter") : null;
            var skills = handles.Length == 3 ? Array(handles[1], "TriggerSkillCfgs") : new List<object>();
            var cfg = skills.Count == 1 ? skills[0] as Dictionary<string, object> : null;
            var custom = Object(cfg, "CustomDamageInfo");
            bool supported = StringField(trigger, "_type") == "FightOnAfterBeDamageTriggerCfg"
                && StringField(condition, "_type") == "LogicFilter" && Int(condition, "LogicType") == 0
                && filters.Length == 4 && Array(condition, "SubFilters").Count == 4
                && StringField(filters[0], "_type") == "DamageTypeFilter" && Bool(filters[0], "IsBlock")
                && !Bool(filters[0], "IsCrit") && !Bool(filters[0], "IsDodge")
                && !Bool(filters[0], "IsBlinding") && !Bool(filters[0], "IsImmunity")
                && !Bool(filters[0], "IsBreakResilience") && !Bool(filters[0], "IsTrueDamage")
                && StringField(filters[1], "_type") == "DamageSkillCanTriggerStatusFilter"
                && KeyId(filters[1], "StatusEntityKey") == 2 && KeyId(filters[1], "SkillEntityIdKey") == 1025
                && StringField(filters[2], "_type") == "LogicNotFilter"
                && StringField(notDead, "_type") == "EntityHasActionTypeByEntityKeyFilter"
                && KeyId(notDead, "EntityKey") == 1 && Int(notDead, "ActionType") == 3
                && StringField(filters[3], "_type") == "LogicNotFilter"
                && StringField(enemy, "_type") == "SameFactionByEcIdKeyFilter"
                && KeyId(enemy, "CheckEntityIdKey") == 1021 && KeyId(enemy, "MainEntityKey") == 1
                && handles.Length == 3 && Array(flow, "HandleCfgs").Count == 3
                && StringField(handles[0], "_type") == "FightHandleCfgBBEcId2EcEntityAndStore"
                && KeyId(handles[0], "ValueBBKey") == 1021 && KeyId(handles[0], "StorageBBKey") != 0
                && !Bool(Object(handles[0], "StorageBBKey"), "IsPersistentBlackboard")
                && StringField(handles[1], "_type") == "FightHandleCfgTriggerSkills"
                && KeyId(handles[1], "SourceEntityKey") == KeyId(handles[0], "StorageBBKey")
                && KeyId(handles[1], "TargetEntityKey") == 1 && !Bool(handles[1], "UseDamagePos")
                && Int(cfg, "SkillId") == 11230 && Int(cfg, "Source") == 2 && Int(cfg, "Target") == 1
                && Int(cfg, "TargetPos") == 1 && !Bool(cfg, "UseAI") && !Bool(cfg, "AffectedByProp")
                && Float(cfg, "BasePercent") == 1f && !Bool(cfg, "StackedCountToDamageFactor")
                && Array(cfg, "StatusReplacePropertyValues").Count == 0 && Array(cfg, "TargetWithoutStatusIds").Count == 0
                && custom != null && custom.Values.All(value => LongValue(value) == 0)
                && StringField(handles[2], "_type") == "FightHandleCfgFireTriggerStatus"
                && KeyId(handles[2], "StatusEntityKey") == 2;
            if (!supported) { status.LogicRulesSupported = false; return result; }
            if (attacker == null || target != owner || attacker.Side == owner.Side
                || skill == null || hit == null || damage == null || damage.IsCure
                || !damage.Block || damage.Dodge || damage.Blinding || damage.Immunity
                || !damage.HasDamageValue || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                || owner.Hp <= 0 || owner.Statuses.Any(value => value != null && value.ActionType == 3)) return result;

            var runtime = new Dictionary<int, FightBattleLogicBlackboardValue>();
            SetBlackboard(status, Object(handles[0], "StorageBBKey"), runtime, attacker.Id);
            result.Add(new FightBattleStatusSkillInvocation
            {
                Trigger = TriggerSkill(cfg, status).WithSource(status),
                SourceUnitId = owner.Id, TargetUnitId = attacker.Id,
                TargetPositionUnitId = attacker.Id, SourceRootSkillId = status.RootSkillId,
                Delay = damage.Delay,
                DamageFactor = DoubleBlackboard(status, Object(handles[1], "DamageFactorKey"), runtime, 1d)
            });
            status.LogicTriggeredCount++;
            return result;
        }
    }
}
