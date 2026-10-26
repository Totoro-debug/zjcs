using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfficialMainlineTaskSourceManifest
    {
        public string manifest;
        public string output_sha256;
    }

    [Serializable]
    public sealed class OfficialMainlineTaskCatalogData
    {
        public string manifest;
        public int playerExpItemId;
        public OfficialMainlineTaskDefinition[] tasks;
        public OfficialMainlineNpcFriendshipLevel[] npcFriendshipLevels;
        public int[] rewardItemIds;
        public OfficialMainlineRewardItem[] rewardItems;
    }

    [Serializable]
    public sealed class OfficialMainlineNpcFriendshipLevel
    {
        public int npcClassId;
        public int level;
        public long friendshipExp;
        public int subRankLimit;
    }

    [Serializable]
    public sealed class OfficialMainlineRewardItem
    {
        public int classId;
        public int itemType;
    }

    [Serializable]
    public sealed class OfficialMainlineTaskDefinition
    {
        public int classId;
        public int order;
        public int triggerType;
        public OfficialTaskCondition acquestCondition;
        public OfficialTaskCondition displayCondition;
        public OfficialTaskCondition finishCondition;
        public OfficialTaskReward[] rewards;
        public OfficialTaskNpcFriendshipReward[] npcFriendshipRewards;
        public int[] rewardRuleIds;
        public int[] rewardRuleIdsWithProbability;
    }

    [Serializable]
    public sealed class OfficialTaskNpcFriendshipReward
    {
        public int npcClassId;
        public long amount;
    }

    [Serializable]
    public sealed class OfficialTaskCondition
    {
        public string type;
        public int mapClassId;
        public int region;
        public int count;
        public int result;
        public int progress;
        public int[] taskClassIds;
        public string tag;
        public int rank;
        public int chestId;
        public int[] ids;
        public int entryIndex;
        public int targetLayer;
    }

    [Serializable]
    public sealed class OfficialTaskReward
    {
        public int itemId;
        public long count;
    }

    public sealed class OfficialMainlineTaskCatalog
    {
        public const string ExpectedManifest = "854_156383";
        public const string DefaultResourcePath = "Official/Progression/mainline-tasks";
        public const string DefaultSourceManifestResourcePath = "Official/Progression/mainline-tasks-source-manifest";

        private readonly Dictionary<int, OfficialMainlineTaskDefinition> tasks;
        private readonly List<OfficialMainlineTaskDefinition> orderedTasks;
        private readonly HashSet<int> rewardItemIds;
        private readonly Dictionary<int, int> rewardItemTypes;
        private readonly Dictionary<int, List<OfficialMainlineNpcFriendshipLevel>> npcFriendshipLevels;

        private OfficialMainlineTaskCatalog(OfficialMainlineTaskCatalogData data)
        {
            tasks = new Dictionary<int, OfficialMainlineTaskDefinition>();
            orderedTasks = new List<OfficialMainlineTaskDefinition>(data.tasks);
            PlayerExpItemId = data.playerExpItemId;
            rewardItemIds = new HashSet<int>(data.rewardItemIds ?? Array.Empty<int>());
            rewardItemTypes = new Dictionary<int, int>();
            npcFriendshipLevels = new Dictionary<int, List<OfficialMainlineNpcFriendshipLevel>>();
            var friendshipKeys = new HashSet<(int NpcClassId, int Level)>();
            foreach (OfficialMainlineNpcFriendshipLevel row in data.npcFriendshipLevels
                ?? Array.Empty<OfficialMainlineNpcFriendshipLevel>())
            {
                if (row == null || row.npcClassId <= 0 || row.level <= 0
                    || row.friendshipExp < 0 || row.subRankLimit <= 0
                    || !friendshipKeys.Add((row.npcClassId, row.level)))
                    throw new FormatException("NPC friendship level data is invalid.");
                if (!npcFriendshipLevels.TryGetValue(row.npcClassId, out List<OfficialMainlineNpcFriendshipLevel> rows))
                {
                    rows = new List<OfficialMainlineNpcFriendshipLevel>();
                    npcFriendshipLevels.Add(row.npcClassId, rows);
                }
                rows.Add(row);
            }
            if (friendshipKeys.Count != 10000)
                throw new FormatException("NPC friendship level data does not match the pinned table.");
            foreach (List<OfficialMainlineNpcFriendshipLevel> rows in npcFriendshipLevels.Values)
                rows.Sort((left, right) => left.level.CompareTo(right.level));
            foreach (OfficialMainlineRewardItem item in data.rewardItems ?? Array.Empty<OfficialMainlineRewardItem>())
            {
                if (item == null || item.classId <= 0 || !rewardItemTypes.TryAdd(item.classId, item.itemType))
                    throw new FormatException("Invalid official mainline reward item catalog.");
            }

            foreach (OfficialMainlineTaskDefinition task in orderedTasks)
            {
                if (task == null || task.classId <= 0 || task.triggerType != 1
                    || task.acquestCondition == null || task.finishCondition == null
                    || !tasks.TryAdd(task.classId, task))
                    throw new FormatException("Invalid official mainline task catalog row.");
                if (task.rewards == null) task.rewards = Array.Empty<OfficialTaskReward>();
                if (task.rewardRuleIds == null) task.rewardRuleIds = Array.Empty<int>();
                if (task.rewardRuleIdsWithProbability == null)
                    task.rewardRuleIdsWithProbability = Array.Empty<int>();
                if (task.npcFriendshipRewards == null)
                    task.npcFriendshipRewards = Array.Empty<OfficialTaskNpcFriendshipReward>();
                if (task.displayCondition != null && string.IsNullOrEmpty(task.displayCondition.type))
                    task.displayCondition = null;
                foreach (OfficialTaskReward reward in task.rewards)
                    if (reward == null || reward.itemId <= 0 || reward.count < 0 || !rewardItemIds.Contains(reward.itemId))
                        throw new FormatException("Task reward is not present in the pinned item catalog.");
                foreach (OfficialTaskNpcFriendshipReward reward in task.npcFriendshipRewards)
                    if (reward == null || reward.npcClassId <= 0 || reward.amount < 0)
                        throw new FormatException("Task NPC friendship reward is invalid.");
            }
            if (tasks.Count == 0 || rewardItemTypes.Count != rewardItemIds.Count)
                throw new FormatException("Official mainline task catalog is incomplete.");
        }

        public static OfficialMainlineTaskCatalog LoadDefault()
        {
            TextAsset catalog = Resources.Load<TextAsset>(DefaultResourcePath);
            TextAsset manifest = Resources.Load<TextAsset>(DefaultSourceManifestResourcePath);
            if (catalog == null || manifest == null)
                throw new InvalidOperationException("Official mainline task resources are not installed.");
            return Load(catalog.text, manifest.text);
        }

        public static OfficialMainlineTaskCatalog Load(string catalogJson, string sourceManifestJson)
        {
            if (string.IsNullOrEmpty(catalogJson) || string.IsNullOrEmpty(sourceManifestJson))
                throw new ArgumentException("Official mainline task payload and manifest are required.");
            OfficialMainlineTaskSourceManifest manifest = JsonUtility.FromJson<OfficialMainlineTaskSourceManifest>(sourceManifestJson);
            if (manifest == null || manifest.manifest != ExpectedManifest)
                throw new InvalidOperationException("Official mainline task catalog is not from the pinned manifest.");
            string actualHash;
            using (SHA256 sha = SHA256.Create())
                actualHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(catalogJson)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            if (!string.Equals(actualHash, manifest.output_sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Official mainline task catalog hash differs from its manifest.");

            OfficialMainlineTaskCatalogData data = JsonUtility.FromJson<OfficialMainlineTaskCatalogData>(catalogJson);
            if (data == null || data.manifest != ExpectedManifest || data.playerExpItemId <= 0 || data.tasks == null
                || Array.Exists(data.tasks, task => task == null))
                throw new FormatException("Official mainline task catalog payload is invalid.");
            Array.Sort(data.tasks, (left, right) =>
            {
                int order = left.order.CompareTo(right.order);
                return order != 0 ? order : left.classId.CompareTo(right.classId);
            });
            return new OfficialMainlineTaskCatalog(data);
        }

        public IReadOnlyList<OfficialMainlineTaskDefinition> OrderedTasks
        {
            get { return orderedTasks; }
        }

        public int PlayerExpItemId { get; }

        public bool TryGetFriendshipExperience(int npcClassId, int level, out long experience)
        {
            if (npcFriendshipLevels.TryGetValue(npcClassId, out List<OfficialMainlineNpcFriendshipLevel> rows))
            {
                OfficialMainlineNpcFriendshipLevel row = rows.FirstOrDefault(value => value.level == level);
                if (row != null)
                {
                    experience = row.friendshipExp;
                    return true;
                }
            }
            experience = 0;
            return false;
        }

        public int FriendshipLevel(int npcClassId, long experience, int subRank)
        {
            if (!npcFriendshipLevels.TryGetValue(npcClassId, out List<OfficialMainlineNpcFriendshipLevel> rows))
                return 0;
            return rows.Where(value => value.friendshipExp <= experience && value.subRankLimit <= subRank)
                .Select(value => value.level).DefaultIfEmpty(0).Max();
        }

        public int MaximumFriendshipLevel(int npcClassId, int subRank)
        {
            if (!npcFriendshipLevels.TryGetValue(npcClassId, out List<OfficialMainlineNpcFriendshipLevel> rows))
                return 0;
            return rows.Where(value => value.subRankLimit <= subRank)
                .Select(value => value.level).DefaultIfEmpty(0).Max();
        }

        public bool TryGetTask(int classId, out OfficialMainlineTaskDefinition task)
        {
            return tasks.TryGetValue(classId, out task);
        }

        public bool ContainsRewardItem(int itemId)
        {
            return rewardItemIds.Contains(itemId);
        }

        public bool TryGetRewardItemType(int itemId, out int itemType)
        {
            return rewardItemTypes.TryGetValue(itemId, out itemType);
        }
    }
}
