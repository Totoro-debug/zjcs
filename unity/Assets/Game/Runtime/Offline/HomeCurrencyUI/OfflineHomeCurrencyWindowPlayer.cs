using System;
using Zjcs.Official.HomeCurrencyUI;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // Shares the same profile and quantity observers as the original top bar.
    // The owner supplies original item-data projection and ban-cost state.
    public sealed class OfflineHomeCurrencyWindowPlayer : IHomeCurrencyWindowPlayer, IDisposable
    {
        readonly OfflineMainlineProgressSession profile;
        readonly OfflineHomeCurrencyPlayer quantities;
        readonly OfflineHomeQuickBuyCounters counters;
        readonly Func<int,long,IProfessionItemData> projectItem;
        readonly Func<int,bool> isBanCostItem;
        readonly Func<DateTime> clock;
        bool disposed;

        public OfflineHomeCurrencyWindowPlayer(OfflineMainlineProgressSession profile,
            OfflineHomeCurrencyPlayer quantities, OfflineHomeQuickBuyCounters counters,
            Func<int,long,IProfessionItemData> projectItem, Func<int,bool> isBanCostItem,
            Func<DateTime> clock = null)
        {
            this.profile=profile??throw new ArgumentNullException(nameof(profile));
            this.quantities=quantities??throw new ArgumentNullException(nameof(quantities));
            this.counters=counters??throw new ArgumentNullException(nameof(counters));
            this.projectItem=projectItem??throw new ArgumentNullException(nameof(projectItem));
            this.isBanCostItem=isBanCostItem??throw new ArgumentNullException(nameof(isBanCostItem));
            this.clock=clock??(()=>DateTime.UtcNow);
            if(!quantities.ManagerAvailable) throw new ArgumentException("Live shared currency observers are required.");
            quantities.OnItemCountUpdate+=OnQuantityChanged;
            quantities.OnAutoIncreaseItemDataUpdate+=OnParameterChanged;
        }

        OfflinePlayerProgress Player
        {
            get { RequireLive(); return profile.Save.Player; }
        }
        public int Level=>Player.Level;
        public int SubRank=>Player.SubRank;
        public long GetItemAmount(int itemId) { RequireLive(); return quantities.GetItemAmount(itemId); }
        public bool HasItem(int itemId,long count)=>GetItemAmount(itemId)>=count;
        public bool IsBanCostItem(int itemId) { RequireLive(); return isBanCostItem(itemId); }
        public long QuickBuyGetItemBuyCount(int itemId,EQuickBuyRefreshType type)
            =>counters.GetBuyCount(Player,itemId,type,clock());
        public event Action<IProfessionItemData,long,object> OnItemCountUpdate;
        public event Action<HomeCurrencyAutoIncreaseEvent> OnAutoIncrease;

        void OnQuantityChanged(HomeCurrencyItem item,long previous,object source)
        {
            var changed=OnItemCountUpdate;
            if(changed==null) return;
            var projected=projectItem(item.Id,item.Count);
            if(projected==null||projected.Id!=item.Id||projected.Count!=item.Count
                ||projected.Info==null||projected.Info.ClassId!=item.Id)
                throw new InvalidOperationException("Original currency-window item projection differs from committed inventory.");
            changed(projected,previous,source);
        }
        void OnParameterChanged(HomeCurrencyItem item)
            =>OnAutoIncrease?.Invoke(new HomeCurrencyAutoIncreaseEvent{ItemId=item.Id});
        void RequireLive()
        {
            if(disposed) throw new ObjectDisposedException(nameof(OfflineHomeCurrencyWindowPlayer));
        }
        public void Dispose()
        {
            if(disposed) return;
            disposed=true;
            quantities.OnItemCountUpdate-=OnQuantityChanged;
            quantities.OnAutoIncreaseItemDataUpdate-=OnParameterChanged;
            OnItemCountUpdate=null;OnAutoIncrease=null;
        }
    }
}
