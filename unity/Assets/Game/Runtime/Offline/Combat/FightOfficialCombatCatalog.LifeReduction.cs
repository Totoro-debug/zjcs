using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusLifeReductionState ParseLifeReduction(Dictionary<string, object> component)
        {
            if (component == null) return null;
            var result = new FightBattleStatusLifeReductionState { ReduceRoundCount = Int(component, "ReduceRoundCount") };
            result.HasUnsupportedFilter = !ReadSourceSkillTagFilter(Component(component, "FilterCondition"), result.TargetTagQuery);
            return result;
        }

        internal void RestoreLifeReductionDefinition(FightBattleUnitState owner, FightBattleStatusState status,
            bool initialize)
        {
            if (!entities.TryGetValue(status.ClassId, out var components)) return;
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            if (status.Components.GameplayTags == null) status.Components.GameplayTags = ReadGameplayTags(components);
            if (status.Components.LifeReduction != null) return;
            var resolved = ResolveStatusParameters(components, status.ReplacementProperties);
            status.Components.LifeReduction = ParseLifeReduction(Component(resolved, "FightStatusReduceStatusLifeComponent"));
            if (!initialize && status.Components.LifeReduction != null)
                foreach (var target in owner.Statuses)
                    if (target != null) status.Components.LifeReduction.ReducedStatusKeys.Add(FightStatusLifeReduction.StatusKey(owner, target));
        }
    }
}
