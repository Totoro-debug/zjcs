// Official parser bodies; see partner-gift-configuration-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;

namespace Zjcs.Offline.PartnersUI
{
    // Typed projections of the selected tables; algorithms below are generated
    // from the official parsers, rather than inferred from the current values.
    public sealed class OfflinePartnerGiftConfiguration : IPartnerGiftLevels, IPartnerGiftExperience
    {
        internal sealed class Lookup<T> : Dictionary<int, T> where T : class
        {
            public T Get(int id) => TryGetValue(id, out var value) ? value : null;
        }
        internal sealed class NpcRecord
        {
            public bool CanDevelop;
            public PartnerGiftNpcInfo Info;
        }
        internal sealed class ItemRecord { public int ItemType; }
        internal sealed class UnlockRecord { public bool CanSendGift; }
        internal sealed class LevelRecord
        {
            public long FriendshipExp;
            public int SubRankLimit;
        }
        internal sealed class TagRecord
        {
            public int ClassId;
            public List<int> NpcRoleTypes, LevelRanges;
            public string TagBg;
                public bool InRangeLevel(int level)
    {
        if (LevelRanges.Count != 2)
        {
            return false;
        }
        if (level >= LevelRanges[0])
        {
            return level <= LevelRanges[1];
        }
        return false;
    }
        }
        private readonly Lookup<NpcRecord> npcs = new Lookup<NpcRecord>();
        private readonly Lookup<PartnerGiftInfo> gifts = new Lookup<PartnerGiftInfo>();
        private readonly Lookup<ItemRecord> items = new Lookup<ItemRecord>();
        private readonly Lookup<UnlockRecord> unlocks = new Lookup<UnlockRecord>();
        private readonly Dictionary<int, Dictionary<int, PartnerGiftLevelInfo>> displayLevels = new Dictionary<int, Dictionary<int, PartnerGiftLevelInfo>>();
        private readonly HashSet<char> validCharacters;
        private readonly OriginalLevels levels;
        private readonly OriginalGifts experience;
        private readonly OriginalTags tags;
        private readonly int customTagMaxLength;
        public double NpcGiftDefaultPreferenceFactor { get; }
        public int CanCustomTagFriendShipLevel { get; }

        public OfflinePartnerGiftConfiguration(OfficialPartnerCatalog catalog, string characters)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (string.IsNullOrEmpty(characters)) throw new ArgumentException("Official custom-content character asset is unbound.", nameof(characters));
            validCharacters = new HashSet<char>(characters);
            var settings = catalog.Rows("game_settings").Single();
            NpcGiftDefaultPreferenceFactor = Convert.ToDouble(settings["NpcGiftDefaultPreferenceFactor"], CultureInfo.InvariantCulture);
            var tagSettings = (Dictionary<string, object>)settings["NpcFriendshipTagSettings"];
            customTagMaxLength = Int(tagSettings, "CustomTagMaxLength");
            CanCustomTagFriendShipLevel = Int(tagSettings, "CanCustomTagFriendShipLevel");
            foreach (var row in catalog.Rows("npc"))
            {
                int id = Int(row, "Id");
                npcs.Add(id, new NpcRecord { CanDevelop = (bool)row["CanDevelop"], Info = new PartnerGiftNpcInfo {
                    Id = id, NpcRoleType = Int(row, "NpcRoleType"), IsStarGodNpc = Int(row, "NpcRoleType") == 2,
                    CanCustomFriendshipTag = (bool)row["CanCustomFriendshipTag"],
                    Properties = new PartnerFriendshipNpcInfo { Id = id, LevelPropId = Int(row, "LevelPropId") },
                    GiftPreferenceDict = row["GiftPreferenceDict"] is Dictionary<string, object> preferences
                        ? preferences.ToDictionary(value => int.Parse(value.Key, CultureInfo.InvariantCulture), value => Convert.ToDouble(value.Value, CultureInfo.InvariantCulture)) : null
                }});
            }
            foreach (var row in catalog.Rows("item")) items.Add(Int(row, "ClassId"), new ItemRecord { ItemType = Int(row, "ItemType") });
            foreach (var row in catalog.Rows("npc_gift")) gifts.Add(Int(row, "ClassId"), new PartnerGiftInfo { ClassId = Int(row, "ClassId"), Friendship = Int(row, "Friendship") });
            foreach (var row in catalog.Rows("npc_unlock_item")) unlocks.Add(Int(row, "ClassId"), new UnlockRecord { CanSendGift = (bool)row["CanSendGift"] });
            var levelRows = new Dictionary<int, Dictionary<int, LevelRecord>>();
            foreach (var row in catalog.Rows("npc_friendship_level"))
            {
                int id = Int(row, "Id"), level = Int(row, "Level");
                if (!levelRows.TryGetValue(id, out var group)) { group = new Dictionary<int, LevelRecord>(); levelRows.Add(id, group); displayLevels.Add(id, new Dictionary<int, PartnerGiftLevelInfo>()); }
                long exp = Convert.ToInt64(row["FriendshipExp"], CultureInfo.InvariantCulture);
                group.Add(level, new LevelRecord { FriendshipExp = exp, SubRankLimit = Int(row, "SubRankLimit") });
                displayLevels[id].Add(level, new PartnerGiftLevelInfo { FriendshipExp = exp });
            }
            levels = new OriginalLevels(levelRows, npcs);
            experience = new OriginalGifts(items, gifts, unlocks, NpcGiftDefaultPreferenceFactor);
            var tagRows = new Dictionary<int, TagRecord>();
            foreach (var row in catalog.Rows("npc_friendship_tag")) tagRows.Add(Int(row, "ClassId"), new TagRecord {
                ClassId = Int(row, "ClassId"), NpcRoleTypes = Integers(row["NpcRoleTypes"]), LevelRanges = Integers(row["LevelRanges"]), TagBg = (string)row["TagBg"] });
            tags = new OriginalTags(tagRows);
        }
        public PartnerGiftNpcInfo GetNpcInfo(int id) => npcs.Get(id)?.Info;
        public PartnerGiftInfo GetGiftInfo(int id) => gifts.Get(id);
        // ItemInfo.IsNpcGift and IsNpcUnlockItem are enum comparisons in the original client.
        public bool IsNpcGift(int itemId) => items.Get(itemId)?.ItemType == 328;
        public PartnerGiftTagInfo GetTagInfoByRoleTypeAndLevel(int role, int level)
        {
            var value = tags.GetTagInfoByRoleTypeAndLevel(role, level);
            return value == null ? null : new PartnerGiftTagInfo { ClassId = value.ClassId, TagBg = value.TagBg };
        }
        public int GetMaxLevel(int id) => levels.GetMaxLevel(id);
        public int GetMaxSubRankLevel(int id, int rank) => levels.GetMaxSubRankLevel(id, rank);
        public int GetFriendshipLevelByExp(int id, long exp, int rank) => levels.GetFriendshipLevelByExp(id, exp, rank);
        public PartnerGiftLevelInfo GetMaxLevelInfo(int id) => Get(id, GetMaxLevel(id));
        public IReadOnlyDictionary<int, PartnerGiftLevelInfo> Get(int id) => displayLevels.TryGetValue(id, out var rows) ? rows : null;
        public PartnerGiftLevelInfo Get(int id, int level) => displayLevels.TryGetValue(id, out var rows) && rows.TryGetValue(level, out var value) ? value : null;
        public int GetAllExpByDict(Dictionary<int, long> values, Dictionary<int, double> preferences) => experience.GetAllExpByDict(values, preferences);
            public bool IsAllCharacterValid(string content)
    {
        HashSet<char> andTryRefreshCharactersSet = validCharacters;
        foreach (char item in content)
        {
            if (!andTryRefreshCharactersSet.Contains(item))
            {
                return false;
            }
        }
        return true;
    }
            public int CheckNpcFriendshipTagLegal(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 1513;
        }
        if (IsOverCharacterCountLimit(name, customTagMaxLength))
        {
            return 1514;
        }
        return 0;
    }
            public bool IsOverCharacterCountLimit(string str, int limitCount)
    {
        return GetCharacterCount(str) > limitCount;
    }

    public int GetCharacterCount(string str)
    {
        return Common.CJK.GetByteCount(str);
    }
        private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);
        private static List<int> Integers(object value) => value is List<object> rows ? rows.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToList() : null;

        private sealed class OriginalLevels
        {
            private readonly Dictionary<int, Dictionary<int, LevelRecord>> configs;
            private readonly Lookup<NpcRecord> npcs;
            private readonly Dictionary<int, int> mMaxLevelDict = new Dictionary<int, int>();
            private readonly Dictionary<int, Dictionary<int, List<int>>> mSubRankLevelDict = new Dictionary<int, Dictionary<int, List<int>>>();
            internal OriginalLevels(Dictionary<int, Dictionary<int, LevelRecord>> rows, Lookup<NpcRecord> npcs)
            { configs = rows; this.npcs = npcs; PostParse(); }
            private Dictionary<int, LevelRecord> Get(int id) => configs.TryGetValue(id, out var rows) ? rows : null;
                public void PostParse()
    {

        foreach (KeyValuePair<int, Dictionary<int, LevelRecord>> config in configs)
        {
            mMaxLevelDict[config.Key] = config.Value.Keys.Max();
            foreach (KeyValuePair<int, LevelRecord> item in config.Value)
            {
                if (!mSubRankLevelDict.TryGetValue(config.Key, out var value))
                {
                    value = new Dictionary<int, List<int>>();
                    mSubRankLevelDict.Add(config.Key, value);
                }
                if (!value.TryGetValue(item.Value.SubRankLimit, out var value2))
                {
                    value2 = new List<int>();
                    value.Add(item.Value.SubRankLimit, value2);
                }
                value2.Add(item.Key);
            }
        }
    }

    public int GetMaxLevel(int id)
    {
        mMaxLevelDict.TryGetValue(id, out var value);
        return value;
    }

    public int GetMaxSubRankLevel(int id, int subRank)
    {
        NpcRecord npcInfo = npcs.Get(id);
        if (npcInfo == null)
        {
            return 0;
        }
        if (!npcInfo.CanDevelop)
        {
            return 0;
        }
        return GetSubRankLevelList(id, subRank)?.Max() ?? 0;
    }

    public List<int> GetSubRankLevelList(int id, int subRank)
    {
        if (!mSubRankLevelDict.TryGetValue(id, out var value))
        {
            return null;
        }
        List<int> list = null;
        int subRank2 = 0;
        foreach (KeyValuePair<int, List<int>> item in value)
        {
            if (subRank >= item.Key && item.Key >= subRank2)
            {
                subRank2 = item.Key;
                list = item.Value;
            }
        }
        if (list == null)
        {
        }
        return list;
    }

    public int GetFriendshipLevelByExp(int id, long friendshipExp, int subRank)
    {
        Dictionary<int, LevelRecord> dictionary = Get(id);
        if (dictionary == null)
        {
            return 0;
        }
        int num = 0;
        foreach (KeyValuePair<int, LevelRecord> item in dictionary)
        {
            if (item.Value.FriendshipExp <= friendshipExp && item.Key > num && item.Value.SubRankLimit <= subRank)
            {
                num = item.Key;
            }
        }
        return num;
    }
        }
        private sealed class OriginalGifts
        {
            private readonly Lookup<ItemRecord> items;
            private readonly Lookup<PartnerGiftInfo> gifts;
            private readonly Lookup<UnlockRecord> unlocks;
            private readonly double defaultPreference;
            internal OriginalGifts(Lookup<ItemRecord> items, Lookup<PartnerGiftInfo> gifts, Lookup<UnlockRecord> unlocks, double factor)
            { this.items = items; this.gifts = gifts; this.unlocks = unlocks; defaultPreference = factor; }
            private PartnerGiftInfo Get(int id) => gifts.Get(id);
                public int GetAllExpByDict(Dictionary<int, long> giftDict, Dictionary<int, double> giftPreferenceDict)
    {
        int num = 0;
        double npcGiftDefaultPreferenceFactor = defaultPreference;
        var itemInfoParser = items;
        var npcUnlockItemInfoParser = unlocks;
        foreach (KeyValuePair<int, long> item in giftDict)
        {
            ItemRecord itemInfo = itemInfoParser.Get(item.Key);
            if (itemInfo == null)
            {
                return 0;
            }
            if (itemInfo.ItemType == 433)
            {
                UnlockRecord npcUnlockItemInfo = npcUnlockItemInfoParser.Get(item.Key);
                if (npcUnlockItemInfo == null || !npcUnlockItemInfo.CanSendGift)
                {
                    continue;
                }
            }
            PartnerGiftInfo npcGiftInfo = Get(item.Key);
            if (npcGiftInfo != null)
            {
                double num2 = npcGiftDefaultPreferenceFactor;
                if (giftPreferenceDict.TryGetValue(item.Key, out var value))
                {
                    num2 = value;
                }
                num += (int)Math.Round((double)(npcGiftInfo.Friendship * item.Value) * num2);
            }
        }
        return num;
    }
        }
        private sealed class OriginalTags
        {
            private readonly Dictionary<int, TagRecord> configs;
            public Dictionary<int, List<TagRecord>> RoleType2TagInfoDict;
            internal OriginalTags(Dictionary<int, TagRecord> rows) { configs = rows; PostParse(configs); }
                private void PostParse(Dictionary<int, TagRecord> configs)
    {

        RoleType2TagInfoDict = new Dictionary<int, List<TagRecord>>();
        foreach (TagRecord value2 in configs.Values)
        {
            if (value2.NpcRoleTypes == null || value2.NpcRoleTypes.Count == 0)
            {
                continue;
            }
            foreach (int npcRoleType in value2.NpcRoleTypes)
            {
                if (!RoleType2TagInfoDict.TryGetValue(npcRoleType, out var value))
                {
                    value = new List<TagRecord>();
                    RoleType2TagInfoDict.Add(npcRoleType, value);
                }
                value.Add(value2);
            }
        }
    }

    private List<TagRecord> GetTagInfoByRoleType(int roleType)
    {
        if (RoleType2TagInfoDict != null && RoleType2TagInfoDict.TryGetValue(roleType, out var value))
        {
            return value;
        }
        return null;
    }

    public TagRecord GetTagInfoByRoleTypeAndLevel(int roleType, int level)
    {
        List<TagRecord> tagInfoByRoleType = GetTagInfoByRoleType(roleType);
        if (tagInfoByRoleType == null)
        {
            return null;
        }
        foreach (TagRecord item in tagInfoByRoleType)
        {
            if (item.InRangeLevel(level))
            {
                return item;
            }
        }
        return null;
    }
        }
    }
}
