using System;
using System.Collections.Generic;
using Zjcs.Official.BagTipDots;

namespace Zjcs.Official.BagItemQueries
{
    // Read-only projections of the original query inputs. The local inventory,
    // equipment, pet, cooking and payment owners retain their own transactions.
    public sealed class PlayerItemDataWrap
    {
        public int Id;
        public long Count;
        public BagQueryItemInfo Info;
        public PlayerItemParamEquipWrap Equip;
        public PlayerItemParamEquipWrap GetItemParamEquip() => Equip;
    }
    public sealed class BagQueryItemInfo
    {
        public int ClassId, Quality;
        public ItemType ItemType;
        public RedDotType RedDotType;
        public object PartInfo;
    }
    public sealed class EquipPieceMergeInfo { public int PieceMergeCount; }
    public sealed class ItemPieceMergeInfo { public int PieceMergeCount; }
    public sealed class RandomTreasureExchangeInfo { public int RequireCount; }
    public sealed class PetInfo { public int ClassId, PieceMergeCount; }
    public sealed class PeriodCardItemInfo { public int PeriodCardId; }
    public sealed class PeriodicCardPayInfo { public int Id; }
    public sealed class CookingInfo { public int Id; }
    public sealed class EquipAttributeReplaceItemInfo { public List<int> Quality; public int AttributePropId; }
    public sealed class EquipPropBase { public int PropId; }
    public sealed class BagQueryEquipProps { public IReadOnlyDictionary<int, EquipPropBase> RawData; }
    public sealed class PlayerItemParamEquipWrap
    {
        public IReadOnlyCollection<object> PurifyProps;
        public BagQueryEquipProps OtherProps;
    }
    public interface IBagQueryTable<T> where T : class { T Get(int id); }
    public interface IBagQueryPetTable { PetInfo GetByPieceClassId(int id); }
    public interface IBagQueryCookingTable
    {
        IReadOnlyDictionary<int, int> Formula2CookingLookup { get; }
        bool TryGet(int id, out CookingInfo value);
    }
    public interface IBagItemQueryTables
    {
        IBagQueryTable<EquipPieceMergeInfo> EquipPieceMergeInfoParser { get; }
        IBagQueryTable<ItemPieceMergeInfo> ItemPieceMergeInfoParser { get; }
        IBagQueryTable<RandomTreasureExchangeInfo> RandomTreasureExchangeInfoParser { get; }
        IBagQueryPetTable PetInfoParser { get; }
        IBagQueryTable<PeriodicCardPayInfo> PeriodicCardPayInfoParser { get; }
        IBagQueryCookingTable CookingInfoParser { get; }
        IBagQueryTable<EquipAttributeReplaceItemInfo> EquipAttributeReplaceItemInfoParser { get; }
    }
    public sealed class BagQueryPaySettings { public int PeriodicCardMaxDurationDay; }
    public interface IBagItemQuerySettings
    {
        IReadOnlyCollection<ItemType> ShowCanUseRedDotItemTypes { get; }
        IReadOnlyCollection<ItemType> EquipTypes { get; }
        BagQueryPaySettings Pay { get; }
    }
    public interface IBagItemQueryPlayer
    {
        bool HasItem(int itemId, long count);
        bool HasPet(int classId);
        bool TryGetBuyCount(PayType type, int id, DateTime time, out int count);
        IReadOnlyCollection<PlayerItemDataWrap> FindItemsByType(IReadOnlyCollection<ItemType> types);
    }
    public interface IBagItemQueryServices
    {
        IBagItemQuerySettings Settings { get; }
        IBagItemQueryTables Tables { get; }
        IBagItemQueryPlayer Player { get; }
        DateTime GetDateTime();
        bool IsLimit(CookingInfo cooking);
    }
}
