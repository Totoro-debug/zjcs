using System;

namespace Zjcs.Official.GachaUI
{
    // Original QuickBuyUtils.LackItemQuickBuyInfo payload, carried unchanged
    // from the cost check to the enclosing panel's confirmation flow.
    public struct GachaLackItemQuickBuyInfo
    {
        public int ItemId, CurrencyId;
        public long Price, BuyCount, GetItemCount;
    }

    public interface IGachaChestButtonServices
    {
        long GetChestCostItemCount(int chestId, int costItemId);
        bool TryGetQuickBuyInfo(int requireItemId, long requireCount, out GachaLackItemQuickBuyInfo info);
    }

    public interface IGachaDailyCountServices
    {
        long GetGachaDailyCount(GachaClassType type);
        long GetDailyGachaMaxCount(GachaClassType type);
        event Action<GachaType, int> OnGacha;
    }
}
