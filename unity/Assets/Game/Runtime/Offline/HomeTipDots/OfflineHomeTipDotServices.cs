using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeTipDots
{
    public sealed class OfflineHomeTipDotServices : IHomeTipDotServices, IHomeTipDotPlayer, IDisposable
    {
        private sealed class Subscription : IDisposable
        {
            private Action release;
            internal Subscription(Action release) => this.release = release;
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }

        private readonly Func<OfflineMainlineSave> readSave;
        private readonly OfflineHomeIncomeSession session;
        private readonly OfficialHomeRules rules;
        private readonly Func<DateTime> clock;
        private readonly Dictionary<EFurniture, List<HomeTipFastHangUpInfo>> byFurniture =
            new Dictionary<EFurniture, List<HomeTipFastHangUpInfo>>();
        private readonly HashSet<int> fastHangUpItems = new HashSet<int>();
        private readonly Dictionary<int, long> observedItemCounts = new Dictionary<int, long>();
        private readonly HashSet<int> observedUnlockedHangUps = new HashSet<int>();
        private readonly List<Action> newDaySubscribers = new List<Action>();
        private DateTime observedDay;
        private bool disposed;

        public OfflineHomeTipDotServices(Func<OfflineMainlineSave> readSave, OfflineHomeIncomeSession session,
            OfficialHomeRules rules, Func<DateTime> clock = null)
        {
            this.readSave = readSave ?? throw new ArgumentNullException(nameof(readSave));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.clock = clock ?? (() => DateTime.Now);
            observedDay = OfflineRefreshClock.DayStart(this.clock());

            foreach (HomeFastHangUpRecord fast in rules.Data.fastHangUps ?? Array.Empty<HomeFastHangUpRecord>())
            {
                if (fast == null) continue;
                var furniture = (EFurniture)fast.bindFurniture;
                if (!byFurniture.TryGetValue(furniture, out List<HomeTipFastHangUpInfo> list))
                {
                    list = new List<HomeTipFastHangUpInfo>();
                    byFurniture.Add(furniture, list);
                }
                var itemDic = new Dictionary<int, int>();
                foreach (HomeFastHangUpItem item in fast.items ?? Array.Empty<HomeFastHangUpItem>())
                {
                    if (item == null || item.itemId <= 0) continue;
                    int hours = checked((int)Math.Round(item.hours));
                    itemDic[item.itemId] = hours;
                    fastHangUpItems.Add(item.itemId);
                }
                list.Add(new HomeTipFastHangUpInfo
                {
                    FastHangUpId = fast.fastHangUpId,
                    HangUpIdList = (fast.hangUpIdList ?? Array.Empty<int>()).ToList(),
                    FastHangUpItemDic = itemDic,
                });
            }

            foreach (int itemId in fastHangUpItems)
                observedItemCounts[itemId] = GetItemAmount(itemId);
            foreach (OfflineHomeIncomeDefinition definition in session.Definitions)
                if (definition.Unlocked) observedUnlockedHangUps.Add(definition.HangUpId);
        }

        public IHomeTipDotPlayer Player => this;

        public event Action<int> OnHangUpUnlock;
        public event Action<int> OnFastHangUp;
        public event Action<long> OnDebugTimeOffset;
        public event Action<HomeTipItemData, long, object> OnItemCountUpdate;

        public void BindNode(TipDotNodeBase node)
        {
            if (node is IHomeTipDotNode homeNode)
                homeNode.Services = this;
        }

        public bool TryGetFastHangUpInfo(EFurniture furniture, out List<HomeTipFastHangUpInfo> list)
        {
            if (byFurniture.TryGetValue(furniture, out List<HomeTipFastHangUpInfo> found) && found.Count > 0)
            {
                list = found;
                return true;
            }
            list = null;
            return false;
        }

        public bool IsFastHangUpItem(int itemId) => fastHangUpItems.Contains(itemId);

        public EFurniture GetHangUpBindFurniture(int hangUpId)
        {
            HomeHangUpRecord row = rules.HangUp(hangUpId);
            return row == null ? (EFurniture)(-1) : (EFurniture)row.bindFurniture;
        }

        public IDisposable SubscribeEnterNewDay(Action callback)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeTipDotServices));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            newDaySubscribers.Add(callback);
            return new Subscription(() => newDaySubscribers.Remove(callback));
        }

        public bool CanFreeFastHangUp(int fastHangUpId) => session.CanFreeFastForward(fastHangUpId);

        public long GetItemAmount(int itemId)
        {
            OfflinePlayerProgress player = readSave()?.Player;
            if (player?.Inventory == null) return 0;
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value != null && value.ItemId == itemId);
            return OfflineItemAutoIncrease.Count(stack, DateTime.UtcNow);
        }

        public bool HasUnlockedHangUp(int hangUpId)
            => session.Definitions.Any(value => value.HangUpId == hangUpId && value.Unlocked);

        public void NotifyFastHangUp(int fastHangUpId)
        {
            if (disposed) return;
            OnFastHangUp?.Invoke(fastHangUpId);
        }

        public void NotifyHangUpUnlock(int hangUpId)
        {
            if (disposed) return;
            if (observedUnlockedHangUps.Add(hangUpId) || HasUnlockedHangUp(hangUpId))
                OnHangUpUnlock?.Invoke(hangUpId);
        }

        public void NotifyDebugTimeOffset(long offset)
        {
            if (disposed) return;
            OnDebugTimeOffset?.Invoke(offset);
        }

        public void Synchronize()
        {
            if (disposed) return;
            foreach (OfflineHomeIncomeDefinition definition in session.Definitions)
            {
                if (definition.Unlocked && observedUnlockedHangUps.Add(definition.HangUpId))
                    OnHangUpUnlock?.Invoke(definition.HangUpId);
            }
            foreach (int itemId in fastHangUpItems)
            {
                long current = GetItemAmount(itemId);
                long previous = observedItemCounts.TryGetValue(itemId, out long prior) ? prior : 0L;
                if (current != previous)
                {
                    observedItemCounts[itemId] = current;
                    OnItemCountUpdate?.Invoke(new HomeTipItemData
                    {
                        Info = new HomeTipItemInfo { ClassId = itemId }
                    }, previous, null);
                }
            }
            DateTime currentDay = OfflineRefreshClock.DayStart(clock());
            if (currentDay != observedDay)
            {
                observedDay = currentDay;
                foreach (Action callback in newDaySubscribers.ToArray()) callback?.Invoke();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            newDaySubscribers.Clear();
            OnHangUpUnlock = null;
            OnFastHangUp = null;
            OnDebugTimeOffset = null;
            OnItemCountUpdate = null;
        }
    }
}
