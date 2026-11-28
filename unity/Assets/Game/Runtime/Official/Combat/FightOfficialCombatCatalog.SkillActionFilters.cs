using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // Original SkillActionTypeFilter and LogicFilterType (And=0, Or=1).
        // Resolve from the verified graph so legacy checkpoints use the same filter.
        internal bool MatchesHitSkillActionFilter(int statusId, FightBattleSkillDefinition skill)
        {
            if (!entities.TryGetValue(statusId, out var entity)) return true;
            var component = Component(entity, "FightStatusHitSkillComponent");
            if (component == null) return true;
            var filter = Component(component, "FilterCondition");
            return filter == null || skill != null
                && TryMatchSkillActionFilter(filter, skill.ActionType, out bool matches) && matches;
        }

        private static bool TryMatchSkillActionFilter(Dictionary<string, object> filter,
            int actionType, out bool matches)
        {
            matches = false;
            if (filter == null) return false;
            string type = StringField(filter, "_type");
            if (type == "SkillActionTypeFilter")
            {
                matches = Int(filter, "ActionType") == actionType;
                return true;
            }
            if (type != "LogicFilter") return false;
            int logic = Int(filter, "LogicType");
            if (logic != 0 && logic != 1) return false;
            matches = logic == 0;
            foreach (object raw in FieldArray(filter, "SubFilters"))
            {
                if (!TryMatchSkillActionFilter(Object(raw, "skill action filter"), actionType,
                    out bool child)) return false;
                matches = logic == 0 ? matches && child : matches || child;
            }
            return true;
        }
    }
}
