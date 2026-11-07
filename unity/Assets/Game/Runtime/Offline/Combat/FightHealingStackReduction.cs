using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // Confirmed offline execution for 25131; other StackChange rules retain their own path.
    internal static class FightHealingStackReduction
    {
        public static void Apply(FightBattleUnitState owner, FightBattleStatusState source)
        {
            var component = source?.Components?.StackChange;
            if (source?.ClassId != 25131 || component == null || source.ForbiddenType != 0
                || component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount) return;
            component.TriggeredCount++;
            foreach (var rule in component.Changes)
            {
                if (rule == null || rule.StatusId != 24242) continue;
                foreach (var target in new List<FightBattleStatusState>(owner.Statuses))
                {
                    if (target == null || target.ClassId != rule.StatusId
                        || target.CreatorId != source.CreatorId || target.RootSkillId != source.RootSkillId
                        || !FightBattleStatusOrigin.Matches(FightBattleStatusOrigin.Resolve(target),
                            FightBattleStatusOrigin.Resolve(source))) continue;
                    FightStatusLifecycle.AddStack(target, rule.ChangeValue);
                    if (target.StackCount == 0) FightStatusLifecycle.End(owner, target);
                }
            }
        }
    }
}
