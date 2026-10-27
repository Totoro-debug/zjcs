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

        public bool TryGetStory(string key, out Map10RuntimeStory story) => stories.TryGetValue(key, out story);

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

        private static int NextHandInClaim(OfflineMainlineProgressSession progress, string slotId,
            int itemId, int maximum)
        {
            for (int claim = 0; claim < maximum; claim++)
                if (!progress.Save.Player.AppliedInventoryEventIds.Contains(HandInReceipt(slotId, itemId, claim)))
                    return claim;
            return -1;
        }

        private static string HandInReceipt(string slotId, int itemId, int claim) =>
            "map13:" + slotId + ":hand-in:" + itemId + ":" + claim;

        private void Validate()
        {
            if (Data.manifest != Manifest || Data.mapClassId != 13 || Data.stories == null
                || Data.stories.Length != 65 || Data.subgraphs == null || Data.subgraphs.Length != 23
                || Data.dialogRoles == null || Data.dialogRoles.Length != 18
                || Data.mapLogicTags == null || Data.mapLogicTags.Length != 49
                || Data.storyOptions == null || Data.fixedRewards == null || Data.fixedRewards.Length != 6
                || Data.handInItems == null || Data.handInItems.Length != 3)
                throw new InvalidDataException("Official Map13 story catalog does not match its pinned source closure.");

            HashSet<string> all = new HashSet<string>(StringComparer.Ordinal);
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
                }
            }
            if (Data.handInItems.Any(value => value == null || value.Id <= 0 || value.CanHandTimes < 0
                    || value.Items == null || value.Items.Length == 0
                    || value.Items.Any(item => item == null || item.ItemId <= 0 || item.Count <= 0)))
                throw new InvalidDataException("Official Map13 hand-in item configuration is malformed.");
        }
    }
}
