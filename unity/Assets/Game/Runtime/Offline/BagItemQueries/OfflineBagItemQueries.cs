using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Progression;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagTipDots;
using QueryItem = Zjcs.Official.BagItemQueries.PlayerItemDataWrap;

namespace Zjcs.Offline.BagItemQueries
{
    public static class OfflineBagItemIndicatorFactory
    {
        public static IOfflineInventoryItemIndicators Create(Func<OfflineMainlineSave> readSave)
            => new OfflineBagItemQueries(readSave, OfflineBagItemQueryCatalog.LoadDefault());
    }

    // Reads the current profile for every query. Transactions replace Save; no
    // query reloads, normalizes, saves or creates another inventory/payment state.
    public sealed class OfflineBagItemQueries : IOfflineInventoryItemIndicators, IBagItemQueryServices, IBagItemQueryPlayer
    {
        readonly Func<OfflineMainlineSave> readSave;
        readonly Func<DateTime> utcNow;
        readonly OfflineBagItemQueryCatalog catalog;
        readonly BagItemQueryRules rules;
        OfflinePlayerProgress PlayerData => readSave()?.Player ?? throw new InvalidOperationException("The selected local player is unavailable.");
        public IBagItemQuerySettings Settings => catalog;
        public IBagItemQueryTables Tables => catalog;
        public IBagItemQueryPlayer Player => this;
        public OfflineBagItemQueryCatalog Catalog => catalog;

        public OfflineBagItemQueries(Func<OfflineMainlineSave> readSave, OfflineBagItemQueryCatalog catalog, Func<DateTime> utcNow = null)
        {
            this.readSave = readSave ?? throw new ArgumentNullException(nameof(readSave));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            rules = new BagItemQueryRules(this);
        }
        public bool CanUseRedDot(int itemId, long instanceId, long count) => rules.ShowBagCanUseItemRedDot(Project(itemId, instanceId, count));
        public int GetRedDotType(int itemId, long instanceId, long count) => (int)rules.GetItemRedDotType(Project(itemId, instanceId, count));
        public long GetBlueDotCount(int itemId, long instanceId, long count) => rules.GetCanUseBlueDotCount(Project(itemId, instanceId, count));
        public bool HasItem(int itemId, long count) => PlayerData.Inventory.Where(row => row.ItemId == itemId).Sum(row => row.Count) >= count;
        public bool HasPet(int classId) => PlayerData.Pets.Pets.Any(row => row.ClassId == classId);
        public DateTime GetDateTime() => utcNow();

        public bool TryGetBuyCount(PayType type, int id, DateTime time, out int count)
        {
            if (type != PayType.PeriodicCard) throw new InvalidOperationException("This query only consumes original periodic-card purchase state.");
            // PlayerPayItemManager's absent entry succeeds with a zero count.
            // KeepDays uses the owner's current clock rather than its argument.
            var player = PlayerData;
            if (!OfflineCommerceSession.TryReadPeriodicCardExpiry(player, id, out long expires))
                expires = player.CharacterProperties.PeriodicCards.FirstOrDefault(row => row.CardId == id)?.ExpiresAtUnixSeconds ?? 0;
            DateTime now = utcNow();
            if (expires <= new DateTimeOffset(now).ToUnixTimeSeconds()) { count = 0; return true; }
            DateTime end = DateTimeOffset.FromUnixTimeSeconds(expires).LocalDateTime;
            count = (int)(OfflineRefreshClock.DayStart(end) - OfflineRefreshClock.DayStart(now.ToLocalTime())).TotalDays;
            return true;
        }
        public bool IsLimit(CookingInfo cooking)
        {
            var row = catalog.Cooking(cooking.Id);
            // CookingInfo.IsLimit is the negation of the condition, not IsMet.
            switch (row.ConditionType)
            {
                case "AlwaysCondition": case "NoneCondition": return false;
                case "MapUnlockCondition": return !PlayerData.UnlockedMapClassIds.Contains(row.MapClassId);
                case "UsedItemCondition": return !PlayerData.AppliedInventoryEventIds.Contains("cooking:used-item:" + row.UseSourceOperate + ":" + row.ConditionItemId);
                default: throw new InvalidOperationException("Bind the original cooking condition: " + row.ConditionType);
            }
        }
        public IReadOnlyCollection<QueryItem> FindItemsByType(IReadOnlyCollection<ItemType> types)
        {
            var equipmentTypes = new HashSet<ItemType>(catalog.EquipTypes);
            var player = PlayerData;
            // PlayerItemManagerExt.FindItemsByType appends each type's list in
            // caller order. Repeated types deliberately repeat their records.
            return types.SelectMany(type => player.Inventory
                .Where(row => (ItemType)catalog.Item(row.ItemId).ItemType == type && !equipmentTypes.Contains(type))
                .Select(row => Project(row.ItemId, 0, row.Count))
                .Concat(player.Equipment.Instances.Where(row => (ItemType)catalog.Item(row.ItemId).ItemType == type)
                    .Select(row => Project(row.ItemId, row.InstanceId, 1)))).ToArray();
        }
        QueryItem Project(int itemId, long instanceId, long count)
        {
            var row = catalog.Item(itemId);
            var item = new QueryItem { Id = itemId, Count = count, Info = new BagQueryItemInfo {
                ClassId = itemId, ItemType = (ItemType)row.ItemType, Quality = row.Quality,
                RedDotType = (RedDotType)row.RedDotType, PartInfo = catalog.CardItem(itemId) } };
            if (instanceId > 0)
            {
                var equipment = PlayerData.Equipment.Instances.FirstOrDefault(value => value.InstanceId == instanceId && value.ItemId == itemId)
                    ?? throw new InvalidOperationException("The selected equipment instance is unavailable.");
                item.Equip = new PlayerItemParamEquipWrap {
                    PurifyProps = equipment.PurifyProps.Select(value => (object)value).ToArray(),
                    OtherProps = new BagQueryEquipProps { RawData = equipment.Terms.Select((value, index) => new { value, index })
                        .ToDictionary(value => value.index, value => new EquipPropBase { PropId = value.value.PropertyId }) } };
            }
            return item;
        }
    }
}
