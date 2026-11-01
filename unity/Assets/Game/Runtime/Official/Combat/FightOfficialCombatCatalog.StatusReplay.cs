using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        public FightBattleStatusReplayState StatusReplayDefinition(int classId)
            => ReadStatusReplay(RequiredEntity(classId), null);

        public long[] SkillGameplayTags(int entityId)
        {
            return ReadGameplayTags(RequiredEntity(entityId));
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

        // FightStatusBaseComponent.GetPropertyReplaceValue / FightStatusUtilsHandler.
        // Explicit status ReplaceProps retain their own type; actor properties are not substitutes.
        private static double ReplayParameter(Dictionary<string, object> component, string name,
            double fallback, bool floating, IList<FightStatusReplacementValue> values)
        {
            object raw;
            if (values == null || !component.TryGetValue("StatusReplacePropertyValues", out raw) || raw == null)
                return fallback;
            foreach (object entry in Array(raw, "status replay replacement properties"))
            {
                var replacement = Object(entry, "status replay replacement property");
                if (StringField(replacement, "PropertyName") != name) continue;
                int id = Int(replacement, "SpecialParamProp");
                foreach (var value in values)
                    if (value != null && value.PropertyId == id)
                    {
                        if (floating) return value.IsFloat ? (float)(value.RawValue / 10000d) : unchecked((int)value.RawValue);
                        return value.IsFloat ? (int)(value.RawValue / 10000d) : unchecked((int)value.RawValue);
                    }
                return fallback;
            }
            return fallback;
        }

        private static FightBattleStatusReplayState ReadStatusReplay(Dictionary<string, object> components,
            IList<FightStatusReplacementValue> replacementProperties)
        {
            var component = Component(components, "FightStatusSkillReplayComponent");
            if (component == null) return null;
            var result = new FightBattleStatusReplayState
            {
                Rate = (float)ReplayParameter(component, "Rate", Float(component, "Rate", 1f), true, replacementProperties),
                AddRateWhenNotHit = (float)ReplayParameter(component, "AddRateWhenNotHit",
                    Float(component, "AddRateWhenNotHit", 0f), true, replacementProperties),
                MaxCountEachRound = (int)ReplayParameter(component, "MaxCountEachRound",
                    Int(component, "MaxCountEachRound"), false, replacementProperties),
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
