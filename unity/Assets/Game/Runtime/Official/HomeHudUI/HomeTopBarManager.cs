using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHudUI
{
    // Original TopBarManager; dependency injection supplies the same native
    // pools and item-ID factory through the local composition boundary.
    public sealed class HomeTopBarManager : ITopBarManager
    {
        private readonly GameObjectPool<TopBarItem> mTopBarPool;
        private readonly GameObjectPool<MePlayerItemUI> mCurrencyPool;
        private readonly Func<PlayerItemType, int> getItemId;

        public HomeTopBarManager(GameObjectPool<TopBarItem> topBarPool,
            GameObjectPool<MePlayerItemUI> currencyPool, Func<PlayerItemType, int> getItemId)
        {
            mTopBarPool = topBarPool ?? throw new ArgumentNullException(nameof(topBarPool));
            mCurrencyPool = currencyPool ?? throw new ArgumentNullException(nameof(currencyPool));
            this.getItemId = getItemId ?? throw new ArgumentNullException(nameof(getItemId));
        }

        private TopBarItem BorrowTopBarInPool()
        {
            TopBarItem topBarItem = mTopBarPool.Borrow();
            if (!topBarItem.name.EndsWith("GameObjectPoolCSLeakerIgnoreAllChildren"))
                topBarItem.name += "_GameObjectPoolCSLeakerIgnoreAllChildren";
            return topBarItem;
        }

        private MePlayerItemUI BorrowCurrencyInPool()
        {
            MePlayerItemUI item = mCurrencyPool.Borrow();
            if (!item.name.EndsWith("GameObjectPoolCSLeakerIgnoreAllChildren"))
                item.name += "_GameObjectPoolCSLeakerIgnoreAllChildren";
            return item;
        }

        public TopBarItem BorrowTopBar(List<CurrencyItemData> datas)
        {
            TopBarItem item = BorrowTopBarInPool();
            foreach (CurrencyItemData data in datas) item.SetCurrencyItem(BorrowCurrency(data));
            return item;
        }

        public void ReturnTopBar(TopBarItem topBar)
        {
            ReturnTopBarAllCurrency(topBar);
            mTopBarPool.Return(topBar);
        }

        public MePlayerItemUI BorrowCurrency(CurrencyItemData data)
        {
            MePlayerItemUI item = BorrowCurrencyInPool();
            if (data.ItemIdProivder.ItemId <= 0) data.ItemIdProivder.ItemId = getItemId(data.Type);
            item.Init(data.Type, data.ItemIdProivder);
            return item;
        }

        public void ReturnCurrency(MePlayerItemUI currency) => mCurrencyPool.Return(currency);

        public void UpdateTopBar(TopBarItem topBar, List<CurrencyItemData> data)
        {
            ReturnTopBarAllCurrency(topBar);
            if (data == null) return;
            foreach (CurrencyItemData datum in data) topBar.SetCurrencyItem(BorrowCurrency(datum));
        }

        private void ReturnTopBarAllCurrency(TopBarItem topBar)
        {
            List<IProfessionCurrencyItem> items = topBar.GetAllCurrencyItems();
            foreach (IProfessionCurrencyItem item in items) ReturnCurrency((MePlayerItemUI)item);
            items.Clear();
        }
    }
}
