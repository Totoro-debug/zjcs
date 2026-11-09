using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusCureReduceState ParseCureReduce(Dictionary<string, object> component)
        {
            if (component == null) return null;
            var result = new FightBattleStatusCureReduceState {
                DefinitionResolved = true,
                Rate = Float(component, "Rate", 1f),
                ReduceCurePer = Float(component, "ReduceCurePer", 0f),
                IsCheckSkillTrigger = Bool(component, "IsCheckSkillTrigger"),
                ExecuteOrder = Int(component, "ExcuteOrder")
            };
            result.HasUnsupportedFilter = !ReadSourceSkillTagFilter(Component(component, "FilterCondition"),
                result.SourceSkillTagQuery);
            AddTriggerStatuses(component, "StatusCfgs", result.StatusTriggers);
            foreach (var trigger in result.StatusTriggers)
                if (trigger == null || trigger.AffectedByProp
                    || trigger.ApplyTarget != 0 && trigger.ApplyTarget != 1 && trigger.ApplyTarget != 2
                        && trigger.ApplyTarget != 3 && trigger.ApplyTarget != 5)
                    result.HasUnsupportedFilter = true;
            return result;
        }

        internal void RestoreCureReduceDefinition(FightBattleStatusState status)
        {
            if (status == null || status.Components?.CureReduce?.DefinitionResolved == true
                || !entities.TryGetValue(status.ClassId, out var componentMap)
                || Component(componentMap, "FightStatusCureReduceComponent") == null) return;
            var resolved = ResolveStatusParameters(componentMap, status.ReplacementProperties);
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            status.Components.CureReduce = ParseCureReduce(Component(resolved, "FightStatusCureReduceComponent"));
        }
    }
}
