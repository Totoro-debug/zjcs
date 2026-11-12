using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        public FightBattleStatusReplayState StatusReplayDefinition(int classId)
            => ReadStatusReplay(RequiredEntity(classId));

        public void RestoreStatusReplayDefinition(FightBattleStatusState status)
        {
            var definition = StatusReplayDefinition(status.ClassId);
            // JsonUtility materializes null inline classes as default objects.
            // Only the original component grants replay; retain counters for real components.
            if (definition == null) status.SkillReplay = null;
            else if (status.SkillReplay == null) status.SkillReplay = definition;
        }

        public long[] SkillGameplayTags(int entityId)
        {
            return ReadGameplayTags(RequiredEntity(entityId));
        }

        public bool TryGetEntityGameplayTags(int entityId, out long[] tags)
        {
            tags = null;
            if (!entities.TryGetValue(entityId, out var components)) return false;
            var component = Component(components, "FightGameplayTagComponent");
            if (component == null || Component(component, "GameplayTag") == null) return false;
            tags = ReadGameplayTags(components);
            return true;
        }

        private static long[] ReadGameplayTags(Dictionary<string, object> components)
        {
            var component = Component(components, "FightGameplayTagComponent");
            var tags = component == null ? null : Component(component, "GameplayTag");
            var result = new long[4];
            if (tags != null)
                for (int i = 0; i < 4; i++) result[i] = Long(tags, "V" + i);
            return result;
        }

        private static FightBattleStatusReplayState ReadStatusReplay(Dictionary<string, object> components)
        {
            var component = Component(components, "FightStatusSkillReplayComponent");
            if (component == null) return null;
            var result = new FightBattleStatusReplayState
            {
                Rate = Float(component, "Rate", 1f),
                AddRateWhenNotHit = Float(component, "AddRateWhenNotHit", 0f),
                MaxCountEachRound = Int(component, "MaxCountEachRound"),
                IsCheckSkillLimitedTimes = Bool(component, "IsCheckSkillLimitedTimes"),
                FilterTypes = new List<int>(IntArray(component, "FilterTypes")),
                TriggerInGameStatusList = new List<int>(IntArray(component, "TriggerInGameStatusList"))
            };
            var filter = Component(component, "FilterCondition");
            if (filter == null) return result;
            var key = Component(filter, "EntityKey");
            if (StringField(filter, "_type") != "EntityGameplayTagFilter" || key == null || Int(key, "Id") != 1)
                throw new NotSupportedException("Unbound original status replay filter.");
            var query = Object(Required(filter, "Query"), "status replay tag query");
            var desc = Object(Required(query, "Desc"), "status replay tag clauses");
            int length = Int(query, "Length");
            if (length < 0 || length > 8) throw new FormatException("Original tag query exceeds eight clauses.");
            for (int i = 0; i < length; i++)
            {
                var source = Object(Required(desc, "V" + i), "status replay tag clause");
                var tags = Object(Required(source, "Tags"), "status replay tag mask");
                var clause = new FightGameplayTagClause { Type = Int(source, "Type") };
                if (clause.Type < 0 || clause.Type > 2) throw new NotSupportedException("Original tag query type.");
                for (int word = 0; word < 4; word++) clause.Tags[word] = Long(tags, "V" + word);
                result.TagQuery.Add(clause);
            }
            return result;
        }
    }
}
