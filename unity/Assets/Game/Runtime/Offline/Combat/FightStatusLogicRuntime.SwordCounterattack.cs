using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    internal static partial class FightStatusLogicRuntime
    {
        // Server hotfix implementations are absent. The user confirmed the
        // successful-hit gate, conditional random draw and applicator->attacker
        // mapping for original 10537/25017. Other status graphs keep their rules.
        private static List<FightBattleStatusSkillInvocation> ApplySwordCounterattack(
            FightBattleUnitState owner, FightBattleStatusState status, FightBattleUnitState attacker,
            FightBattleUnitState target, FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            FightBattleDamageEvent damage, List<object> triggers, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            var trigger = triggers.Count == 1 ? triggers[0] as Dictionary<string, object> : null;
            var flow = Object(trigger, "Flow");
            var condition = Object(flow, "FilterCondition");
            var filters = Array(condition, "SubFilters").OfType<Dictionary<string, object>>().ToArray();
            var handles = Array(flow, "HandleCfgs").OfType<Dictionary<string, object>>().ToArray();
            var notDead = filters.Length == 5 ? Object(filters[2], "TargetFilter") : null;
            var skills = handles.Length == 3 ? Array(handles[1], "TriggerSkillCfgs") : new List<object>();
            var cfg = skills.Count == 1 ? skills[0] as Dictionary<string, object> : null;
            var custom = Object(cfg, "CustomDamageInfo");
            // Preflight the authored graph before any probability/counter change.
            // Unsupported edits remain unresolved instead of executing a subset.
            bool supported = StringField(trigger, "_type") == "FightOnAfterBeDamageTriggerCfg"
                && StringField(condition, "_type") == "LogicFilter" && Int(condition, "LogicType") == 0
                && filters.Length == 5 && Array(condition, "SubFilters").Count == 5
                && StringField(filters[0], "_type") == "DamageResultTypeFilter"
                && Int(filters[0], "ResultType") == 1 && KeyId(filters[0], "DamageEntityKey") == 1023
                && StringField(filters[1], "_type") == "DamageSkillCanTriggerStatusFilter"
                && KeyId(filters[1], "StatusEntityKey") == 2 && KeyId(filters[1], "SkillEntityIdKey") == 1025
                && StringField(filters[2], "_type") == "LogicNotFilter"
                && StringField(notDead, "_type") == "EntityHasActionTypeByEntityKeyFilter"
                && KeyId(notDead, "EntityKey") == 1 && Int(notDead, "ActionType") == 3
                && StringField(filters[3], "_type") == "RangeByEcIdKeyFilter"
                && KeyId(filters[3], "CenterEntity") == 1 && KeyId(filters[3], "TargetEntityIdKey") == 1021
                && !Bool(filters[3], "UseWorldPosByCenterEntityTransform") && Bool(filters[3], "ConsiderTargetBodySize")
                && StringField(filters[4], "_type") == "RandomJudgeFilter"
                && handles.Length == 3 && Array(flow, "HandleCfgs").Count == 3
                && StringField(handles[0], "_type") == "FightHandleCfgBBEcId2EcEntityAndStore"
                && KeyId(handles[0], "ValueBBKey") == 1021 && KeyId(handles[0], "StorageBBKey") != 0
                && !Bool(Object(handles[0], "StorageBBKey"), "IsPersistentBlackboard")
                && StringField(handles[1], "_type") == "FightHandleCfgTriggerSkills"
                && KeyId(handles[1], "SourceEntityKey") == KeyId(handles[0], "StorageBBKey")
                && KeyId(handles[1], "TargetEntityKey") == 1 && !Bool(handles[1], "UseDamagePos")
                && Int(cfg, "SkillId") == 11210 && Int(cfg, "Source") == 2 && Int(cfg, "Target") == 1
                && Int(cfg, "TargetPos") == 1 && !Bool(cfg, "UseAI") && !Bool(cfg, "AffectedByProp")
                && !Bool(cfg, "StackedCountToDamageFactor")
                && Array(cfg, "StatusReplacePropertyValues").Count == 0 && Array(cfg, "TargetWithoutStatusIds").Count == 0
                && custom != null && custom.Values.All(value => LongValue(value) == 0)
                && StringField(handles[2], "_type") == "FightHandleCfgFireTriggerStatus"
                && KeyId(handles[2], "StatusEntityKey") == 2;
            if (!supported) { status.LogicRulesSupported = false; return result; }
            if (attacker == null || target != owner || skill == null || hit == null || damage == null
                || damage.IsCure || damage.Dodge || damage.Blinding || damage.Immunity
                || !damage.HasDamageValue || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                || owner.Hp <= 0 || owner.Statuses.Any(value => value != null && value.ActionType == 3)) return result;

            bool inRange = false;
            foreach (var raw in Array(filters[3], "Range"))
            {
                var point = raw as Dictionary<string, object>;
                if (point == null) { status.LogicRulesSupported = false; return result; }
                int x = owner.X + Int(point, "X"), y = owner.Y + Int(point, "Y");
                if (attacker.X == x && attacker.Y == y || attacker.BodyOffsets != null
                    && attacker.BodyOffsets.Any(body => attacker.X + body.X == x && attacker.Y + body.Y == y))
                    inRange = true;
            }
            if (!inRange) return result;
            // Original RandomJudgeFilter description uses <= Rate. The user
            // confirmed drawing only after every preceding deterministic gate.
            if (random == null || random.Next() > Float(filters[4], "Rate")) return result;
            var runtime = new Dictionary<int, FightBattleLogicBlackboardValue>();
            SetBlackboard(status, Object(handles[0], "StorageBBKey"), runtime, attacker.Id);
            if (Roll(random, Float(cfg, "BasePercent", 1f)))
            {
                result.Add(new FightBattleStatusSkillInvocation
                {
                    Trigger = TriggerSkill(cfg, status),
                    SourceUnitId = owner.Id, TargetUnitId = attacker.Id,
                    TargetPositionUnitId = attacker.Id,
                    SourceRootSkillId = status.RootSkillId, Delay = damage.Delay,
                    DamageFactor = DoubleBlackboard(status, Object(handles[1], "DamageFactorKey"), runtime, 1d)
                });
            }
            status.LogicTriggeredCount++;
            return result;
        }
    }
}
