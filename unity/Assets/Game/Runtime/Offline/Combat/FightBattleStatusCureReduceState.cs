using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusCureReduceState
    {
        public bool DefinitionResolved;
        public float Rate = 1f;
        public float ReduceCurePer;
        public bool IsCheckSkillTrigger;
        public int ExecuteOrder;
        public bool HasUnsupportedFilter;
        public List<FightGameplayTagClause> SourceSkillTagQuery = new List<FightGameplayTagClause>();
        public List<FightBattleStatusStateTrigger> StatusTriggers = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusCureReduceState Copy()
        {
            var copy = (FightBattleStatusCureReduceState)MemberwiseClone();
            copy.SourceSkillTagQuery = new List<FightGameplayTagClause>();
            if (SourceSkillTagQuery != null) foreach (var clause in SourceSkillTagQuery)
                copy.SourceSkillTagQuery.Add(clause == null ? null : new FightGameplayTagClause {
                    Type = clause.Type, Tags = clause.Tags == null ? null : (long[])clause.Tags.Clone() });
            copy.StatusTriggers = new List<FightBattleStatusStateTrigger>();
            if (StatusTriggers != null) foreach (var trigger in StatusTriggers)
                copy.StatusTriggers.Add(trigger?.Copy());
            return copy;
        }
    }
}
