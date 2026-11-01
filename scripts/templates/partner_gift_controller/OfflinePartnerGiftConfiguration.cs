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
            /*TAG_RANGE*/
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
        /*CHARACTER_CHECK*/
        /*TAG_CHECK*/
        /*CHARACTER_LENGTH*/
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
            /*LEVEL_METHODS*/
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
            /*GIFT_METHOD*/
        }
        private sealed class OriginalTags
        {
            private readonly Dictionary<int, TagRecord> configs;
            public Dictionary<int, List<TagRecord>> RoleType2TagInfoDict;
            internal OriginalTags(Dictionary<int, TagRecord> rows) { configs = rows; PostParse(configs); }
            /*TAG_METHODS*/
        }
    }
}
