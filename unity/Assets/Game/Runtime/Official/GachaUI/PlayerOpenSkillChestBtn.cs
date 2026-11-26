using System;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.GachaUI
{
    // Original chest button. The local owner supplies the original chest and
    // quick-buy lookups; PlayerCostMoneyContent owns native input and costs.
    public class PlayerOpenSkillChestBtn : UIBase
    {
        public PlayerCostMoneyContent CostMoneyContent;
        public IGachaChestButtonServices Services { private get; set; }
        private int mCostItemId;
        private int mOpenCount;
        private Action<int> OnHandleItemEnough;
        private Action<long, double, GachaLackItemQuickBuyInfo> OnHandleItemNotEnough;

        private void HandleItemNotEnough(long currentCount, double price)
        {
            long num = (long)price - currentCount;
            if (!Services.TryGetQuickBuyInfo(mCostItemId, num, out var quickBuyInfo))
                CostMoneyContent.BroadcastItemNotEnough(num);
            else if (OnHandleItemNotEnough != null)
                OnHandleItemNotEnough(currentCount, price, quickBuyInfo);
        }

        private void HandleItemEnough()
        {
            if (OnHandleItemEnough != null) OnHandleItemEnough(mOpenCount);
        }

        public void Init(int chestId, int openCount, int costItemId, Action<int> handleItemEnough,
            Action<long, double, GachaLackItemQuickBuyInfo> handleItemNotEnough)
        {
            mOpenCount = openCount;
            mCostItemId = costItemId;
            long count = Services.GetChestCostItemCount(chestId, mCostItemId) * mOpenCount;
            OnHandleItemEnough = handleItemEnough;
            OnHandleItemNotEnough = handleItemNotEnough;
            CostMoneyContent.Init(costItemId, count).SetItemEnoughHandler(HandleItemEnough)
                .SetItemNotEnoughHandler(HandleItemNotEnough);
        }
    }
}
