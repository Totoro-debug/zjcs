using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    internal sealed class Map13OfflinePlayerConditionEvaluator
    {
        private readonly Dictionary<int, Map13RuntimePlayerConditionData> conditions;
        private readonly Func<int, long> itemCount;
        private readonly Func<string, int> storyCount;
        private readonly Func<string, int> storyStatus;
        private readonly Func<int, int> stageWinCount;
        private readonly Func<int, int, int> regionProgress;
        private readonly Func<string, bool> completedTag;

        public Map13OfflinePlayerConditionEvaluator(Map13RuntimePlayerCondition[] source,
            Func<int, long> itemCount, Func<string, int> storyCount, Func<string, int> storyStatus,
            Func<int, int> stageWinCount, Func<int, int, int> regionProgress, Func<string, bool> completedTag)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            conditions = source.ToDictionary(value => value.ClassId, value => value.Condition);
            this.itemCount = itemCount ?? throw new ArgumentNullException(nameof(itemCount));
            this.storyCount = storyCount ?? throw new ArgumentNullException(nameof(storyCount));
            this.storyStatus = storyStatus ?? throw new ArgumentNullException(nameof(storyStatus));
            this.stageWinCount = stageWinCount ?? throw new ArgumentNullException(nameof(stageWinCount));
            this.regionProgress = regionProgress ?? throw new ArgumentNullException(nameof(regionProgress));
            this.completedTag = completedTag ?? throw new ArgumentNullException(nameof(completedTag));
        }

        public bool Evaluate(string type, Map13RuntimePlayerConditionData parameters, int[] compositeIds, int not)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new InvalidDataException("Official Map13 player condition node is malformed.");
            bool matched;
            if (type == "CompositeOr")
            {
                matched = compositeIds != null && compositeIds.Length > 0
                    && compositeIds.Any(id => EvaluateCondition(Find(id), 0));
            }
            else matched = EvaluateDirect(type + "Condition", parameters);
            return not == 0 ? matched : !matched;
        }

        private bool EvaluateCondition(Map13RuntimePlayerConditionData value, int depth)
        {
            if (value == null || depth > 32)
                throw new InvalidDataException("Official Map13 player condition graph is invalid.");
            if (value._type == "CompositeOrCondition")
                return (value.Ids ?? Array.Empty<int>()).Any(id => EvaluateCondition(Find(id), depth + 1));
            return EvaluateDirect(value._type, value);
        }

        private bool EvaluateDirect(string type, Map13RuntimePlayerConditionData value)
        {
            if (value == null) throw new InvalidDataException("Official Map13 player condition parameters are malformed: " + type);
            switch (type)
            {
                case "HasItemCondition": return Compare(itemCount(value.ItemId), value.Op, value.Count);
                case "StoryCountCondition": return Compare(storyCount(value.StoryKey), value.Op, value.Count);
                case "StoryStatusCondition": return storyStatus(value.StoryKey) == ParseStoryStatus(value.Status);
                case "StagePassCondition": return Compare(stageWinCount(value.StageId), value.Op, 1);
                case "WorldMapProgressCondition":
                    if (value.MapClassId != 13 || value.Region < 0)
                        throw new InvalidDataException("Unsupported official Map13 world-progress condition target.");
                    return Compare(regionProgress(value.MapClassId, value.Region), ">=", value.Progress);
                case "MapLogicEntityEndByTagCondition": return completedTag(value.Tag);
                default: throw new InvalidDataException("Unsupported official Map13 player condition: " + type);
            }
        }

        private Map13RuntimePlayerConditionData Find(int id)
        {
            if (id <= 0 || !conditions.TryGetValue(id, out Map13RuntimePlayerConditionData value))
                throw new InvalidDataException("Official Map13 player condition is not loaded: " + id);
            return value;
        }

        private static int ParseStoryStatus(string value)
        {
            if (int.TryParse(value, out int status) && status >= 0 && status <= 2) return status;
            switch (value)
            {
                case "None": return 0;
                case "Playing": return 1;
                case "End": return 2;
                default: throw new InvalidDataException("Unsupported official story status: " + value);
            }
        }

        private static bool Compare(long left, string operation, long right)
        {
            switch (operation)
            {
                case "==": return left == right;
                case "!=": return left != right;
                case ">": return left > right;
                case "<": return left < right;
                case ">=": return left >= right;
                case "<=": return left <= right;
                default: throw new InvalidDataException("Unsupported official condition comparison: " + operation);
            }
        }
    }
}
