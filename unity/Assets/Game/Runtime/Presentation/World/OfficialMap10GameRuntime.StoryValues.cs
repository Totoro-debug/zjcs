using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private Dictionary<int, string> dialogueOverrides;

        [Serializable] private sealed class StoryValueFields
        {
            public int ItemId, RequireCount, ActionType, GoByNodeId, GotoId, DialogId, Order;
            public string GoTo;
            public StoryItemRequirement[] ItemConfigs;
            public StorySerializableValue[] Params;
        }
        [Serializable] private sealed class StoryItemRequirement { public int ItemId; public long ItemCount; }
        [Serializable] private sealed class StoryDialogFields { public int DialogId, Order; }
        [Serializable] private sealed class StoryBooleanFields { public int Input, MapClassId, RegionId; }
        [Serializable] private sealed class StorySerializableValue
        {
            public int mValueType, mInt;
            public long mLong;
            public float mFloat;
            public double mDouble;
            public int mBool;
            public string mString;
        }

        private static StoryValueFields StoryFields(Map10RuntimeStoryNode node) =>
            JsonUtility.FromJson<StoryValueFields>(node.sourceJson);

        private long StoryItemCount(int itemId) => progressionSave.Save.Player.Inventory
            .FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;

        private bool EvaluateStoryBoolean(Map10RuntimeStory story, Map10RuntimeStoryNode input, int depth = 0)
        {
            if (input == null || depth > 64) throw new InvalidDataException("Invalid official story condition graph.");
            switch (input.type)
            {
                case "AndNode":
                    Map10RuntimeStoryConnection[] connections = input.ports?.FirstOrDefault(value => value.name == "Input")?.connections;
                    return connections == null || connections.Length == 0
                        ? JsonUtility.FromJson<StoryBooleanFields>(input.sourceJson).Input != 0
                        : connections.All(value => EvaluateStoryBoolean(story, FindStoryNode(story, value.nodeId), depth + 1));
                case "NotNode":
                    int argument = StoryNext(input, "Input");
                    return !(argument > 0 ? EvaluateStoryBoolean(story, FindStoryNode(story, argument), depth + 1)
                        : JsonUtility.FromJson<StoryBooleanFields>(input.sourceJson).Input != 0);
                case "IsStoryStateNode":
                    return progressionSave != null && progressionSave.StoryStatus(input.storyName) == input.storyStatus;
                case "IsInTargetMapNode":
                    StoryBooleanFields targetMap = JsonUtility.FromJson<StoryBooleanFields>(input.sourceJson);
                    if (session == null || avatar == null || mapClassId != targetMap.MapClassId) return false;
                    if (targetMap.RegionId == -1) return true;
                    return regionConfigs.TryGetValue(targetMap.RegionId, out Map10RuntimeRegionConfig region)
                        && Zjcs.Offline.World.OfficialMapNavigationView.TryGetCell(session.Definition.Navigation,
                            currentGrid.X, currentGrid.Y, out int area, out int ground)
                        && region.Areas.Contains(area);
                case "QueryPlayerContainsItemNode":
                    return StoryFields(input).ItemConfigs.All(value => StoryItemCount(value.ItemId) >= value.ItemCount);
                case "CanGetFixedRewardNode": return CanGetFixedStoryReward(input.fixedRewardId);
                case "CheckMapEntityStateNode": return CheckStoryMapEntityState(input);
                case "IsPlayerHasFollowerIdsNode": return false;
                case "CompareNode":
                    Map10RuntimeStoryNode origin = FindStoryNode(story, StoryNext(input, "Origin"));
                    long current = Convert.ToInt64(EvaluateStoryValue(story, origin, depth + 1), CultureInfo.InvariantCulture);
                    long target = input.compareTargetInt;
                    switch (input.compareOption)
                    {
                        case 0: return current == target;
                        case 1: return current != target;
                        case 2: return current > target;
                        case 3: return current < target;
                        case 4: return current >= target;
                        case 5: return current <= target;
                        default: throw new InvalidDataException("Unsupported official story comparison option: " + input.compareOption);
                    }
                default: throw new InvalidDataException("Unsupported official story condition: " + input.type);
            }
        }

        private object EvaluateStoryValue(Map10RuntimeStory story, Map10RuntimeStoryNode node, int depth = 0)
        {
            if (node == null || depth > 64) throw new InvalidDataException("Invalid official story value graph.");
            switch (node.type)
            {
                case "QueryItemCountNode": return StoryItemCount(StoryFields(node).ItemId);
                case "QueryStoryCountNode": return GetStoryPlayCount(node.storyName);
                case "QueryPlayerLevelNode": return progressionSave.Save.Player.Level;
                case "StringFormatNode":
                    StorySerializableValue[] parameters = StoryFields(node).Params ?? Array.Empty<StorySerializableValue>();
                    return parameters.Select((value, index) =>
                    {
                        int target = StoryNext(node, "Params " + index);
                        return target > 0 ? EvaluateStoryValue(story, FindStoryNode(story, target), depth + 1)
                            : StoryConstant(value);
                    }).ToArray();
                default: throw new InvalidDataException("Unsupported official story value: " + node.type);
            }
        }

        private static object StoryConstant(StorySerializableValue value)
        {
            // Common.SerializableObj.ValueType stores the original primitive tag.
            switch (value.mValueType)
            {
                case 0: return value.mInt;
                case 1: return value.mLong;
                case 2: return value.mFloat;
                case 3: return value.mDouble;
                case 4: return value.mBool != 0;
                case 5: return value.mString;
                default: throw new InvalidDataException("Unsupported official story parameter type: " + value.mValueType);
            }
        }

        private string FormatStoryOption(Map10RuntimeStoryNode node, int index, string text)
        {
            int target = StoryNext(node, "OptionParams " + index);
            if (target <= 0) return text;
            object[] parameters = EvaluateStoryValue(activeStory, FindStoryNode(activeStory, target)) as object[];
            if (parameters == null) throw new InvalidDataException("Official story option parameters are invalid.");
            return string.Format(CultureInfo.InvariantCulture, text, parameters);
        }

        private Dictionary<int, string> ResolveStoryDialogOverrides(Map10RuntimeStory story, Map10RuntimeStoryNode node)
        {
            var result = new Dictionary<int, string>();
            if (node == null) return result;
            int target = StoryNext(node, "DialogItems");
            if (target <= 0) return result;
            Map10RuntimeStoryNode merge = FindStoryNode(story, target);
            if (merge == null || merge.type != "DialogItemMergeNode")
                throw new InvalidDataException("Unsupported official dialog item provider.");
            foreach (Map10RuntimeStoryConnection connection in merge.ports.First(value => value.name == "DialogItems").connections)
            {
                Map10RuntimeStoryNode item = FindStoryNode(story, connection.nodeId);
                if (item == null || item.type != "DialogItemNode")
                    throw new InvalidDataException("Unsupported official dialog item input.");
                StoryDialogFields fields = JsonUtility.FromJson<StoryDialogFields>(item.sourceJson);
                Map10RuntimeDialogueLine line = story.dialogueGroups.First(value => value.Id == fields.DialogId)
                    .Lines.FirstOrDefault(value => value.Order == fields.Order);
                if (line == null) continue;
                // DialogItemNode reads dynamic Params ports; the serialized
                // string list supplies the number of arguments.
                int count = item.ports.Count(value => value.name.StartsWith("Params ", StringComparison.Ordinal));
                object[] parameters = Enumerable.Range(0, count).Select(index =>
                {
                    int source = StoryNext(item, "Params " + index);
                    return source > 0 ? EvaluateStoryValue(story, FindStoryNode(story, source)) : null;
                }).ToArray();
                result[fields.Order] = string.Format(CultureInfo.InvariantCulture, line.Text, parameters);
            }
            return result;
        }
    }
}
