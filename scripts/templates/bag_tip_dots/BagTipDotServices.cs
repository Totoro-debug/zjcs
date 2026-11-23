using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BagTipDots
{
    // Query/event ports carry original player operations; they do not decide tips.
    public interface IBagTipDotNode { IBagTipDotServices Services { get; set; } }
    public interface IBagTipDotServices
    {
        IBagTipDotPlayer Player { get; }
        IBagTipDotSettings Settings { get; }
        // Current scope collector only; null outside a scope. Removal callbacks
        // run before the owner clears the old collector, as in the source.
        IBagGemMergeManager GemMergeManager { get; }
        BagTipFurnitureInfo GetFurnitureInfo(EFurniture furniture);
        IDisposable SubscribeCondition(IBagTipCondition condition, Action action);
        IDisposable SubscribeEnterNewDay(Action action);
        bool IsFunctionLimit(IBagTipDotPlayer player, FunctionType type);
        IReadOnlyCollection<int> ItemTagKeys { get; }
    }
    public interface IBagTipDotSettings
    {
        List<ItemType> EquipTypes { get; }
        List<ItemType> ShowCanUseRedDotItemTypes { get; }
        List<ItemType> NewDayRefreshRedDotItemType { get; }
    }
    public sealed class BagTipItemInfo { public int ClassId; public ItemType ItemType; }
    public sealed class BagTipItem { public BagTipItemInfo Info; public ulong ItemIndex; }
    public sealed class BagTipAward { public SourceOperate SourceOperate; }
    public interface IBagTipCondition { bool IsMet(IBagTipDotPlayer player); }
    public sealed class BagTipFurnitureInfo
    {
        public IBagTipCondition UnlockCondition;
        public IReadOnlyDictionary<int, long> RepairCost;
    }
    public interface IBagTipDotPlayer
    {
        event Action<BagTipItem, long, BagTipAward> OnItemCountUpdate;
        event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        event Action<int> OnSubRankUpdate;
        event Action<EFurniture> OnRepairFurniture;
        event Action<EquipPos, ulong, ulong> OnEquipUpdate;
        event Action<IDictionary<int, long>> OnSplitItem;
        event Action<ulong> OnEquipAttribuatePurifyStateChange;
        IReadOnlyCollection<BagTipItem> FindItemsByType(ItemType type);
        IReadOnlyCollection<BagTipItem> FindItemsByType(List<ItemType> types);
        bool ShowBagCanUseItemRedDot(BagTipItem item);
        RedDotType GetItemRedDotType(BagTipItem item);
        long GetCanUseBlueDotCount(BagTipItem item);
        IReadOnlyCollection<BagTipItem> GetCanRecycleGem();
        List<BagTipItem> GetCanRecycleGift();
        List<BagTipItem> GetCanRecycleDoorkey();
        bool HasFurniture(EFurniture furniture);
        bool HasItems(IReadOnlyDictionary<int, long> cost);
        bool IsFunctionLimit(FunctionType type);
        bool IsEquipWorn(ulong itemIndex);
        bool IsEquipInPurify(ulong itemIndex);
        bool TryGetEquip(EquipPos position, out BagTipItem item);
    }
    public interface IBagGemSubscriber { void Refresh(); }
    public interface IBagGemMergeManager
    {
        void Subscribe(IBagGemSubscriber subscriber);
        void Unsubscribe(IBagGemSubscriber subscriber);
    }
}
