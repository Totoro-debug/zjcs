using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Home
{
    /// <summary>
    /// One official condition node.  ``rules.json`` serialises every condition type
    /// with its own fields; the union below lets one JsonUtility model read all of
    /// the condition rows the home tables reference.
    /// </summary>
    [Serializable]
    public sealed class HomeConditionNode
    {
        public string _type;
        public int Level;
        public string Op;
        public int[] Ids;
        public int ClassId;
        public long Count;
        public int Rank;
        public int SubRank;
        public int MapClassId;
        public string StoryKey;
        public int Status;
        public int ItemType;
        public int Quality;
        public int WeaponType;
        public int Profession;
        public int Id;
        public int From;
        public int To;
        public int Days;
    }

    [Serializable]
    public sealed class HomeCostEntry
    {
        public int itemId;
        public long count;
    }

    [Serializable]
    public sealed class HomeFurnitureRecord
    {
        public int classId;
        public int type;
        public HomeConditionNode unlock;
        public HomeConditionNode repairNotice;
        public HomeConditionNode autoRepair;
        public HomeCostEntry[] repairCost;
        public int[] additionalRepair;
        public string repairStory;
        public bool canLevelUp;
    }

    [Serializable]
    public sealed class HomeHangUpRecord
    {
        public int id;
        public int bindFurniture;
        public int sortOrder;
        public int levelRuleId;
        public HomeConditionNode unlock;
        public HomeConditionNode notice;
        public bool canLevelUp;
        public bool useBlessingLevel;
        public int rewardType;
        public int rewardId;
        public int fastHangUpId;
        public int effectPercentProp;
        public int effectFixedProp;
        public int maxStoreProp;
        public int finallyEffectPercentProp;
        public int initialExtraRewardSeconds;
        public string icon;
    }

    [Serializable]
    public sealed class HomeHangUpRewardRecord
    {
        public int id;
        public int order;
        public HomeConditionNode condition;
        public int rewardRuleId;
        public double hourlyOutput;
    }

    [Serializable]
    public sealed class HomeHangUpUpgradeRecord
    {
        public int level;
        public int hangUpId;
        public HomeCostEntry[] cost;
    }

    [Serializable]
    public sealed class HomeFastHangUpItem
    {
        public int itemId;
        public double hours;
    }

    [Serializable]
    public sealed class HomeFastHangUpRecord
    {
        public int fastHangUpId;
        public int bindFurniture;
        public int[] hangUpIdList;
        public int[] extraHangUpRewardList;
        public int freeTimes;
        public HomeFastHangUpItem[] items;
        public int freeHours;
        public int maxUseItemCount;
        public bool autoReceive;
    }

    [Serializable]
    public sealed class HomeLevelRecord
    {
        public int level;
        public int furnitureLevelUpLimit;
    }

    [Serializable]
    public sealed class HomeFurnitureLevelRuleRecord
    {
        public int id;
        public int level;
        public double levelNum;
        public int maxStoreThread;
    }

    [Serializable]
    public sealed class HomeFurnitureSkinRecord
    {
        public int skinItemId;
        public int furnitureType;
        public string prefabName;
        public bool isDefault;
        public int order;
    }

    [Serializable]
    public sealed class HomeThemeSkinRecord
    {
        public int skinItemId;
        public int themeType;
        public string prefabName;
        public bool isDefault;
        public int order;
    }

    [Serializable]
    public sealed class HomeEnvironmentSkinRecord
    {
        public int skinItemId;
        public int environmentType;
        public string prefabName;
        public bool isDefault;
        public int order;
    }

    [Serializable]
    public sealed class HomeFunctionLimitRecord
    {
        public int type;
        public int key0;
        public int key1;
        public HomeConditionNode condition;
    }

    [Serializable]
    public sealed class HomeRewardRuleRecord
    {
        public int id;
        public int order;
        public HomeConditionNode condition;
        public double percent;
        public int[] ids;
        public int[] weights;
        public double[] counts;
    }

    [Serializable]
    public sealed class HomeConditionRow
    {
        public int classId;
        public HomeConditionNode node;
    }

    [Serializable]
    public sealed class HomeItemRecord
    {
        public int id;
        public string name;
        public int quality;
        public int type;
        public string icon;
        public int rank;
        public bool overlap;
    }

    [Serializable]
    public sealed class HomeRulesData
    {
        public string version;
        public HomeFurnitureRecord[] furniture;
        public HomeHangUpRecord[] hangUps;
        public HomeHangUpRewardRecord[] hangUpRewards;
        public HomeHangUpUpgradeRecord[] hangUpUpgradeMaterials;
        public HomeFastHangUpRecord[] fastHangUps;
        public HomeLevelRecord[] homeLevels;
        public HomeFurnitureLevelRuleRecord[] furnitureLevelRules;
        public HomeFurnitureSkinRecord[] furnitureSkins;
        public HomeThemeSkinRecord[] themeSkins;
        public HomeEnvironmentSkinRecord[] environmentSkins;
        public HomeFunctionLimitRecord[] functionLimits;
        public HomeRewardRuleRecord[] rewardRules;
        public HomeConditionRow[] conditions;
        public HomeItemRecord[] items;
    }

    [Serializable]
    public sealed class HomeRewardTypesData
    {
        public string version;
        public HomeRewardTypeRecord[] types;
        public HomeRewardGroupRecord[] groups;
        public HomeRewardSlotRecord[] slots;
    }

    [Serializable]
    public sealed class HomeRewardTypeRecord
    {
        public int type;
        public int minInterval;
        public int maxInterval;
        public int maxCount;
    }

    [Serializable]
    public sealed class HomeRewardGroupRecord
    {
        public int groupId;
        public int type;
        public int ruleId;
    }

    [Serializable]
    public sealed class HomeRewardSlotRecord
    {
        public int slotId;
        public int[] groupIds;
        public int furnitureLimit;
    }

    /// <summary>Player state every official home condition is evaluated against.</summary>
    public sealed class HomeConditionContext
    {
        public int Level;
        public int SubRank;
        public int Rank;
        public int MaxProfessionRank;
        public int InitialWeaponType;
        public int Profession;
        public ISet<int> UnlockedMapClassIds = new HashSet<int>();
        public Func<int, long> ItemCount = _ => 0;
        public Func<string, int> StoryCount = _ => 0;
        public Func<string, bool> StoryPlayed = _ => false;
        public Func<int, bool> HasItemOfType = _ => false;
        public bool AnyGoddessActive;
        public bool HasManualMeetNpc;
    }

    /// <summary>
    /// The staged official home tables plus the lookups the runtime needs.
    /// All values come from the fixed ``854_156383`` client.
    /// </summary>
    public sealed class OfficialHomeRules
    {
        public const string FolderName = "Home";

        private readonly Dictionary<int, HomeFurnitureRecord> byClassId = new Dictionary<int, HomeFurnitureRecord>();
        private readonly Dictionary<int, HomeFurnitureRecord> byType = new Dictionary<int, HomeFurnitureRecord>();
        private readonly Dictionary<int, HomeHangUpRecord> hangUps = new Dictionary<int, HomeHangUpRecord>();
        private readonly Dictionary<int, HomeHangUpRewardRecord> hangUpRewards = new Dictionary<int, HomeHangUpRewardRecord>();
        private readonly Dictionary<int, HomeFastHangUpRecord> fastHangUps = new Dictionary<int, HomeFastHangUpRecord>();
        private readonly Dictionary<long, double> levelNumbers = new Dictionary<long, double>();
        private readonly Dictionary<int, HomeLevelRecord> homeLevels = new Dictionary<int, HomeLevelRecord>();
        private readonly Dictionary<int, HomeConditionNode> conditions = new Dictionary<int, HomeConditionNode>();
        private readonly Dictionary<int, HomeItemRecord> items = new Dictionary<int, HomeItemRecord>();
        private readonly Dictionary<int, List<HomeRewardRuleRecord>> rewardRules = new Dictionary<int, List<HomeRewardRuleRecord>>();
        private readonly Dictionary<int, List<HomeHangUpUpgradeRecord>> upgrades = new Dictionary<int, List<HomeHangUpUpgradeRecord>>();
        private readonly List<HomeFunctionLimitRecord> functionLimits = new List<HomeFunctionLimitRecord>();

        public HomeRulesData Data { get; }
        public HomeRewardTypesData Rewards { get; }
        public IReadOnlyCollection<HomeFurnitureRecord> Furniture => byClassId.Values;
        public IReadOnlyCollection<HomeHangUpRecord> HangUps => hangUps.Values;
        public IReadOnlyCollection<HomeConditionNode> Unresolved { get; private set; } = Array.Empty<HomeConditionNode>();
        private readonly List<HomeConditionNode> unresolved = new List<HomeConditionNode>();

        private OfficialHomeRules(HomeRulesData data, HomeRewardTypesData rewards)
        {
            Data = data;
            Rewards = rewards;
            foreach (HomeFurnitureRecord record in data.furniture ?? Array.Empty<HomeFurnitureRecord>())
            {
                if (record == null) continue;
                byClassId[record.classId] = record;
                byType[record.type] = record;
            }
            foreach (HomeHangUpRecord record in data.hangUps ?? Array.Empty<HomeHangUpRecord>())
                if (record != null) hangUps[record.id] = record;
            foreach (HomeHangUpRewardRecord record in data.hangUpRewards ?? Array.Empty<HomeHangUpRewardRecord>())
                if (record != null) hangUpRewards[record.id] = record;
            foreach (HomeFastHangUpRecord record in data.fastHangUps ?? Array.Empty<HomeFastHangUpRecord>())
                if (record != null) fastHangUps[record.fastHangUpId] = record;
            foreach (HomeFurnitureLevelRuleRecord record in data.furnitureLevelRules ?? Array.Empty<HomeFurnitureLevelRuleRecord>())
                if (record != null) levelNumbers[Key(record.id, record.level)] = record.levelNum;
            foreach (HomeLevelRecord record in data.homeLevels ?? Array.Empty<HomeLevelRecord>())
                if (record != null) homeLevels[record.level] = record;
            foreach (HomeConditionRow row in data.conditions ?? Array.Empty<HomeConditionRow>())
                if (row != null && row.node != null) conditions[row.classId] = row.node;
            foreach (HomeItemRecord item in data.items ?? Array.Empty<HomeItemRecord>())
                if (item != null) items[item.id] = item;
            foreach (HomeRewardRuleRecord rule in data.rewardRules ?? Array.Empty<HomeRewardRuleRecord>())
            {
                if (rule == null) continue;
                if (!rewardRules.TryGetValue(rule.id, out List<HomeRewardRuleRecord> list))
                    rewardRules[rule.id] = list = new List<HomeRewardRuleRecord>();
                list.Add(rule);
            }
            foreach (List<HomeRewardRuleRecord> list in rewardRules.Values) list.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (HomeHangUpUpgradeRecord record in data.hangUpUpgradeMaterials ?? Array.Empty<HomeHangUpUpgradeRecord>())
            {
                if (record == null) continue;
                if (!upgrades.TryGetValue(record.hangUpId, out List<HomeHangUpUpgradeRecord> list))
                    upgrades[record.hangUpId] = list = new List<HomeHangUpUpgradeRecord>();
                list.Add(record);
            }
            foreach (List<HomeHangUpUpgradeRecord> list in upgrades.Values) list.Sort((a, b) => a.level.CompareTo(b.level));
            functionLimits.AddRange(data.functionLimits ?? Array.Empty<HomeFunctionLimitRecord>());
            Unresolved = unresolved;
        }

        public static OfficialHomeRules LoadDefault()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            return Load(folder);
        }

        public static OfficialHomeRules Load(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("A home payload folder is required.", nameof(folder));
            HomeRulesData data = ReadJson<HomeRulesData>(Path.Combine(folder, "rules.json"));
            HomeRewardTypesData rewards = ReadJson<HomeRewardTypesData>(Path.Combine(folder, "rewards.json"));
            if (data == null || data.version != "854_156383")
                throw new FormatException("The official home rules are missing or from an unsupported manifest.");
            if (rewards == null || rewards.slots == null || rewards.slots.Length != 29
                || rewards.types == null || rewards.types.Length != 2)
                throw new FormatException("The official home random reward tables are incomplete.");
            return new OfficialHomeRules(data, rewards);
        }

        private static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Home payload is missing.", path);
            T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
            if (value == null) throw new InvalidDataException("Home payload is empty: " + path);
            return value;
        }

        private static long Key(int ruleId, int level) => ((long)ruleId << 32) ^ (uint)level;

        public HomeFurnitureRecord FurnitureOf(int type) =>
            byType.TryGetValue(type, out HomeFurnitureRecord record) ? record : null;

        public HomeFurnitureRecord FurnitureByClassId(int classId) =>
            byClassId.TryGetValue(classId, out HomeFurnitureRecord record) ? record : null;

        public int FurnitureClassId(int type) => FurnitureOf(type)?.classId ?? 0;

        public HomeHangUpRecord HangUp(int id) => hangUps.TryGetValue(id, out HomeHangUpRecord record) ? record : null;

        public IReadOnlyList<HomeHangUpRecord> HangUpsOf(int furnitureType) =>
            hangUps.Values.Where(value => value.bindFurniture == furnitureType)
                .OrderBy(value => value.sortOrder).ThenBy(value => value.id).ToArray();

        public HomeFastHangUpRecord FastHangUp(int id) =>
            fastHangUps.TryGetValue(id, out HomeFastHangUpRecord record) ? record : null;

        public HomeHangUpRewardRecord HangUpReward(int id) =>
            hangUpRewards.TryGetValue(id, out HomeHangUpRewardRecord record) ? record : null;

        public HomeItemRecord Item(int id) => items.TryGetValue(id, out HomeItemRecord item) ? item : null;

        public string ItemName(int id) => Item(id)?.name ?? string.Empty;

        public string ItemIcon(int id) => Item(id)?.icon ?? string.Empty;

        public string FurnitureNameForType(int type)
        {
            HomeFurnitureRecord record = FurnitureOf(type);
            return record == null ? string.Empty : ItemName(record.classId);
        }

        public double LevelNumber(int levelRuleId, int level)
        {
            if (levelNumbers.TryGetValue(Key(levelRuleId, level), out double value)) return value;
            // The official table is dense; fall back to the highest level not above the request.
            double result = 0;
            foreach (KeyValuePair<long, double> pair in levelNumbers)
                if ((int)(pair.Key >> 32) == levelRuleId && (uint)pair.Key <= level) result = Math.Max(result, pair.Value);
            return result;
        }

        public int FurnitureLevelUpLimit(int blessingLevel)
        {
            if (homeLevels.TryGetValue(Math.Max(1, blessingLevel), out HomeLevelRecord record))
                return record.furnitureLevelUpLimit;
            return 1;
        }

        public IReadOnlyList<HomeHangUpUpgradeRecord> Upgrades(int hangUpId) =>
            upgrades.TryGetValue(hangUpId, out List<HomeHangUpUpgradeRecord> list)
                ? (IReadOnlyList<HomeHangUpUpgradeRecord>)list : Array.Empty<HomeHangUpUpgradeRecord>();

        public HomeHangUpUpgradeRecord Upgrade(int hangUpId, int level)
        {
            IReadOnlyList<HomeHangUpUpgradeRecord> list = Upgrades(hangUpId);
            foreach (HomeHangUpUpgradeRecord record in list)
                if (record.level == level) return record;
            return null;
        }

        public int MaxHangUpLevel(int hangUpId) => Upgrades(hangUpId).Count + 1;

        /// <summary>First satisfaction-ordered reward rule row for a hang-up.</summary>
        public HomeRewardRuleRecord SelectRewardRule(int rewardRuleId, HomeConditionContext context)
        {
            if (!rewardRules.TryGetValue(rewardRuleId, out List<HomeRewardRuleRecord> rows)) return null;
            foreach (HomeRewardRuleRecord row in rows)
                if (IsMet(row.condition, context)) return row;
            return null;
        }

        /// <summary>Picks the weighted item of a reward rule row with a saved random state.</summary>
        public static int SelectRewardItem(HomeRewardRuleRecord row, ref ulong randomState)
        {
            if (row == null || row.ids == null || row.ids.Length == 0) return 0;
            int total = 0;
            int[] weights = row.weights ?? Array.Empty<int>();
            for (int index = 0; index < row.ids.Length; index++)
                total += Math.Max(0, index < weights.Length ? weights[index] : 1000);
            if (total > 0)
            {
                randomState = randomState == 0 ? 0x9e3779b97f4a7c15UL : randomState;
                randomState ^= randomState << 13;
                randomState ^= randomState >> 7;
                randomState ^= randomState << 17;
                int roll = (int)(randomState % (ulong)total);
                for (int index = 0; index < row.ids.Length; index++)
                {
                    roll -= Math.Max(0, index < weights.Length ? weights[index] : 1000);
                    if (roll < 0) return row.ids[index];
                }
            }
            return row.ids[0];
        }

        public bool IsFunctionLimited(int type, int key0 = 0, int key1 = 0)
        {
            foreach (HomeFunctionLimitRecord row in functionLimits)
            {
                if (row == null || row.type != type || row.key0 != key0 || row.key1 != key1) continue;
                if (row.condition != null && !IsMet(row.condition, CurrentContext)) return true;
            }
            return false;
        }

        /// <summary>Context updated by the runtime before each evaluation.</summary>
        public HomeConditionContext CurrentContext { get; set; } = new HomeConditionContext();

        public bool IsMet(HomeConditionNode node) => IsMet(node, CurrentContext);

        public bool IsMet(HomeConditionNode node, HomeConditionContext context)
        {
            if (node == null) return true;
            context = context ?? new HomeConditionContext();
            switch (node._type)
            {
                case null:
                case "":
                case "NoneCondition":
                case "AlwaysCondition":
                    return true;
                case "LevelCondition":
                    return Compare(node.Op, context.Level, node.Level);
                case "RankCondition":
                    return Compare(node.Op, context.Rank, node.Rank);
                case "SubRankCondition":
                    return Compare(node.Op, context.SubRank, node.SubRank);
                case "MaxProfessionRankCondition":
                    return Compare(node.Op, context.MaxProfessionRank, node.Rank);
                case "SubRankBetweenCondition":
                    return context.SubRank >= node.From && context.SubRank <= node.To;
                case "CompositeAndCondition":
                    return (node.Ids ?? Array.Empty<int>()).All(id => conditions.TryGetValue(id, out HomeConditionNode child) && IsMet(child, context));
                case "CompositeOrCondition":
                    return (node.Ids ?? Array.Empty<int>()).Any(id => conditions.TryGetValue(id, out HomeConditionNode child) && IsMet(child, context));
                case "LogicalNotCondition":
                    return !(conditions.TryGetValue(node.Id, out HomeConditionNode inner) && IsMet(inner, context));
                case "HasItemCondition":
                    return Compare(node.Op, context.ItemCount(node.ClassId), node.Count);
                case "HasItemTypeCondition":
                    return node.Count > 0 && context.HasItemOfType(node.ItemType);
                case "StoryCountCondition":
                    return Compare(node.Op, context.StoryCount(node.StoryKey), node.Count);
                case "StoryStatusCondition":
                    return context.StoryPlayed(node.StoryKey);
                case "MapUnlockCondition":
                    return context.UnlockedMapClassIds.Contains(node.MapClassId);
                case "InitialWeaponCondition":
                    return context.InitialWeaponType == node.WeaponType;
                case "PlayerProfessionCondition":
                case "PlayerProfessionFactionCondition":
                case "ProfessionUnlockCondition":
                    return context.Profession == node.Profession;
                case "AnyMapGoddessActivedCondition":
                    return context.AnyGoddessActive;
                case "HasManualMeetNpcCondition":
                    return context.HasManualMeetNpc;
                case "MapLocationCondition":
                case "MapRegionActivedCondition":
                case "MapRegionGoddessCondition":
                case "MapWorldActivedCondition":
                case "MapGoddessCondition":
                    return true; // Map progress is tracked by the world module; home unlock rows use level gates instead.
                default:
                    if (!unresolved.Contains(node)) unresolved.Add(node);
                    return false;
            }
        }

        public static bool Compare(string op, long actual, long expected)
        {
            switch (op)
            {
                case ">=": return actual >= expected;
                case ">": return actual > expected;
                case "<=": return actual <= expected;
                case "<": return actual < expected;
                case "==": return actual == expected;
                case "!=": return actual != expected;
                default: return actual >= expected;
            }
        }
    }
}
