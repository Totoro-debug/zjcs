using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    public enum OfflineInventorySort
    {
        Type,
        Quality,
        GetTime
    }

    public enum OfflineInventoryDetailKind
    {
        Item,
        Equipment,
        Gem,
        Skill,
        SkillPiece,
        EquipmentPiece,
        ItemPiece,
        RandomTreasurePiece,
        Treasure,
        TreasurePiece,
        Cooking,
        Pet,
        PetSkill,
        Appearance,
        AppearanceChest,
        HomeDecoration,
        HangUpMaterial,
        SkillSkin,
        WrapItem,
        AttributeReplace,
        CookingFormula,
        TreasureChest,
        RandomTreasureChest,
        SkillPieceChest,
        DelegateTask,
        FastHangUp,
        ResetHangUp,
        VoidFissureTimes,
        PetPieceMerge,
        UseItem
    }

    public sealed class OfflineInventoryItemView
    {
        public int ItemId { get; internal set; }
        public int ItemType { get; internal set; }
        public int Quality { get; internal set; }
        public long Count { get; internal set; }
        public long ExpiredCount { get; internal set; }
        public long InstanceId { get; internal set; }
        public long AcquiredAtUnixSeconds { get; internal set; }
        public long ExpiresAtUnixSeconds { get; internal set; }
        public string NameKey { get; internal set; }
        public string IconUri { get; internal set; }
        public bool IsEquipped { get; internal set; }
        public bool IsTimed { get; internal set; }
        public OfflineInventoryDetailKind DetailKind { get; internal set; }
    }

    public static class OfflineInventoryCatalog
    {
        public static IReadOnlyList<OfflineInventoryItemView> Query(OfflineMainlineSave save,
            OfficialEquipmentCatalog catalog, IReadOnlyList<int> tabTypes, OfflineInventorySort sort,
            bool reverse, long nowUnixSeconds)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Player == null || save.Player.Inventory == null || save.Player.Equipment == null)
                throw new ArgumentException("Player inventory data is invalid.", nameof(save));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (tabTypes == null) throw new ArgumentNullException(nameof(tabTypes));

            var typePriority = new Dictionary<int, int>();
            for (int index = 0; index < tabTypes.Count; index++)
                if (!typePriority.ContainsKey(tabTypes[index])) typePriority.Add(tabTypes[index], index);

            var result = new List<OfflineInventoryItemView>();
            foreach (OfflineItemStack stack in save.Player.Inventory)
            {
                Dictionary<string, object> item = catalog.Find("item", stack.ItemId);
                if (item == null || !Bool(item, "ShowInBag")) continue;
                int itemType = Int(item, "ItemType");
                if (!typePriority.ContainsKey(itemType) || IsType(catalog, "EquipTypes", itemType)) continue;
                if (stack.Count <= 0) continue;
                IEnumerable<OfflineItemUnitRecord> records = stack.UnitRecords == null
                    ? Enumerable.Empty<OfflineItemUnitRecord>() : stack.UnitRecords;
                long acquired = records.Select(value => value.AcquiredAtUnixSeconds).DefaultIfEmpty(0).Max();
                long expiry = records.Where(value => value.ExpiresAtUnixSeconds > 0)
                    .Select(value => value.ExpiresAtUnixSeconds).DefaultIfEmpty(0).Min();
                result.Add(new OfflineInventoryItemView
                {
                    ItemId = stack.ItemId,
                    ItemType = itemType,
                    Quality = Int(item, "Quality"),
                    Count = stack.Count,
                    ExpiredCount = OfflineInventoryOperations.ExpiredCount(stack, nowUnixSeconds),
                    NameKey = Text(item, "Name"),
                    IconUri = Text(item, "RawIcon"),
                    AcquiredAtUnixSeconds = acquired,
                    ExpiresAtUnixSeconds = expiry,
                    IsTimed = IsTimed(item) || expiry > 0,
                    DetailKind = DetailKind(catalog, item, itemType)
                });
            }

            foreach (OfflineEquipmentInstance equipment in save.Player.Equipment.Instances)
            {
                Dictionary<string, object> item = catalog.Find("item", equipment.ItemId);
                if (item == null || !Bool(item, "ShowInBag")) continue;
                int itemType = Int(item, "ItemType");
                if (!IsType(catalog, "EquipTypes", itemType) || !typePriority.ContainsKey(itemType)) continue;
                result.Add(new OfflineInventoryItemView
                {
                    ItemId = equipment.ItemId,
                    ItemType = itemType,
                    Quality = Int(item, "Quality"),
                    Count = 1,
                    NameKey = Text(item, "Name"),
                    IconUri = Text(item, "RawIcon"),
                    InstanceId = equipment.InstanceId,
                    IsEquipped = save.Player.Equipment.Equipped.Any(slot => slot.InstanceId == equipment.InstanceId),
                    IsTimed = IsTimed(item),
                    DetailKind = OfflineInventoryDetailKind.Equipment
                });
            }

            Comparison<OfflineInventoryItemView> comparer = (left, right) => Compare(left, right,
                typePriority, sort, reverse, nowUnixSeconds);
            result.Sort(comparer);
            return result;
        }

        public static OfflineInventoryDetailKind DetailKind(OfficialEquipmentCatalog catalog,
            Dictionary<string, object> item, int itemType)
        {
            if (item == null) return OfflineInventoryDetailKind.Item;
            if (IsType(catalog, "EquipTypes", itemType)) return OfflineInventoryDetailKind.Equipment;
            if (itemType == 309) return OfflineInventoryDetailKind.Gem;
            if (IsType(catalog, "SkillTypes", itemType) || IsType(catalog, "MonsterSkillTypes", itemType))
                return OfflineInventoryDetailKind.Skill;
            if (IsType(catalog, "SkillPieceTypes", itemType)) return OfflineInventoryDetailKind.SkillPiece;
            switch (itemType)
            {
                case 334: return OfflineInventoryDetailKind.ItemPiece;
                case 326: return OfflineInventoryDetailKind.EquipmentPiece;
                case 340: return OfflineInventoryDetailKind.RandomTreasurePiece;
                case 305: return OfflineInventoryDetailKind.Treasure;
                case 306: return OfflineInventoryDetailKind.TreasurePiece;
                case 323: return OfflineInventoryDetailKind.Cooking;
                case 404: return OfflineInventoryDetailKind.PetSkill;
                case 400: return OfflineInventoryDetailKind.Pet;
                case 405: return OfflineInventoryDetailKind.PetPieceMerge;
                case 339: return OfflineInventoryDetailKind.Appearance;
                case 352: return OfflineInventoryDetailKind.HangUpMaterial;
                case 425: return OfflineInventoryDetailKind.SkillSkin;
                case 50: return OfflineInventoryDetailKind.WrapItem;
                case 434: return OfflineInventoryDetailKind.AttributeReplace;
                case 322: return OfflineInventoryDetailKind.CookingFormula;
                case 345: return OfflineInventoryDetailKind.ResetHangUp;
                case 315: return OfflineInventoryDetailKind.FastHangUp;
                case 341: return OfflineInventoryDetailKind.RandomTreasureChest;
                case 342: return OfflineInventoryDetailKind.TreasureChest;
                case 343: return OfflineInventoryDetailKind.SkillPieceChest;
            }

            if (IsType(catalog, "AppearanceChestTypes", itemType)) return OfflineInventoryDetailKind.AppearanceChest;
            if (IsType(catalog, "AppearanceTypes", itemType)) return OfflineInventoryDetailKind.Appearance;
            if (IsType(catalog, "HomeDecorationTypes", itemType)) return OfflineInventoryDetailKind.HomeDecoration;
            if (!Bool(item, "CanUse")) return OfflineInventoryDetailKind.Item;
            switch (Int(item, "ItemUseEffectType"))
            {
                case 4: return OfflineInventoryDetailKind.DelegateTask;
                case 6: return OfflineInventoryDetailKind.VoidFissureTimes;
                case 7: return OfflineInventoryDetailKind.CookingFormula;
                case 8: return OfflineInventoryDetailKind.Cooking;
                case 9: return OfflineInventoryDetailKind.EquipmentPiece;
                case 10: return OfflineInventoryDetailKind.ItemPiece;
                case 11: return OfflineInventoryDetailKind.PetPieceMerge;
                case 13: return OfflineInventoryDetailKind.RandomTreasureChest;
                case 14: return OfflineInventoryDetailKind.RandomTreasurePiece;
                case 15: return OfflineInventoryDetailKind.TreasureChest;
                case 16: return OfflineInventoryDetailKind.SkillPieceChest;
                case 17: return OfflineInventoryDetailKind.ResetHangUp;
                default: return OfflineInventoryDetailKind.UseItem;
            }
        }

        private static int Compare(OfflineInventoryItemView left, OfflineInventoryItemView right,
            IReadOnlyDictionary<int, int> typePriority, OfflineInventorySort sort, bool reverse, long nowUnixSeconds)
        {
            int result;
            if (sort != OfflineInventorySort.GetTime)
            {
                result = CompareScore(left.IsEquipped ? 1 : 0, right.IsEquipped ? 1 : 0, true);
                if (result != 0) return result;
            }
            result = CompareScore(left.IsTimed ? 1 : 0, right.IsTimed ? 1 : 0, true);
            if (result != 0) return result;

            if (sort == OfflineInventorySort.Quality)
            {
                result = CompareScore(left.Quality, right.Quality, !reverse);
                if (result != 0) return result;
                result = CompareScore(-typePriority[left.ItemType], -typePriority[right.ItemType], true);
            }
            else if (sort == OfflineInventorySort.GetTime)
            {
                result = CompareScore(left.AcquiredAtUnixSeconds, right.AcquiredAtUnixSeconds, !reverse);
            }
            else
            {
                result = CompareScore(-typePriority[left.ItemType], -typePriority[right.ItemType], !reverse);
                if (result != 0) return result;
                result = CompareScore(left.Quality, right.Quality, true);
            }
            if (result != 0) return result;
            return CompareScore(left.ItemId, right.ItemId, true);
        }

        private static int CompareScore(long left, long right, bool descending)
        {
            int result = left.CompareTo(right);
            return descending ? -result : result;
        }

        private static bool IsTimed(Dictionary<string, object> item) =>
            item.TryGetValue("ItemTimeLimitInfo", out object value) && value != null;

        private static bool IsType(OfficialEquipmentCatalog catalog, string key, int itemType)
        {
            if (catalog == null || !catalog.GameSettings.TryGetValue(key, out object raw) || raw == null) return false;
            if (raw is List<object> values)
                return values.Any(value => Convert.ToInt32(value, CultureInfo.InvariantCulture) == itemType);
            if (raw is Dictionary<string, object> types)
                return types.Keys.Any(value => int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int parsed) && parsed == itemType);
            return false;
        }

        private static bool Bool(Dictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object result) && result != null
                && Convert.ToBoolean(result, CultureInfo.InvariantCulture);

        private static int Int(Dictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object result) && result != null
                ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;

        private static string Text(Dictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object result) ? Convert.ToString(result, CultureInfo.InvariantCulture) : string.Empty;

    }
}
