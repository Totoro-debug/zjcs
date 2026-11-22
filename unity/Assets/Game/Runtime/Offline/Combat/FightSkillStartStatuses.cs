using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // Local execution of the original FightStatusSkillStartComponent.Status branch.
    internal sealed class FightSkillStartStatusRule
    {
        internal int MaxCount = -1;
        internal readonly List<int> ActionTypes = new List<int>();
        internal readonly List<int> Elements = new List<int>();
        internal readonly List<FightBattleStatusStateTrigger> Statuses = new List<FightBattleStatusStateTrigger>();
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private readonly Dictionary<int, FightSkillStartStatusRule> skillStartStatusRules =
            new Dictionary<int, FightSkillStartStatusRule>();

        internal FightSkillStartStatusRule SkillStartStatusRule(int classId)
        {
            if (skillStartStatusRules.TryGetValue(classId, out var cached)) return cached;
            var component = Component(RequiredEntity(classId), "FightStatusSkillStartComponent");
            var rule = ParseSkillStartStatusRule(component);
            skillStartStatusRules.Add(classId, rule);
            return rule;
        }

        private static FightSkillStartStatusRule ParseSkillStartStatusRule(Dictionary<string, object> component)
        {
            if (component == null || Int(component, "SkillStartSource") != 2
                || Int(component, "CD") != 0 || Bool(component, "ResetCDAtStart")
                || Bool(component, "RemoveWhenNotEnoughTimes")
                || ArrayCount(component, "StatusReplacePropertyValues") > 0
                || ArrayCount(component, "Cfgs") > 0 || ArrayCount(component, "Skills") > 0
                || ArrayCount(component, "Status") == 0 || ArrayCount(component, "Settings") == 0) return null;
            var rule = new FightSkillStartStatusRule { MaxCount = Int(component, "MaxCount", -1) };
            foreach (var raw in Array(component["Settings"], "skill start settings"))
            {
                var setting = Object(raw, "skill start condition");
                if (Int(setting, "TargetType") != 2) return null;
                string kind = StringField(setting, "_type");
                if (kind == "StatusSkillConditionActionTypeSetting" && Int(setting, "ConditionType") == 7)
                {
                    // The authored list is an OR; different condition settings are ANDed.
                    if (rule.ActionTypes.Count > 0 || ArrayCount(setting, "ActionTypes") == 0) return null;
                    rule.ActionTypes.AddRange(IntArray(setting, "ActionTypes"));
                }
                else if (kind == "StatusSkillConditionSkillElementSetting" && Int(setting, "ConditionType") == 8)
                    rule.Elements.Add(Int(setting, "ElementType"));
                else return null;
            }
            foreach (var raw in Array(component["Status"], "skill start statuses"))
            {
                var trigger = Object(raw, "skill start status");
                var propTarget = Component(trigger, "PropTarget");
                if (Int(trigger, "ApplyTarget") != 2 || Bool(trigger, "AffectedByProp")
                    || ArrayCount(trigger, "StatusReplacePropertyValues") > 0
                    || propTarget != null && (Bool(propTarget, "IsEnemy") || Bool(propTarget, "IncludeApplicator")
                        || Bool(propTarget, "Heighest") || Bool(propTarget, "SupportCustomFilter")
                        || Int(propTarget, "PropType") != 0 || ArrayCount(propTarget, "RoleType") > 0)) return null;
            }
            AddHpAccTriggerEntries(component, "Status", "StatusId", rule.Statuses, false);
            return rule;
        }
    }

    internal static class FightSkillStartStatuses
    {
        internal static bool Apply(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (actor == null || skill == null || catalog == null || actor.Statuses == null
                || !FightBattleTargeting.IsSelectable(actor)) return false;
            bool changed = false;
            foreach (var source in new List<FightBattleStatusState>(actor.Statuses))
            {
                if (source == null || !source.HasSkillStartTrigger || source.ForbiddenType != 0 || !actor.Statuses.Contains(source)
                    || !FightSkillTriggerPolicy.AllowsStatus(skill, source) || source.OwnerId != actor.Id) continue;
                var rule = catalog.SkillStartStatusRule(source.ClassId);
                if (rule == null || rule.MaxCount >= 0 && source.SkillStartTriggeredCount >= rule.MaxCount
                    || rule.ActionTypes.Count > 0 && !rule.ActionTypes.Contains(skill.ActionType)) continue;
                bool matches = true;
                foreach (int element in rule.Elements) if (element != skill.ElementType) matches = false;
                if (!matches) continue;
                bool invoked = false;
                foreach (var trigger in rule.Statuses)
                {
                    if (trigger.EntityId <= 0 || trigger.BasePercent <= 0f
                        || trigger.BasePercent < 1f && (random == null || random.Next() >= trigger.BasePercent)) continue;
                    int instance = 1;
                    foreach (var existing in actor.Statuses)
                        if (existing != null && existing.InstanceId >= instance) instance = existing.InstanceId + 1;
                    var template = catalog.CreateStatus(trigger.EntityId, actor.Id, source.CreatorId, instance,
                        source.RootSkillId, source.ApplyingSkillItemId, source.SourceSkillRank,
                        source.SourceSkillLevel, source.SourceSubRank, source.SourceProperties, 0,
                        source.ReplacementProperties, source.BlessPropertyScales);
                    template.Origin = FightBattleStatusOrigin.Resolve(source).Copy();
                    template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
                    changed |= FightStatusLifecycle.Apply(actor, template, -1, instance, random) != null;
                    invoked = true;
                }
                if (invoked) source.SkillStartTriggeredCount++;
            }
            return changed;
        }
    }
}
