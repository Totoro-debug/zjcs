using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeCurrencyUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeUI
{
    // The home owner calls SynchronizeCommittedPlayer after reloading/committing
    // its shared profile. Natural recovery is read by the original coroutine.
    public sealed class OfflineHomeCurrencyPlayer : IHomeCurrencyBindings, IHomeCurrencyPlayer, IDisposable
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly Func<DateTime> clock;
        private readonly HashSet<int> outputAdditionItems;
        private OfflinePlayerProgress observed;
        private bool disposed;
        private sealed class CurrencyBinding { public PlayerItemType Type; public int ItemId; public bool HasAutoIncrease; }
        private readonly List<CurrencyBinding> currencies = new List<CurrencyBinding>();
        private sealed class CurrencyScope : IDisposable
        {
            private Action release;
            public CurrencyScope(Action release) { this.release = release; }
            public void Dispose() { var action=release;release=null;action?.Invoke(); }
        }
        public IDisposable RegisterCurrency(PlayerItemType type,int itemId,bool hasAutoIncrease)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OfflineHomeCurrencyPlayer));
            if(itemId<=0||!Enum.IsDefined(typeof(PlayerItemType),type))throw new ArgumentException("Bind a selected original currency identity.");
            int known=KnownItemId(type);
            if(known!=0&&(known!=itemId||hasAutoIncrease!=(itemId==57)))throw new ArgumentException("The selected currency conflicts with the existing home binding.");
            if(currencies.Any(row=>(row.Type==type&&row.ItemId!=itemId)||(row.ItemId==itemId&&row.HasAutoIncrease!=hasAutoIncrease)))
                throw new ArgumentException("The selected currency conflicts with another window binding.");
            var binding=new CurrencyBinding{Type=type,ItemId=itemId,HasAutoIncrease=hasAutoIncrease};currencies.Add(binding);
            return new CurrencyScope(()=>currencies.Remove(binding));
        }
        private static int KnownItemId(PlayerItemType type)=>type==PlayerItemType.Money?1:type==PlayerItemType.Diamon?2:
            type==PlayerItemType.TiliId?57:type==PlayerItemType.FlareStar?15:0;
        public IHomeCurrencyPlayer MainPlayer => disposed ? null : this;
        public bool ManagerAvailable => !disposed;
        public SafeAction OnPlayerUpdate { get; } = new SafeAction();
        public event Action<HomeCurrencyItem, long, object> OnItemCountUpdate;
        public event Action<HomeCurrencyItem> OnAutoIncreaseItemDataUpdate;

        public OfflineHomeCurrencyPlayer(OfflineMainlineProgressSession profile, Func<DateTime> clock = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.clock = clock ?? (() => DateTime.UtcNow);
            outputAdditionItems = new HashSet<int>(OfficialHomeAutoIncreaseConfiguration.LoadDefault().outputAdditionItems);
            observed = profile.Save.Player;
        }

        public long GetItemAmount(int itemId) => OfflineItemAutoIncrease.Count(
            profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId), clock());

        public HomeCurrencyItemInfo GetItemInfo(int itemId)
        {
            RequireCurrency(itemId);
            return new HomeCurrencyItemInfo { HasAutoIncrease = currencies.LastOrDefault(row=>row.ItemId==itemId)?.HasAutoIncrease ?? (itemId == 57) };
        }

        public bool IsOutputAdditionItem(int itemId) { RequireCurrency(itemId); return outputAdditionItems.Contains(itemId); }

        public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type, PlayerItemUIItemIdProvider id)
        {
            int expected = currencies.LastOrDefault(row=>row.Type==type)?.ItemId ?? KnownItemId(type);
            if (expected == 0 || expected != id.ItemId)
                throw new InvalidOperationException("Only the selected original home and exchange currencies are bound here.");
            return new PlayerItemUIPlayerItemCountProvider(id.ItemId, this);
        }

        public void SynchronizeCommittedPlayer()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeCurrencyPlayer));
            var next = profile.Save.Player;
            if (ReferenceEquals(observed, next)) return;
            var previous = observed;
            observed = next;
            DateTime now = clock();
            var before = previous.Inventory.ToDictionary(value => value.ItemId);
            var after = next.Inventory.ToDictionary(value => value.ItemId);
            foreach (int id in before.Keys.Union(after.Keys).OrderBy(value => value))
            {
                before.TryGetValue(id, out var oldItem);
                after.TryGetValue(id, out var newItem);
                long oldCount = OfflineItemAutoIncrease.Count(oldItem, now);
                long count = OfflineItemAutoIncrease.Count(newItem, now);
                var item = new HomeCurrencyItem { Id = id, Count = count };
                if (oldCount != count) OnItemCountUpdate?.Invoke(item, oldCount, null);
                if (!SameParameter(oldItem?.AutoIncrease, newItem?.AutoIncrease))
                    OnAutoIncreaseItemDataUpdate?.Invoke(item);
            }
        }

        private static bool SameParameter(OfflineItemAutoIncreaseProgress left, OfflineItemAutoIncreaseProgress right)
            => ReferenceEquals(left, right) || left != null && right != null && left.Decimal == right.Decimal
            && left.LastUpdateTimeTicks == right.LastUpdateTimeTicks && left.IncreaseSpeed == right.IncreaseSpeed
            && left.BonusSeconds == right.BonusSeconds && left.MaxCount == right.MaxCount;

        private void RequireCurrency(int itemId)
        {
            if (itemId != 1 && itemId != 2 && itemId != 15 && itemId != 57 && !currencies.Any(row=>row.ItemId==itemId))
                throw new InvalidOperationException("The selected home and exchange bars have currencies 1/2/15/57.");
        }

        public void Dispose() { disposed = true; currencies.Clear(); observed = null; OnItemCountUpdate = null; OnAutoIncreaseItemDataUpdate = null; }
    }
}
