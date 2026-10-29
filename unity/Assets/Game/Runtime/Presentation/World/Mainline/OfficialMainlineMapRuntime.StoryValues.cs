using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private Dictionary<int, string> dialogueOverrides;
        private StoryMap14RegionLabels map14StoryRegionLabels;
        private bool dialogueVoiceSuppressed;

        [Serializable] private sealed class StoryValueFields
        {
            public int ItemId, HandInItemId, RequireCount, ActionType, GoByNodeId, GotoId, DialogId, Order, ShowOptionsAtBegin;
            public int LoEntityId, State, TransferType;
            public string GoTo;
            public string Tag;
            public StoryItemRequirement[] ItemConfigs;
            public StorySerializableValue[] Params;
        }
        [Serializable] private sealed class StoryItemRequirement { public int ItemId; public long ItemCount; }
        [Serializable] private sealed class StoryDialogFields { public int DialogId, Order; }
        [Serializable] private sealed class StoryBooleanFields { public int Input, MapClassId, RegionId, State, LoEntityId, SceneType; public string Tag; }
        [Serializable] private sealed class StoryTransferFields
        {
            public string LoEntityTag;
            public int MapClassId, LoEntityId, IsFastTransfer, Direction;
        }
        [Serializable] private sealed class StoryMap14RegionLabels
        {
            public int mapClassId, width, height;
            public float[] origin;
            public int[] regionGrid;

            public int QueryRegion(Vector3 position)
            {
                if (mapClassId != 14 || origin == null || origin.Length < 2 || regionGrid == null
                    || regionGrid.Length != width * height)
                    throw new InvalidDataException("Official Map14 region label grid is malformed.");
                int x = (int)(position.x - origin[0] + 0.5f);
                int y = (int)(position.y - origin[1] + 0.5f);
                return x < 0 || x >= width || y < 0 || y >= height ? -1 : regionGrid[y * width + x];
            }
        }
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

        private StoryMap14RegionLabels Map14StoryRegionLabels()
        {
            if (map14StoryRegionLabels != null) return map14StoryRegionLabels;
            TextAsset labels = Resources.Load<TextAsset>("Map14/Native/region-label-data");
            if (labels == null) throw new InvalidDataException("Official Map14 region labels are missing.");
            map14StoryRegionLabels = JsonUtility.FromJson<StoryMap14RegionLabels>(labels.text);
            if (map14StoryRegionLabels == null || map14StoryRegionLabels.mapClassId != 14
                || map14StoryRegionLabels.width != 280 || map14StoryRegionLabels.height != 280
                || map14StoryRegionLabels.regionGrid == null
                || map14StoryRegionLabels.regionGrid.Length != map14StoryRegionLabels.width * map14StoryRegionLabels.height)
                throw new InvalidDataException("Official Map14 region label grid does not match its pinned map.");
            return map14StoryRegionLabels;
        }

        private int QueryMap14StoryRegion(Vector3 position) => Map14StoryRegionLabels().QueryRegion(position);

        internal static Zjcs.Offline.World.MapGridPoint ConvertStoryVectorPositionToGrid(Vector2 position) =>
            new Zjcs.Offline.World.MapGridPoint((int)position.x, (int)position.y);

        internal static Vector2 ConvertStoryCameraPosition(OfficialMapNavigation navigation, Vector3 position, Vector2 offset)
        {
            if (navigation == null) throw new ArgumentNullException(nameof(navigation));
            Zjcs.Offline.World.OfficialMapNavigationView.WorldToGrid(navigation,
                new OfficialMapVector3 { X = position.x, Y = position.y, Z = position.z }, out int x, out int y);
            return new Vector2(x, y) + offset;
        }

        private long StoryItemCount(int itemId) => progressionSave.Save.Player.Inventory
            .FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;

        private bool EvaluateStoryBoolean(Map10RuntimeStory story, Map10RuntimeStoryNode input, int depth = 0)
        {
            if (input == null || depth > 64) throw new InvalidDataException("Invalid official story condition graph.");
            switch (input.type)
            {
                case "PlayerConditionNode":
                    if (mapClassId != 13 || map13PlayerConditionEvaluator == null)
                        throw new InvalidDataException("Official player conditions are not loaded for Map" + mapClassId + ".");
                    return map13PlayerConditionEvaluator.Evaluate(input.playerConditionType,
                        input.playerConditionParam, input.playerConditionIds, input.playerConditionNot);
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
                    if (mapClassId == 14) return QueryMap14StoryRegion(avatar.transform.position) == targetMap.RegionId;
                    return regions.regionConfigs.TryGetValue(targetMap.RegionId, out OfficialMainlineRegionConfig region)
                        && Zjcs.Offline.World.OfficialMapNavigationView.TryGetCell(session.Definition.Navigation,
                            navigation.currentGrid.X, navigation.currentGrid.Y, out int area, out int ground)
                        && region.Areas.Contains(area);
                case "QueryPlayerContainsItemNode":
                    return StoryFields(input).ItemConfigs.All(value => StoryItemCount(value.ItemId) >= value.ItemCount);
                case "CanGetFixedRewardNode": return CanGetFixedStoryReward(input.fixedRewardId);
                case "CanHandInItemNode":
                    return handInItems.TryGetValue(input.handInItemId, out Map13RuntimeHandInItem handIn)
                        && progressionSave.CanHandInItem(handIn.Id, handIn.CanHandTimes);
                case "CheckMapEntityStateNode": return CheckStoryMapEntityState(input);
                case "WaitMapEntityInStateConditionNode":
                    StoryValueFields entityState = StoryFields(input);
                    Map10RuntimeLogicTag logicTag = mapLogicTags.Values.FirstOrDefault(value => value.Tag == entityState.Tag);
                    if (logicTag == null || logicTag.MapClass != mapClassId
                        || !mapEntities.TryGetValue(logicTag.LogicId, out Map10RuntimeEntity stateEntity)) return false;
                    Map10RuntimeMapLogicState entitySavedState = localSave.mapLogicStates.FirstOrDefault(value =>
                        value != null && value.Tag == logicTag.Tag);
                    int currentState = progressionSave.Save.World.CompletedEntityTags.Contains(logicTag.Tag)
                        ? -1 : entitySavedState?.State ?? stateEntity.State;
                    return currentState == entityState.State;
                case "PlayerOnMapConditionNode":
                    StoryBooleanFields playerMap = JsonUtility.FromJson<StoryBooleanFields>(input.sourceJson);
                    if (session == null || avatar == null || battleController != null
                        || mapClassId != playerMap.MapClassId) return false;
                    if (playerMap.RegionId == -1) return true;
                    if (mapClassId == 14)
                        return QueryMap14StoryRegion(avatar.transform.position) == playerMap.RegionId;
                    return regions.regionConfigs.TryGetValue(playerMap.RegionId,
                            out OfficialMainlineRegionConfig playerRegion)
                        && Zjcs.Offline.World.OfficialMapNavigationView.TryGetCell(session.Definition.Navigation,
                            navigation.currentGrid.X, navigation.currentGrid.Y, out int playerArea, out int playerGround)
                        && playerRegion.Areas.Contains(playerArea);
                case "PlayerOnSceneConditionNode":
                    StoryBooleanFields playerScene = JsonUtility.FromJson<StoryBooleanFields>(input.sourceJson);
                    return (mapClassId == 13 || mapClassId == 14) && session != null && battleController == null
                        && playerScene.SceneType == 3;
                case "IsPlayerHasFollowerIdsNode":
                    int[] followerIds = JsonUtility.FromJson<StoryFollowerFields>(input.sourceJson)?.FollowerClassId;
                    if (followerIds == null)
                        throw new InvalidDataException("Official Map13 follower condition has no class ID list.");
                    return followerIds.All(progressionSave.Save.Player.NpcFollowerClassIds.Contains);
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
                case "GetMapWitchExploreInfoNode": return progressionSave.ActiveWitchCount;
                case "QueryPlayerLevelNode": return progressionSave.Save.Player.Level;
                case "GetEntityPosNode":
                case "GetPosAroundStoryObjectNode": return ResolveStoryPositionNode(node);
                case "GetPosAroundWorldCamera":
                    if (worldCamera == null) throw new InvalidDataException("Official story world camera is not bound.");
                    return ConvertStoryCameraPosition(session.Definition.Navigation, worldCamera.transform.position,
                        StorySpatialFields(node).Offset);
                case "MapPositionChangeTypeNode":
                    StoryValueFields transfer = StoryFields(node);
                    if (transfer.TransferType != 1)
                        throw new InvalidDataException("Unsupported official story position transfer: " + transfer.TransferType);
                    Map10RuntimeStoryNode vectorInput = FindStoryNode(story, StoryNext(node, "VectorPosIn"));
                    if (vectorInput == null) throw new InvalidDataException("Official story grid conversion has no vector input.");
                    Vector2 vector = (Vector2)EvaluateStoryValue(story, vectorInput, depth + 1);
                    return ConvertStoryVectorPositionToGrid(vector);
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
