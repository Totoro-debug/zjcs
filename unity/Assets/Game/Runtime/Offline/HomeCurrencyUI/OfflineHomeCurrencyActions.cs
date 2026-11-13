using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Zjcs.Official.HomeCurrencyActionsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // Original HomeUI handlers consume selected configuration and the actual
    // profile. Secondary item-detail presentation remains an explicit host port.
    public sealed class OfflineHomeCurrencyActions : IHomeFlareStarActionServices
    {
        private sealed class Condition : IHomeCurrencyPurchaseCondition
        {
            public readonly JObject Value;
            public Condition(JObject value) { Value = value; }
        }
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfflineHomeCurrencyPlayer quantities;
        private readonly OfflineHomeCurrencyAssets assets;
        private readonly OfflineHomeCurrencyWindows windows;
        private readonly Func<JObject, bool> isMet;
        private readonly Func<bool> isPayFunctionLimited;
        private readonly Action<int, long> popupItemDetail;
        private readonly Action popupDirectPay;

        public OfflineHomeCurrencyActions(OfflineMainlineProgressSession profile,
            OfflineHomeCurrencyPlayer quantities, OfflineHomeCurrencyAssets assets, OfflineHomeCurrencyWindows windows,
            Func<JObject, bool> isMet, Func<bool> isPayFunctionLimited, Action<int, long> popupItemDetail, Action popupDirectPay)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.quantities = quantities ?? throw new ArgumentNullException(nameof(quantities));
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
            this.isMet = isMet ?? throw new ArgumentNullException(nameof(isMet));
            this.isPayFunctionLimited = isPayFunctionLimited ?? throw new ArgumentNullException(nameof(isPayFunctionLimited));
            this.popupItemDetail = popupItemDetail ?? throw new ArgumentNullException(nameof(popupItemDetail));
            this.popupDirectPay = popupDirectPay ?? throw new ArgumentNullException(nameof(popupDirectPay));
        }

        public IPlayerItemUIHandler CreateHandler(PlayerItemType type, PlayerItemUIItemIdProvider id)
            => HomeCurrencyActionFactory.Create(type, id, this);
        public HomeCurrencyQuickBuyInfo GetQuickBuyInfo(int itemId)
        {
            var row = assets.QuickBuyRow(itemId);
            if (row == null) return null;
            return new HomeCurrencyQuickBuyInfo { ItemId = (int)row["ItemId"],
                Condition = row["Condition"] is JObject condition ? new Condition(condition) : null };
        }
        public bool IsMet(IHomeCurrencyPurchaseCondition condition)
            => isMet((condition as Condition ?? throw new ArgumentException("Selected currency condition is required.", nameof(condition))).Value);
        public bool IsPayFunctionLimited => isPayFunctionLimited();
        public HomeCurrencyActionItemInfo GetItemInfo(int itemId)
        {
            var info = quantities.GetItemInfo(itemId);
            return new HomeCurrencyActionItemInfo { ClassId = itemId, HasAutoIncrease = info.HasAutoIncrease };
        }
        public long GetAutoIncreaseMaxCount(int itemId)
        {
            var item = profile.Save.Player.Inventory.FirstOrDefault(row => row.ItemId == itemId);
            return item?.AutoIncrease?.MaxCount
                ?? throw new InvalidOperationException("Original currency auto-increase parameter is absent.");
        }
        public void PopupItemDetail(int itemId, long count) => popupItemDetail(itemId, count);
        public void PopupQuickBuy(int itemId, long count) => windows.ShowQuickBuy(itemId, count);
        public void PopupPayMoneyChange(long count) => windows.ShowExchange(count);
        public void PopupPayConfirm(string key, Action confirm) => windows.ShowConfirm(key, null, confirm);
        public void PopupDirectPay() => popupDirectPay();
    }
}
