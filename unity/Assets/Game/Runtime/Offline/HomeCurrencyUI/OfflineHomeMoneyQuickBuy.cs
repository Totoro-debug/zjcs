using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // HomeUI requests the Money quick-buy entry. Pricing and quantity derive
    // from the selected QuickBuyInfo / QuickBuyUtils; one local transaction
    // replaces the request, inventory response, award and count-update messages.
    public sealed class OfflineHomeMoneyQuickBuy
    {
        readonly OfflineMainlineProgressSession profile;
        readonly OfflineHomeCurrencyAssets assets;
        readonly OfflineHomeQuickBuyCounters counters;
        readonly Func<JObject, OfflineMainlineSave, bool> isMet;
        readonly Func<OfflinePlayerProgress, int, bool> isBanCostItem;
        readonly Func<DateTime> clock;

        public OfflineHomeMoneyQuickBuy(OfflineMainlineProgressSession profile,
            OfflineHomeCurrencyAssets assets, HomeQuickBuyCalendar calendar,
            Func<JObject, OfflineMainlineSave, bool> isMet,
            Func<OfflinePlayerProgress, int, bool> isBanCostItem, Func<DateTime> clock = null)
        {
            this.profile=profile??throw new ArgumentNullException(nameof(profile));
            this.assets=assets?assets:throw new ArgumentNullException(nameof(assets));
            counters=new OfflineHomeQuickBuyCounters(calendar);
            this.isMet=isMet??throw new ArgumentNullException(nameof(isMet));
            this.isBanCostItem=isBanCostItem??throw new ArgumentNullException(nameof(isBanCostItem));
            this.clock=clock??(()=>DateTime.UtcNow);
        }

        public ProgressionResult Buy(string requestId,long count)
        {
            string receipt="home-money-quick-buy:"+requestId;
            if(string.IsNullOrWhiteSpace(requestId)||receipt.Length>160||count<=0)
                return Rejected("A positive purchase quantity and request identity are required.");
            try
            {
                profile.Reload();
                if(profile.Save.Player.AppliedInventoryEventIds.Contains(receipt))
                    return new ProgressionResult{Status=ProgressionResultStatus.Unchanged};
                var info=assets.QuickBuy(assets.MoneyId);
                if(info==null) return Rejected("Original money quick-buy configuration is unavailable.");
                var condition=assets.QuickBuyRow(info.ItemId)["Condition"] as JObject;
                if(condition!=null&&!isMet(condition,profile.Save))
                    return Rejected("Original quick-buy condition is not met.");
                if(isBanCostItem(profile.Save.Player,info.CurrencyId))
                    return Rejected("Quick-buy currency cannot currently be spent.");
                DateTime now=clock();
                long bought=counters.GetBuyCount(profile.Save.Player,info.ItemId,info.RefreshType,now);
                long available=profile.InventoryCount(info.CurrencyId);
                if(count>info.GetMaxBuyCount(bought,available)||!info.GetPrice(bought,count,out long price)||price<0)
                    return Rejected("Insufficient currency or original purchase limit reached.");
                long award=checked(info.GetAwardItemCount(profile.Save.Player.Level,profile.Save.Player.SubRank)*count);
                if(award<=0) return Rejected("Original quick-buy reward is not positive.");
                long unix=new DateTimeOffset(now).ToUnixTimeSeconds();
                return profile.ApplyInventoryTransaction(receipt,save=>
                {
                    if(price>0&&!OfflineInventoryOperations.Consume(save.Player,info.CurrencyId,price,unix).IsApplied)
                        throw new InvalidOperationException("Quick-buy currency debit was rejected.");
                    if(!OfflineInventoryOperations.Grant(save.Player,info.ItemId,award,unix).IsApplied)
                        throw new InvalidOperationException("Quick-buy reward was rejected.");
                    counters.AddBuyCount(save.Player,info.RefreshType,info.ItemId,count,now);
                });
            }
            catch(IOException error) { return Rejected(error.Message); }
            catch(UnauthorizedAccessException error) { return Rejected(error.Message); }
            catch(OverflowException error) { return Rejected(error.Message); }
        }

        static ProgressionResult Rejected(string message)=>new ProgressionResult{
            Status=ProgressionResultStatus.InvalidState,Message=message};
    }
}
