using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Zjcs.Offline.BagItemQueries;
using Zjcs.Offline.BagRecycleQueries;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Official.BagTipDots;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using QueryItem = Zjcs.Official.BagItemQueries.PlayerItemDataWrap;
using BagEquipPos = Zjcs.Official.BagTipDots.EquipPos;
using BagItemType = Zjcs.Official.BagTipDots.ItemType;
using BagRedDotType = Zjcs.Official.BagTipDots.RedDotType;
using BagFunctionType = Zjcs.Official.BagTipDots.FunctionType;

namespace Zjcs.Offline.BagTipDots
{
    // Original nodes make every tip decision. This Home-scope owner adapts the
    // selected local profile and its committed changes, without another ledger.
    public sealed class OfflineBagTipDotServices : IBagTipDotServices, IBagTipDotPlayer,
        IBagTipDotSettings, IBagGemMergeManager, IDisposable
    {
        sealed class Record { internal int ItemId; internal long InstanceId, Count; }
        sealed class Subscription : IDisposable
        {
            Action dispose;
            internal Subscription(Action dispose) => this.dispose = dispose;
            public void Dispose() { var action = dispose; dispose = null; action?.Invoke(); }
        }
        sealed class Condition : IBagTipCondition
        {
            internal OfflineBagTipDotServices Owner;
            internal HomeConditionNode Node;
            public bool IsMet(IBagTipDotPlayer player)
            {
                if (!ReferenceEquals(player, Owner)) throw new ArgumentException("The condition belongs to another player.");
                return Owner.homeRules.IsMet(Node);
            }
        }
        readonly Func<OfflineMainlineSave> readSave;
        readonly Func<int, bool> ownsFurniture;
        readonly Func<DateTime> clock;
        readonly OfficialHomeRules homeRules;
        readonly OfflineBagItemQueryCatalog items;
        readonly OfflineBagItemQueries queries;
        readonly OfflineBagRecycleCatalog recycleCatalog;
        readonly OfflineBagRecycleQueries recycling;
        readonly ConditionalWeakTable<BagTipItem, Record> records = new ConditionalWeakTable<BagTipItem, Record>();
        readonly Dictionary<EFurniture, BagTipFurnitureInfo> furniture = new Dictionary<EFurniture, BagTipFurnitureInfo>();
        readonly HashSet<IBagGemSubscriber> gemSubscribers = new HashSet<IBagGemSubscriber>();
        readonly Dictionary<IBagTipCondition, List<Action>> conditions = new Dictionary<IBagTipCondition, List<Action>>();
        readonly HashSet<TipDotConditionNodeBase> nodes = new HashSet<TipDotConditionNodeBase>();
        OfflineMainlineSave observed;
        HashSet<int> owned;
        DateTime day;
        Action newDay;
        bool disposed;
        public IBagTipDotPlayer Player => this;
        public IBagTipDotSettings Settings => this;
        public IBagGemMergeManager GemMergeManager => disposed ? null : this;
        public List<BagItemType> EquipTypes { get; }
        public List<BagItemType> ShowCanUseRedDotItemTypes { get; }
        public List<BagItemType> NewDayRefreshRedDotItemType { get; }
        public IReadOnlyCollection<int> ItemTagKeys => recycleCatalog.ItemTags.Select(row => row.Id).ToArray();
        public event Action<BagTipItem, long, BagTipAward> OnItemCountUpdate;
        public event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        public event Action<int> OnSubRankUpdate;
        public event Action<EFurniture> OnRepairFurniture;
        public event Action<BagEquipPos, ulong, ulong> OnEquipUpdate;
        public event Action<IDictionary<int, long>> OnSplitItem;
        public event Action<ulong> OnEquipAttribuatePurifyStateChange;
        public int SubscriberCount => Count(OnItemCountUpdate) + Count(OnNpcFriendshipLevelUpdate) + Count(OnSubRankUpdate)
            + Count(OnRepairFurniture) + Count(OnEquipUpdate) + Count(OnSplitItem) + Count(OnEquipAttribuatePurifyStateChange)
            + Count(newDay) + conditions.Values.Sum(values => values.Count) + gemSubscribers.Count;
        public int ReleaseSubscriberCount { get; private set; } = -1;
        static int Count(Delegate value) => value?.GetInvocationList().Length ?? 0;

        public OfflineBagTipDotServices(Func<OfflineMainlineSave> readSave, OfficialHomeRules rules,
            Func<int, bool> ownsFurniture, OfflineBagTipDotAssets assets, Func<DateTime> clock = null)
        {
            this.readSave = readSave ?? throw new ArgumentNullException(nameof(readSave));
            homeRules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (!rules.HasFurnitureConfiguration) throw new ArgumentException("Bind the selected original furniture rules first.", nameof(rules));
            this.ownsFurniture = ownsFurniture ?? throw new ArgumentNullException(nameof(ownsFurniture));
            if (!assets) throw new ArgumentNullException(nameof(assets));
            this.clock = clock ?? (() => DateTime.UtcNow);
            items = OfflineBagItemQueryCatalog.LoadDefault();
            queries = new OfflineBagItemQueries(readSave, items, this.clock);
            recycleCatalog = OfflineBagRecycleCatalog.LoadDefault();
            recycling = new OfflineBagRecycleQueries(readSave, recycleCatalog, items);
            EquipTypes = assets.EquipTypes.Select(value => (BagItemType)value).ToList();
            ShowCanUseRedDotItemTypes = assets.ShowCanUseRedDotItemTypes.Select(value => (BagItemType)value).ToList();
            NewDayRefreshRedDotItemType = assets.NewDayRefreshRedDotItemType.Select(value => (BagItemType)value).ToList();
            if (!EquipTypes.SequenceEqual(items.EquipTypes) || !ShowCanUseRedDotItemTypes.SequenceEqual(items.ShowCanUseRedDotItemTypes))
                throw new InvalidOperationException("Bag graph settings and the original query configuration differ.");
            observed = readSave(); owned = ReadOwned(); day = OfflineRefreshClock.DayStart(this.clock().ToLocalTime());
        }
        public void BindNode(TipDotNodeBase node)
        {
            if (node is IBagTipDotNode bag)
            {
                bag.Services = this;
                if (node is TipDotConditionNodeBase condition) nodes.Add(condition);
            }
        }
        BagTipItem Project(QueryItem source, long instanceId = 0)
        {
            var view = new BagTipItem { Info = new BagTipItemInfo { ClassId = source.Id, ItemType = source.Info.ItemType },
                ItemIndex = checked((ulong)instanceId) };
            // Non-equipment stacks have no saved equipment index. Preserve each
            // projected record's quantity instead of summing or inventing IDs.
            records.Add(view, new Record { ItemId = source.Id, InstanceId = instanceId, Count = source.Count });
            return view;
        }
        Record Get(BagTipItem item) => item != null && records.TryGetValue(item, out var value) ? value
            : throw new ArgumentException("The item view belongs to another Bag owner.", nameof(item));
        public IReadOnlyCollection<BagTipItem> FindItemsByType(BagItemType type)
        {
            var player = readSave().Player;
            if (EquipTypes.Contains(type))
                return player.Equipment.Instances.Where(row => items.Item(row.ItemId).ItemType == (int)type).Select(row =>
                    Project(new QueryItem { Id = row.ItemId, Count = 1, Info = new Zjcs.Official.BagItemQueries.BagQueryItemInfo { ItemType = type } }, row.InstanceId)).ToArray();
            return queries.FindItemsByType(new[] { type }).Select(row => Project(row)).ToArray();
        }
        public IReadOnlyCollection<BagTipItem> FindItemsByType(List<BagItemType> types) => types.SelectMany(FindItemsByType).ToArray();
        public bool ShowBagCanUseItemRedDot(BagTipItem item) { var row = Get(item); return queries.CanUseRedDot(row.ItemId, row.InstanceId, row.Count); }
        public BagRedDotType GetItemRedDotType(BagTipItem item) { var row = Get(item); return (BagRedDotType)queries.GetRedDotType(row.ItemId, row.InstanceId, row.Count); }
        public long GetCanUseBlueDotCount(BagTipItem item) { var row = Get(item); return queries.GetBlueDotCount(row.ItemId, row.InstanceId, row.Count); }
        public IReadOnlyCollection<BagTipItem> GetCanRecycleGem() => recycling.GetCanRecycleGem().Select(row => Project(row)).ToArray();
        public List<BagTipItem> GetCanRecycleGift() => recycling.GetCanRecycleGift().Select(row => Project(row)).ToList();
        public List<BagTipItem> GetCanRecycleDoorkey() => recycling.GetCanRecycleDoorkey().Select(row => Project(row)).ToList();
        public bool HasFurniture(EFurniture type) => homeRules.HasFurniture((int)type, ownsFurniture((int)type));
        public bool HasItems(IReadOnlyDictionary<int, long> cost) => cost.All(row => queries.HasItem(row.Key, row.Value));
        public bool IsFunctionLimit(BagFunctionType type) => homeRules.IsFunctionLimited((int)type);
        public bool IsFunctionLimit(IBagTipDotPlayer player, BagFunctionType type)
        {
            if (!ReferenceEquals(player, this)) throw new ArgumentException("The query belongs to another player.");
            return IsFunctionLimit(type);
        }
        public bool IsEquipWorn(ulong index) => index != 0 && readSave().Player.Equipment.Equipped.Any(row => checked((ulong)row.InstanceId) == index);
        public bool IsEquipInPurify(ulong index) => readSave().Player.Equipment.Instances.FirstOrDefault(row => checked((ulong)row.InstanceId) == index)?.PurifyProps.Count > 0;
        public bool TryGetEquip(BagEquipPos position, out BagTipItem item)
        {
            item = null;
            var player = readSave().Player;
            var slot = player.Equipment.Equipped.FirstOrDefault(row => row.Position == (int)position);
            if (slot == null || slot.InstanceId == 0) return false;
            var equipment = player.Equipment.Instances.FirstOrDefault(row => row.InstanceId == slot.InstanceId);
            if (equipment == null) return false;
            item = Project(new QueryItem { Id = equipment.ItemId, Count = 1,
                Info = new Zjcs.Official.BagItemQueries.BagQueryItemInfo { ItemType = (BagItemType)items.Item(equipment.ItemId).ItemType } }, equipment.InstanceId);
            return true;
        }
        public BagTipFurnitureInfo GetFurnitureInfo(EFurniture type)
        {
            if (furniture.TryGetValue(type, out var value)) return value;
            var row = homeRules.FurnitureOf((int)type) ?? throw new InvalidOperationException("Original furniture is unbound: " + type);
            value = new BagTipFurnitureInfo { UnlockCondition = new Condition { Owner = this, Node = row.unlock },
                RepairCost = row.repairCost.ToDictionary(cost => cost.itemId, cost => cost.count) };
            furniture.Add(type, value); return value;
        }
        public IDisposable SubscribeCondition(IBagTipCondition condition, Action action)
        {
            if (!(condition is Condition own) || !ReferenceEquals(own.Owner, this)) throw new ArgumentException("The condition belongs to another owner.");
            if (!conditions.TryGetValue(condition, out var values)) conditions.Add(condition, values = new List<Action>());
            values.Add(action);
            return new Subscription(() => { values.Remove(action); if (values.Count == 0) conditions.Remove(condition); });
        }
        public IDisposable SubscribeEnterNewDay(Action action) { newDay += action; return new Subscription(() => newDay -= action); }
        public void Subscribe(IBagGemSubscriber subscriber) => gemSubscribers.Add(subscriber);
        public void Unsubscribe(IBagGemSubscriber subscriber) => gemSubscribers.Remove(subscriber);
        HashSet<int> ReadOwned() => new HashSet<int>(homeRules.Furniture.Where(row => HasFurniture((EFurniture)row.type)).Select(row => row.type));

        // The local profile commits immutable snapshots rather than receiving
        // the client's individual server messages. Publish real deltas, then
        // invalidate this graph's conditions once for the committed snapshot.
        // An unknown award stays null; never invent a recycle/split operation.
        public void Synchronize(BagTipAward award = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineBagTipDotServices));
            var current = readSave(); var currentOwned = ReadOwned();
            if (!ReferenceEquals(observed, current))
            {
                var before = observed.Player; var after = current.Player;
                var oldCounts = before.Inventory.GroupBy(row => row.ItemId).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
                var newCounts = after.Inventory.GroupBy(row => row.ItemId).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
                foreach (int id in oldCounts.Keys.Union(newCounts.Keys))
                {
                    oldCounts.TryGetValue(id, out long previous); newCounts.TryGetValue(id, out long next);
                    if (previous != next) OnItemCountUpdate?.Invoke(Project(new QueryItem { Id = id, Count = next,
                        Info = new Zjcs.Official.BagItemQueries.BagQueryItemInfo { ItemType = (BagItemType)items.Item(id).ItemType } }), previous, award);
                }
                var oldEquipment = before.Equipment.Instances.ToDictionary(row => row.InstanceId);
                var newEquipment = after.Equipment.Instances.ToDictionary(row => row.InstanceId);
                foreach (long index in oldEquipment.Keys.Union(newEquipment.Keys))
                {
                    bool was = oldEquipment.TryGetValue(index, out var previous);
                    bool present = newEquipment.TryGetValue(index, out var next);
                    if (was == present) continue;
                    int id = (next ?? previous).ItemId;
                    OnItemCountUpdate?.Invoke(Project(new QueryItem { Id = id, Count = present ? 1 : 0,
                        Info = new Zjcs.Official.BagItemQueries.BagQueryItemInfo { ItemType = (BagItemType)items.Item(id).ItemType } }, index), was ? 1 : 0, award);
                }
                var oldNpc = before.CharacterProperties.NpcFriendships.ToDictionary(row => row.NpcClassId, row => row.Level);
                var newNpc = after.CharacterProperties.NpcFriendships.ToDictionary(row => row.NpcClassId, row => row.Level);
                foreach (int id in oldNpc.Keys.Union(newNpc.Keys))
                {
                    oldNpc.TryGetValue(id, out int previous); newNpc.TryGetValue(id, out int next);
                    if (previous != next) OnNpcFriendshipLevelUpdate?.Invoke(id, previous, next);
                }
                if (before.SubRank != after.SubRank) OnSubRankUpdate?.Invoke(after.SubRank);
                var oldSlots = before.Equipment.Equipped.ToDictionary(row => row.Position, row => row.InstanceId);
                var newSlots = after.Equipment.Equipped.ToDictionary(row => row.Position, row => row.InstanceId);
                foreach (int pos in oldSlots.Keys.Union(newSlots.Keys))
                {
                    oldSlots.TryGetValue(pos, out long previous); newSlots.TryGetValue(pos, out long next);
                    if (previous != next) OnEquipUpdate?.Invoke((BagEquipPos)pos, checked((ulong)previous), checked((ulong)next));
                }
                foreach (var item in after.Equipment.Instances)
                {
                    var previous = before.Equipment.Instances.FirstOrDefault(row => row.InstanceId == item.InstanceId);
                    if (previous == null ? item.PurifyProps.Count > 0 : previous.PurifyProps.Count != item.PurifyProps.Count
                        || previous.PurifyProps.Any(property => !item.PurifyProps.Any(value => value.Key == property.Key
                            && JsonUtility.ToJson(value.Value) == JsonUtility.ToJson(property.Value))))
                        OnEquipAttribuatePurifyStateChange?.Invoke(checked((ulong)item.InstanceId));
                }
                foreach (var values in conditions.Values.ToArray()) foreach (var action in values.ToArray()) action();
                foreach (var node in nodes) node.SetDirty();
                foreach (var subscriber in gemSubscribers.ToArray()) subscriber.Refresh();
                observed = current;
            }
            foreach (int type in currentOwned.Except(owned)) OnRepairFurniture?.Invoke((EFurniture)type);
            owned = currentOwned;
            var boundary = OfflineRefreshClock.DayStart(clock().ToLocalTime());
            // UI invalidation follows the same no-repeat rollback policy as the
            // confirmed local daily schedule. It never grants or saves rewards.
            if (boundary > day) { day = boundary; newDay?.Invoke(); }
        }
        public void NotifySplitItem(IDictionary<int, long> items) => OnSplitItem?.Invoke(items);
        public void Dispose()
        {
            if (disposed) return;
            ReleaseSubscriberCount = SubscriberCount;
            disposed = true;
            nodes.Clear(); gemSubscribers.Clear(); conditions.Clear(); newDay = null;
            OnItemCountUpdate = null; OnNpcFriendshipLevelUpdate = null; OnSubRankUpdate = null;
            OnRepairFurniture = null; OnEquipUpdate = null; OnSplitItem = null; OnEquipAttribuatePurifyStateChange = null;
        }
    }
}
