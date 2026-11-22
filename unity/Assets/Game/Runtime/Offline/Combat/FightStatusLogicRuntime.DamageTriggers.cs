using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    internal static partial class FightStatusLogicRuntime
    {
        private static bool SupportsDoDamageFlow(Dictionary<string, object> flow, AfterDamageContext context)
        {
            var condition = Object(flow, "FilterCondition");
            var filters = Array(condition, "SubFilters").OfType<Dictionary<string, object>>().ToArray();
            var handles = Array(flow, "HandleCfgs").OfType<Dictionary<string, object>>().ToArray();
            if (context.Damage?.HasDamagePosition != true || context.Catalog == null || context.SkillItems == null
                || StringField(condition, "_type") != "LogicFilter" || Int(condition, "LogicType") != 0
                || filters.Length != 4 || Array(condition, "SubFilters").Count != 4
                || handles.Length != 2 || Array(flow, "HandleCfgs").Count != 2
                || StringField(filters[0], "_type") != "DamageTypeFilter"
                || StringField(filters[1], "_type") != "BBLongLessFilter"
                || StringField(filters[2], "_type") != "SkillOverrideParentActiveByEcIdFilter"
                || StringField(filters[3], "_type") != "LogicNotFilter"
                || StringField(Object(filters[3], "TargetFilter"), "_type") != "SameFactionByEcIdKeyFilter"
                || StringField(handles[0], "_type") != "FightHandleCfgBBEcId2EcEntityAndStore"
                || KeyId(handles[0], "ValueBBKey") != 1022 || KeyId(handles[0], "StorageBBKey") == 0
                || Bool(Object(handles[0], "StorageBBKey"), "IsPersistentBlackboard")
                || StringField(handles[1], "_type") != "FightHandleCfgTriggerSkills"
                || KeyId(handles[1], "SourceEntityKey") != 1
                || KeyId(handles[1], "TargetEntityKey") != KeyId(handles[0], "StorageBBKey")
                || !Bool(handles[1], "UseDamagePos") || KeyId(handles[1], "DamagePosEntityKey") != 1023)
                return false;
            var configs = Array(handles[1], "TriggerSkillCfgs").OfType<Dictionary<string, object>>().ToArray();
            return configs.Length > 0 && configs.Length == Array(handles[1], "TriggerSkillCfgs").Count
                && configs.All(SupportsDamagePositionSkill);
        }

        private static bool SupportsDamagePositionSkill(Dictionary<string, object> cfg)
        {
            var custom = Object(cfg, "CustomDamageInfo");
            var prop = Object(cfg, "PropTarget");
            return Int(cfg, "SkillId") > 0 && Int(cfg, "Source") == 2 && Int(cfg, "Target") == 5
                && Int(cfg, "TargetPos") == 2 && !Bool(cfg, "UseAI") && !Bool(cfg, "AffectedByProp")
                && !Bool(cfg, "StackedCountToDamageFactor") && (prop == null || Int(prop, "PropType") == 0)
                && Array(cfg, "StatusReplacePropertyValues").Count == 0
                && Array(cfg, "TargetWithoutStatusIds").Count == 0
                && custom != null && custom.Values.All(value => LongValue(value) == 0);
        }

        private static bool MatchDoDamageFilter(Dictionary<string, object> filter, AfterDamageContext context,
            out bool matched, out bool supported)
        {
            matched = false; supported = true;
            string type = StringField(filter, "_type");
            var damage = context.Damage;
            if (type == "DamageTypeFilter")
            {
                string[] flags = { "IsCrit", "IsBlock", "IsDodge", "IsBlinding", "IsImmunity", "IsBreakResilience", "IsTrueDamage" };
                bool[] values = { damage.Critical, damage.Block, damage.Dodge, damage.Blinding, damage.Immunity,
                    damage.BreakResilience, damage.TrueDamage };
                // A single selected flag has unambiguous official semantics.
                // Combined flag modes require their own complete implementation.
                supported = flags.Count(flag => Bool(filter, flag)) == 1;
                matched = supported && flags.Select((flag, i) => Bool(filter, flag) && values[i]).Any(value => value);
                return true;
            }
            if (type == "BBLongLessFilter")
            {
                supported = KeyId(filter, "LhsBBKey") == 1029 && damage.HasDamageValue;
                matched = supported && damage.DamageValue < Long(filter, "Value");
                return true;
            }
            if (type == "SkillOverrideParentActiveByEcIdFilter")
            {
                supported = KeyId(filter, "SkillEntityIdKey") == 1025 && context.Skill != null
                    && context.Hit != null && context.Catalog != null && context.SkillItems != null;
                matched = supported && FightSkillTriggerPolicy.AllowsStatus(context.Skill, context.Status)
                    && context.Catalog.IsReportParentActive(context.Skill.EntityId, context.SkillItems) == Bool(filter, "IsActiveSkill");
                return true;
            }
            if (type == "SameFactionByEcIdKeyFilter")
            {
                supported = KeyId(filter, "CheckEntityIdKey") == 1022 && KeyId(filter, "MainEntityKey") == 1
                    && context.Target != null;
                matched = supported && context.Target.Side == context.Owner.Side;
                return true;
            }
            return false;
        }

        private static bool ApplyDoDamageHandle(Dictionary<string, object> handle, AfterDamageContext context,
            FightRandomStream random, Dictionary<int, FightBattleLogicBlackboardValue> runtime,
            List<FightBattleStatusSkillInvocation> output, out bool supported)
        {
            supported = true;
            string type = StringField(handle, "_type");
            if (type == "FightHandleCfgBBEcId2EcEntityAndStore")
            {
                var entity = AfterDamageEntity(Object(handle, "ValueBBKey"), context, runtime);
                supported = entity != null;
                if (supported) SetBlackboard(context.Status, Object(handle, "StorageBBKey"), runtime, entity.Id);
                return true;
            }
            if (type != "FightHandleCfgTriggerSkills") return false;
            var source = AfterDamageEntity(Object(handle, "SourceEntityKey"), context, runtime);
            var target = AfterDamageEntity(Object(handle, "TargetEntityKey"), context, runtime);
            if (source == null || target == null) { supported = false; return true; }
            foreach (var cfg in Array(handle, "TriggerSkillCfgs").OfType<Dictionary<string, object>>())
            {
                if (!Roll(random, Float(cfg, "BasePercent", 1f))) continue;
                output.Add(new FightBattleStatusSkillInvocation {
                    Trigger = TriggerSkill(cfg, context.Status), SourceUnitId = context.Owner.Id,
                    TargetUnitId = target.Id, TargetPositionUnitId = target.Id, AllowDeadTarget = true,
                    SourceRootSkillId = context.Status.RootSkillId, Delay = context.Damage.Delay,
                    DamageFactor = DoubleBlackboard(context.Status, Object(handle, "DamageFactorKey"), runtime, 1d),
                    HitOrigin = new FightGridPoint(context.Damage.DamageX, context.Damage.DamageY),
                    HitFacing = source.Direction, HitFlipX = source.FlipX
                });
            }
            return true;
        }
    }
}
