using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BagRecycleQueries;

namespace Zjcs.Offline.BagRecycleQueries
{
    public sealed class OfflineBagRecycleCatalog : IBagRecycleQueryTables, IBagRecycleNpcTable,
        IBagRecycleNpcLevelTable, IBagRecycleItemTagTable, IBagRecycleWorldTagTable
    {
        [Serializable] public sealed class NpcRow { public int Id; public int[] ExclusiveGiftIds; }
        [Serializable] public sealed class LevelRow { public int Id, Level; }
        [Serializable] public sealed class ItemTagRow { public int Id; public string Tag; }
        [Serializable] public sealed class TagRow { public string Tag; public EntityRow[] Entities; }
        [Serializable] public sealed class EntityRow { public int MapClassId, Region, LoEntityId, EntityClassId; }
        [Serializable] public sealed class MapRow { public int ClassId; public bool IsDefaultPublicMap; public int[] Regions; }
        [Serializable] public sealed class Payload
        {
            public NpcRow[] Npcs; public LevelRow[] FriendshipLevels; public int[] GiftIds;
            public ItemTagRow[] ItemTags; public TagRow[] WorldTags; public MapRow[] Maps;
        }
        readonly Dictionary<int, int> exclusiveGifts = new Dictionary<int, int>();
        readonly Dictionary<int, int> maxLevels;
        readonly Dictionary<int, ItemTagInfo> itemTags;
        readonly Dictionary<string, List<WorldStageTagEntityInfo>> worldTags;
        readonly Dictionary<int, bool> publicDefaults;
        readonly Dictionary<int, int[]> regionIds;
        readonly int[] giftIds;
        public IReadOnlyDictionary<int, int> ExclusiveGifts => exclusiveGifts;
        public IReadOnlyCollection<ItemTagInfo> ItemTags => itemTags.Values;
        public object NpcGiftInfoParser => giftIds;
        public IBagRecycleNpcTable NpcInfoParser => this;
        public IBagRecycleNpcLevelTable NpcFriendshipLevelInfoParser => this;
        public IBagRecycleItemTagTable ItemTagInfoParser => this;
        public IBagRecycleWorldTagTable WorldStageTagMapInfoParser => this;

        public static OfflineBagRecycleCatalog LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/BagRecycleQueries/catalog");
            if (!asset) throw new InvalidOperationException("Original Bag recycling configuration is unavailable.");
            return new OfflineBagRecycleCatalog(JsonUtility.FromJson<Payload>(asset.text));
        }
        public OfflineBagRecycleCatalog(Payload data)
        {
            if (data?.Npcs == null || data.FriendshipLevels == null || data.GiftIds == null
                || data.ItemTags == null || data.WorldTags == null || data.Maps == null)
                throw new FormatException("Complete original Bag recycling configuration is required.");
            // NpcInfoParser.ProcessNpcExclusiveGift uses ordered Add, including its duplicate failure.
            foreach (var row in data.Npcs)
                foreach (int id in row.ExclusiveGiftIds) exclusiveGifts.Add(id, row.Id);
            // Original two-key table, then NpcFriendshipLevelInfoParser.PostParse.
            maxLevels = data.FriendshipLevels.GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.Level).Keys.Max());
            giftIds = data.GiftIds;
            itemTags = data.ItemTags.ToDictionary(row => row.Id, row => new ItemTagInfo { Id = row.Id, Tag = row.Tag });
            worldTags = data.WorldTags.ToDictionary(row => row.Tag, row => row.Entities == null ? null
                : row.Entities.Select(value => new WorldStageTagEntityInfo { MapClassId = value.MapClassId, Region = value.Region,
                    LoEntityId = value.LoEntityId, EntityClassId = value.EntityClassId }).ToList());
            publicDefaults = data.Maps.ToDictionary(row => row.ClassId, row => row.IsDefaultPublicMap);
            regionIds = data.Maps.ToDictionary(row => row.ClassId, row => row.Regions.OrderBy(id => id).ToArray());
        }
        public bool TryGetNpcByExclusiveGift(int giftId, out int npcId) => exclusiveGifts.TryGetValue(giftId, out npcId);
        public int GetMaxLevel(int id) => maxLevels.TryGetValue(id, out int value) ? value : 0;
        public bool TryGet(int id, out ItemTagInfo config) => itemTags.TryGetValue(id, out config);
        public List<WorldStageTagEntityInfo> GetWorldStageTagEntityInfos(string tag)
            => worldTags.TryGetValue(tag, out var value) ? value : null;
        public bool IsDefaultPublicMap(int id) => publicDefaults.TryGetValue(id, out bool value) ? value
            : throw new InvalidOperationException("Bind the original region-status owner for map " + id);
        public IReadOnlyList<int> RegionIds(int id) => regionIds.TryGetValue(id, out var value) ? value
            : throw new InvalidOperationException("Original region configuration is unbound for map " + id);
    }
}
