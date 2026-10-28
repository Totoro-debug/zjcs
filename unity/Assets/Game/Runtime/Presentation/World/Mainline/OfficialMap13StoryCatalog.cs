using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.Progression;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMap13StoryCatalog
    {
        public const string Manifest = "871_156598";
        [Serializable] private sealed class MapActivationFields { public int MapClassId; }
        [Serializable] private sealed class AloneWorldFields { public int MapAloneWorldClassId; }
        [Serializable] private sealed class NpcFollowerFields { public int[] NpcClassIds; }
        [Serializable] private sealed class SceneConditionFields { public int SceneType; }
        public Map10RuntimeStoryData Data { get; }
        private readonly Dictionary<string, Map10RuntimeStory> stories;
        private readonly Dictionary<string, Map10RuntimeStory> subgraphs;

        private OfficialMap13StoryCatalog(Map10RuntimeStoryData data)
        {
            Data = data;
            stories = data.stories.ToDictionary(value => value.key, StringComparer.Ordinal);
            subgraphs = data.subgraphs.ToDictionary(value => value.key, StringComparer.Ordinal);
            Validate();
        }

        public static OfficialMap13StoryCatalog Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Map13/stories");
            if (asset == null) throw new InvalidDataException("Official Map13 story data is missing.");
            Map10RuntimeStoryData data = JsonUtility.FromJson<Map10RuntimeStoryData>(asset.text);
            if (data == null) throw new InvalidDataException("Official Map13 story data could not be decoded.");
            return new OfficialMap13StoryCatalog(data);
        }

        public bool TryGetStory(string key, out Map10RuntimeStory story) => stories.TryGetValue(key, out story)
            || subgraphs.TryGetValue(key, out story);

        public bool TryGetSubgraph(string key, out Map10RuntimeStory story) => subgraphs.TryGetValue(key, out story);

        public bool CanConfirmHandIn(OfflineMainlineProgressSession progress, string slotId, int itemId)
        {
            if (progress == null || string.IsNullOrWhiteSpace(slotId)) return false;
            Map13RuntimeHandInItem config = Data.handInItems.FirstOrDefault(value => value.Id == itemId);
            if (config == null) return false;
            int claim = NextHandInClaim(progress, slotId, itemId, config.CanHandTimes);
            if (claim < 0) return false;
            return config.Items.All(required => (progress.Save.Player.Inventory ?? new List<OfflineItemStack>()).Any(stack => stack != null
                && stack.ItemId == required.ItemId && stack.Count >= required.Count));
        }

        public ProgressionResult ConfirmHandIn(OfflineMainlineProgressSession progress, string slotId, int itemId)
        {
            if (progress == null || string.IsNullOrWhiteSpace(slotId))
                throw new ArgumentException("Map13 hand-in requires a local progress session and slot.");
            Map13RuntimeHandInItem config = Data.handInItems.FirstOrDefault(value => value.Id == itemId);
            if (config == null) throw new InvalidDataException("Official Map13 hand-in item is missing: " + itemId);
            int claim = NextHandInClaim(progress, slotId, itemId, config.CanHandTimes);
            if (claim < 0) return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            string receipt = HandInReceipt(slotId, itemId, claim);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return progress.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (Map10RuntimeAwardItem required in config.Items)
                {
                    InventoryOperationResult consumed = OfflineInventoryOperations.Consume(save.Player,
                        required.ItemId, required.Count, now);
                    if (!consumed.IsApplied)
                        throw new InvalidOperationException("Map13 hand-in requirements changed before confirmation.");
                }
            });
        }

        public bool CanClaimFixedReward(OfflineMainlineProgressSession progress, string slotId, int rewardId)
        {
            if (progress == null || string.IsNullOrWhiteSpace(slotId)) return false;
            Map10RuntimeFixedReward config = Data.fixedRewards.FirstOrDefault(value => value != null && value.Id == rewardId);
            return config != null && NextFixedRewardClaim(progress, slotId, rewardId, config.CanGetTimes) >= 0;
        }

        public ProgressionResult ClaimFixedReward(OfflineMainlineProgressSession progress, string slotId, int rewardId)
        {
            if (progress == null || string.IsNullOrWhiteSpace(slotId))
                throw new ArgumentException("Map13 fixed reward requires a local progress session and slot.");
            Map10RuntimeFixedReward config = Data.fixedRewards.FirstOrDefault(value => value != null && value.Id == rewardId);
            if (config == null) throw new InvalidDataException("Official Map13 fixed reward is missing: " + rewardId);
            int claim = NextFixedRewardClaim(progress, slotId, rewardId, config.CanGetTimes);
            if (claim < 0) return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };

            string receipt = FixedRewardReceipt(slotId, rewardId, claim);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return progress.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (Map10RuntimeAwardItem item in config.Items)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        item.ItemId, item.Count, now);
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Map13 fixed reward could not be applied: " + item.ItemId);
                }
            });
        }

        private static int NextHandInClaim(OfflineMainlineProgressSession progress, string slotId,
            int itemId, int maximum)
        {
            for (int claim = 0; claim < maximum; claim++)
                if (!progress.Save.Player.AppliedInventoryEventIds.Contains(HandInReceipt(slotId, itemId, claim)))
                    return claim;
            return -1;
        }

        private static int NextFixedRewardClaim(OfflineMainlineProgressSession progress, string slotId,
            int rewardId, int maximum)
        {
            for (int claim = 0; claim < maximum; claim++)
                if (!progress.Save.Player.AppliedInventoryEventIds.Contains(FixedRewardReceipt(slotId, rewardId, claim)))
                    return claim;
            return -1;
        }

        private static string HandInReceipt(string slotId, int itemId, int claim) =>
            "map13:" + slotId + ":hand-in:" + itemId + ":" + claim;

        private static string FixedRewardReceipt(string slotId, int rewardId, int claim) =>
            "map13:" + slotId + ":fixed-reward:" + rewardId + ":" + claim;

        private void Validate()
        {
            if (Data.manifest != Manifest || Data.mapClassId != 13 || Data.stories == null
                || Data.stories.Length != 74 || Data.subgraphs == null || Data.subgraphs.Length != 23
                || Data.dialogRoles == null || Data.dialogRoles.Length != 20
                || Data.mapLogicTags == null || Data.mapLogicTags.Length != 49
                || Data.playerConditions == null || Data.playerConditions.Length != 2
                || Data.storyOptions == null || Data.fixedRewards == null || Data.fixedRewards.Length != 7
                || Data.handInItems == null || Data.handInItems.Length != 3)
                throw new InvalidDataException("Official Map13 story catalog does not match its pinned source closure.");

            if (Data.mapTalkEntries == null || Data.mapTalkEntries.Length != 64
                || Data.mapTalkConditions == null || Data.mapTalkConditions.Length != 23
                || Data.entityTemplates == null || Data.entityTemplates.Length != 165
                || Data.mainlineTasks == null || Data.mainlineTasks.Length != 20
                || Data.quickBuys == null || Data.quickBuys.Length != 1)
                throw new InvalidDataException("Official Map13 common story dependencies are incomplete.");
            foreach (int follower in new[] { 8402, 8440, 8441, 8442 })
                if (!Data.entityTemplates.Any(value => value != null && value.EntityClassId == follower
                    && value.Components != null && value.Components.Length > 0))
                    throw new InvalidDataException("Official Map13 follower EC template is missing: " + follower);
            foreach (Map10RuntimeMapTalkEntry talk in Data.mapTalkEntries)
                if (talk == null || (!string.IsNullOrEmpty(talk.TalkId) && !stories.ContainsKey(talk.TalkId)
                    && !subgraphs.ContainsKey(talk.TalkId)))
                    throw new InvalidDataException("Official Map13 MapTalk story is missing.");

            HashSet<string> all = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> logicTags = new HashSet<string>(Data.mapLogicTags.Select(value => value?.Tag), StringComparer.Ordinal);
            var playerConditions = new Dictionary<int, Map13RuntimePlayerConditionData>();
            foreach (Map13RuntimePlayerCondition condition in Data.playerConditions)
            {
                if (condition == null || condition.ClassId <= 0 || condition.Condition == null
                    || playerConditions.ContainsKey(condition.ClassId)
                    || (condition.Condition._type != "HasItemCondition"
                        && condition.Condition._type != "StoryCountCondition"
                        && condition.Condition._type != "CompositeOrCondition"))
                    throw new InvalidDataException("Official Map13 player condition catalog is malformed.");
                if (condition.Condition._type == "CompositeOrCondition"
                    && (condition.Condition.Ids == null || condition.Condition.Ids.Length == 0
                        || condition.Condition.Ids.Any(id => id <= 0)))
                    throw new InvalidDataException("Official Map13 composite player condition is malformed.");
                playerConditions.Add(condition.ClassId, condition.Condition);
            }
            if (Data.playerConditions.Any(value => value.Condition._type == "CompositeOrCondition"
                && value.Condition.Ids.Any(id => !Data.playerConditions.Any(target => target.ClassId == id))))
                throw new InvalidDataException("Official Map13 composite player condition references a missing ID.");
            int mapActivations = 0;
            int aloneWorldAppearances = 0;
            int npcFollowerAdds = 0;
            int npcFollowerRemovals = 0;
            int mapSceneConditions = 0;
            HashSet<int> npcFollowerClasses = new HashSet<int>();
            foreach (Map10RuntimeStory story in Data.stories.Concat(Data.subgraphs))
            {
                if (story == null || string.IsNullOrWhiteSpace(story.key) || !all.Add(story.key)
                    || story.nodes == null || story.nodes.Length == 0 || story.entryNodeId <= 0
                    || story.dialogueGroups == null || story.nodes.Select(node => node.id).Distinct().Count() != story.nodes.Length
                    || story.nodes.All(node => node.id != story.entryNodeId))
                    throw new InvalidDataException("Official Map13 story graph is malformed.");
                HashSet<int> ids = new HashSet<int>(story.nodes.Select(node => node.id));
                foreach (Map10RuntimeStoryNode node in story.nodes)
                {
                    if (node == null || node.ports == null || string.IsNullOrEmpty(node.type)
                        || node.ports.Any(port => port == null || port.connections == null
                            || port.connections.Any(link => link == null || link.nodeId <= 0 || !ids.Contains(link.nodeId))))
                        throw new InvalidDataException("Official Map13 story graph contains an invalid node edge: " + story.key);
                    if (node.type == "SubgraphRunNode" && !subgraphs.ContainsKey(node.subgraphName))
                        throw new InvalidDataException("Official Map13 story subgraph is not loaded: " + node.subgraphName);
                    if (node.type == "HandInItemNode"
                        && !Data.handInItems.Any(value => value != null && value.Id == node.handInItemId))
                        throw new InvalidDataException("Official Map13 hand-in item configuration is not loaded: " + node.handInItemId);
                    if (node.type == "SetMapLogicEntitySetStateNode"
                        && (node.mapLogicStates == null || node.mapLogicStates.Length == 0
                            || node.mapLogicStates.Any(state => state == null || string.IsNullOrWhiteSpace(state.Tag))))
                        throw new InvalidDataException("Official Map13 story references an unknown map-logic tag: " + story.key);
                    if (node.type == "SetMapLogicEntitySetStateNode")
                        foreach (Map10RuntimeMapLogicState state in node.mapLogicStates)
                            if (!logicTags.Contains(state.Tag)) logicTags.Add(state.Tag);
                    if (node.type == "PlayerActiveMapNode")
                    {
                        MapActivationFields target = JsonUtility.FromJson<MapActivationFields>(node.sourceJson);
                        if (target == null || target.MapClassId <= 0)
                            throw new InvalidDataException("Official Map13 story has an invalid map activation target: " + story.key);
                        mapActivations++;
                    }
                    if (node.type == "PlayerWorldAloneAppearNode")
                    {
                        AloneWorldFields target = JsonUtility.FromJson<AloneWorldFields>(node.sourceJson);
                        if (target == null || target.MapAloneWorldClassId <= 0)
                            throw new InvalidDataException("Official Map13 story has an invalid alone-world target: " + story.key);
                        aloneWorldAppearances++;
                    }
                    if (node.type == "AddNpcFollowerNode")
                    {
                        NpcFollowerFields followers = JsonUtility.FromJson<NpcFollowerFields>(node.sourceJson);
                        if (followers?.NpcClassIds == null || followers.NpcClassIds.Length == 0
                            || followers.NpcClassIds.Any(id => id <= 0))
                            throw new InvalidDataException("Official Map13 follower node has an invalid class ID list: " + story.key);
                        npcFollowerClasses.UnionWith(followers.NpcClassIds);
                        npcFollowerAdds++;
                    }
                    if (node.type == "RemovePlayerNpcFollowerAllNode") npcFollowerRemovals++;
                    if (node.type == "PlayerOnSceneConditionNode")
                    {
                        SceneConditionFields scene = JsonUtility.FromJson<SceneConditionFields>(node.sourceJson);
                        if (scene == null || scene.SceneType != 3)
                            throw new InvalidDataException("Official Map13 story waits for an unsupported scene type: " + story.key);
                        mapSceneConditions++;
                    }
                    if (node.type == "PlayerConditionNode")
                    {
                        if (string.IsNullOrWhiteSpace(node.playerConditionType)
                            || node.playerConditionParam == null)
                            throw new InvalidDataException("Official Map13 player condition node is malformed: " + story.key);
                        switch (node.playerConditionType)
                        {
                            case "CompositeOr":
                                if (node.playerConditionIds == null || node.playerConditionIds.Length == 0
                                    || node.playerConditionIds.Any(id => !playerConditions.ContainsKey(id)))
                                    throw new InvalidDataException("Official Map13 composite player condition references a missing ID: " + story.key);
                                break;
                            case "MapLogicEntityEndByTag":
                            case "StagePass":
                            case "StoryCount":
                            case "StoryStatus":
                            case "WorldMapProgress":
                                break;
                            default:
                                throw new InvalidDataException("Unsupported official Map13 player condition node: " + node.playerConditionType);
                        }
                    }
                }
            }
            if (mapActivations != 1 || aloneWorldAppearances != 1)
                throw new InvalidDataException("Official Map13 story map activation/alone-world node closure is incomplete.");
            if (npcFollowerAdds != 6 || npcFollowerRemovals != 8
                || !npcFollowerClasses.SetEquals(new[] { 8402, 8440, 8441, 8442 }))
                throw new InvalidDataException("Official Map13 NPC follower node closure is incomplete.");
            if (mapSceneConditions != 12)
                throw new InvalidDataException("Official Map13 map-scene condition closure is incomplete.");
            if (Data.handInItems.Any(value => value == null || value.Id <= 0 || value.CanHandTimes < 0
                    || value.Items == null || value.Items.Length == 0
                    || value.Items.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0)))
                throw new InvalidDataException("Official Map13 hand-in item configuration is malformed.");
            if (Data.handInItems.Select(value => value.Id).Distinct().Count() != Data.handInItems.Length)
                throw new InvalidDataException("Official Map13 hand-in item IDs are duplicated.");
            if (Data.fixedRewards.Any(value => value == null || value.Id <= 0 || value.CanGetTimes < 0
                    || value.Items == null || value.Items.Length == 0
                    || value.Items.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0)))
                throw new InvalidDataException("Official Map13 fixed reward configuration is malformed.");
            if (Data.fixedRewards.Select(value => value.Id).Distinct().Count() != Data.fixedRewards.Length)
                throw new InvalidDataException("Official Map13 fixed reward IDs are duplicated.");
        }
    }
}
