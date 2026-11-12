using System;
using System.IO;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // The selected PayMoneyChangeDialog supplies quantity/rate rules. Both
    // inventory changes and the request receipt commit in the shared profile.
    public sealed class OfflineHomeCurrencyExchange
    {
        readonly OfflineMainlineProgressSession profile;
        readonly OfflineHomeCurrencyAssets assets;
        readonly Func<long> clock;

        public OfflineHomeCurrencyExchange(OfflineMainlineProgressSession profile,
            OfflineHomeCurrencyAssets assets, Func<long> clock=null)
        {
            this.profile=profile??throw new ArgumentNullException(nameof(profile));
            this.assets=assets?assets:throw new ArgumentNullException(nameof(assets));
            this.clock=clock??(()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public ProgressionResult Exchange(string requestId,long count)
        {
            string receipt="home-currency-exchange:"+requestId;
            if(string.IsNullOrWhiteSpace(requestId)||receipt.Length>160)
                return Rejected("An exchange request identity is required.");
            try
            {
                profile.Reload();
                if(profile.Save.Player.AppliedInventoryEventIds.Contains(receipt))
                    return new ProgressionResult{Status=ProgressionResultStatus.Unchanged};
                var pay=assets.Pay;
                if(count<pay.DiamonChangeMin||count>pay.DiamonChangeMax||count<=0)
                    return Rejected("Exchange quantity is outside the original configured range.");
                long granted=checked(count*pay.DiamonChangeScale);
                long now=clock();
                return profile.ApplyInventoryTransaction(receipt,save=>
                {
                    var consumed=OfflineInventoryOperations.Consume(save.Player,assets.FlareStarId,count,now);
                    if(!consumed.IsApplied)
                        throw new InvalidOperationException("Insufficient exchange currency.");
                    var awarded=OfflineInventoryOperations.Grant(save.Player,assets.DiamonId,granted,now);
                    if(!awarded.IsApplied)
                        throw new InvalidOperationException("Exchange currency grant was rejected.");
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
