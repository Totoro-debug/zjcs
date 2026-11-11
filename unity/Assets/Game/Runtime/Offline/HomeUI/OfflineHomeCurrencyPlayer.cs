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
            RequireHomeCurrency(itemId);
            return new HomeCurrencyItemInfo { HasAutoIncrease = itemId == 57 };
        }

        public bool IsOutputAdditionItem(int itemId) { RequireHomeCurrency(itemId); return outputAdditionItems.Contains(itemId); }

        public IPlayerItemUICountProvider CreateCountProvider(PlayerItemType type, PlayerItemUIItemIdProvider id)
        {
            int expected = type == PlayerItemType.Money ? 1 : type == PlayerItemType.Diamon ? 2
                : type == PlayerItemType.TiliId ? 57 : 0;
            if (expected == 0 || expected != id.ItemId)
                throw new InvalidOperationException("Only the selected original home currencies are bound here.");
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

        private static void RequireHomeCurrency(int itemId)
        {
            if (itemId != 1 && itemId != 2 && itemId != 57)
                throw new InvalidOperationException("The selected home top bar has currencies 1/2/57.");
        }

        public void Dispose() { disposed = true; observed = null; OnItemCountUpdate = null; OnAutoIncreaseItemDataUpdate = null; }
    }
}
