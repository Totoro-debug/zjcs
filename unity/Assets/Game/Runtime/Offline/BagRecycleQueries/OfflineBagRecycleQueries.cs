using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.BagItemQueries;
using Zjcs.Offline.Progression;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagRecycleQueries;
using Zjcs.Official.BagTipDots;

namespace Zjcs.Offline.BagRecycleQueries
{
    // A current-save reader only. Recycle transactions and rewards remain owned
    // by the inventory feature; these original queries never consume an item.
    public sealed class OfflineBagRecycleQueries : IBagRecycleQueryServices, IBagRecycleQueryPlayer, IBagRecycleNpcManager
    {
        readonly Func<OfflineMainlineSave> readSave;
        readonly OfflineBagRecycleCatalog catalog;
        readonly OfflineBagItemQueries items;
        readonly BagRecycleQueryRules rules;
        public IBagRecycleQueryTables Tables => catalog;
        // Existing independent local world-content rank, never personal rank/season.
        public int PlanesRank => readSave().Player.CharacterProperties.TreasurePlaneRank;
        public IBagRecycleNpcManager NpcManager => this;
        public OfflineBagRecycleQueries(Func<OfflineMainlineSave> readSave, OfflineBagRecycleCatalog catalog,
            OfflineBagItemQueryCatalog itemCatalog)
        {
            this.readSave = readSave ?? throw new ArgumentNullException(nameof(readSave));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            items = new OfflineBagItemQueries(readSave, itemCatalog);
            rules = new BagRecycleQueryRules(this);
        }
        public List<PlayerItemDataWrap> GetCanRecycleGift() => rules.GetCanRecycleGift(this);
        public List<PlayerItemDataWrap> GetCanRecycleDoorkey() => rules.GetCanRecycleDoorkey(this);
        public List<PlayerItemDataWrap> GetCanRecycleGem() => rules.GetCanRecycleGem(this);
        public bool IsTagInAllRegionActived(string tag) => rules.IsTagInAllRegionActived(this, tag);
        public IReadOnlyCollection<PlayerItemDataWrap> FindItemsByItemType(ItemType type) => FindItemsByType(type);
        public IReadOnlyCollection<PlayerItemDataWrap> FindItemsByType(ItemType type)
        {
            var values = items.FindItemsByType(new[] { type });
            if (type == ItemType.Gem)
                foreach (var value in values) value.Info.PartInfo = catalog.GetGemPartInfo(value.Id);
            return values;
        }
        public PlayerNpcDataWrap GetByNpcClassId(int npcClassId)
        {
            var row = readSave().Player.CharacterProperties.NpcFriendships.FirstOrDefault(value => value.NpcClassId == npcClassId);
            return row == null ? null : new PlayerNpcDataWrap { NpcClassId = row.NpcClassId, FriendShipLevel = row.Level };
        }
        public bool RegionIsActived(int mapClassId, int region)
        {
            var world = readSave().World;
            var map = world.Maps.FirstOrDefault(value => value.MapClassId == mapClassId);
            // Player.GetMapRegionStatus: uninitialized/out-of-range is None.
            // A visited map initializes all regions with MapDefaultRegionStatus;
            // saved goddess activations then restore its explicit Public entries.
            if (region < 0 || map == null || region >= map.Regions.Count) return false;
            if (!map.Regions.Select(value => value.Region).OrderBy(id => id).SequenceEqual(catalog.RegionIds(mapClassId)))
                throw new InvalidOperationException("Saved region coverage differs from the original visited map: " + mapClassId);
            return catalog.IsDefaultPublicMap(mapClassId) || world.ActivatedGoddessRegions
                .Any(value => value.MapClassId == mapClassId && value.Region == region);
        }
    }
}
