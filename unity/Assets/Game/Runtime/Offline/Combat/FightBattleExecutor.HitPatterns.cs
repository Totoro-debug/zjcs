using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private IReadOnlyList<FightHitOccurrence> PlanHitPattern(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            List<FightBattleUnitState> targets, FightBattleUnitState primary,
            FightBattleSkillCastPresentation cast, bool selectedByRandomParent = false)
        {
            return FightBattleHitPatterns.For(hit).Plan(new FightHitPatternContext {
                Source = actor, Units = units, Skill = skill, Hit = hit, Targets = targets,
                Primary = primary, Cast = cast, Random = random,
                SelectedByRandomParent = selectedByRandomParent
            });
        }
    }
}
