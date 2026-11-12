using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // Navigation, calendar messages and award presentation belong to the enclosing
    // home scope. They must be supplied explicitly; no secondary window is faked.
    public interface IOfflineHomeCurrencyWindowPresentation
    {
        IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> listener);
        void ShowLackOfItem(int itemId, long count, IWindowOperator owner);
        void ShowError(int error, string itemName);
        void SystemMessage(string text);
        void PopupItemDetail(IWindowOperator owner, int itemId, long count);
        void PopupDirectPay(IWindowOperator owner, Action initialized);
        void TransactionFailed(int itemId, ProgressionResult result);
        bool AwardServiceAvailable { get; }
        void RegisterAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler);
        void RemoveAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler);
        bool IsPayAward(HomeCurrencyAwardMessage message);
        void PopupAward(HomeCurrencyAwardMessage message);
    }

    // One production service boundary for both selected original windows. It
    // composes the actual profile transactions and their shared count source.
    // The enclosing scope owns these dependencies and disposes windows first.
    public sealed class OfflineHomeCurrencyWindowServices : IHomeCurrencyWindowServices, IDisposable
    {
        private readonly OfflineHomeCurrencyAssets assets;
        private readonly OfflineHomeCurrencyWindowPlayer player;
        private readonly OfflineHomeCurrencyPlayer quantities;
        private readonly OfflineHomeMoneyQuickBuy quickBuy;
        private readonly OfflineHomeCurrencyExchange exchange;
        private readonly IOfflineHomeCurrencyWindowPresentation presentation;
        private readonly Func<string, string> createRequestId;
        private readonly List<Subscription> subscriptions = new List<Subscription>();
        private OfflineHomeCurrencyWindows windows;
        private bool disposed;

        public OfflineHomeCurrencyWindowServices(OfflineHomeCurrencyAssets assets,
            OfflineHomeCurrencyWindowPlayer player, OfflineHomeCurrencyPlayer quantities,
            OfflineHomeMoneyQuickBuy quickBuy, OfflineHomeCurrencyExchange exchange,
            IOfflineHomeCurrencyWindowPresentation presentation, Func<string, string> createRequestId)
        {
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.quantities = quantities ?? throw new ArgumentNullException(nameof(quantities));
            this.quickBuy = quickBuy ?? throw new ArgumentNullException(nameof(quickBuy));
            this.exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            this.createRequestId = createRequestId ?? throw new ArgumentNullException(nameof(createRequestId));
        }

        // Resolve the window/service construction cycle once, before showing UI.
        public void AttachWindows(OfflineHomeCurrencyWindows value)
        {
            RequireAlive();
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (windows != null) throw new InvalidOperationException("Currency windows are already attached.");
            windows = value;
        }

        private OfflineHomeCurrencyWindows Windows
        {
            get { RequireAlive(); return windows ?? throw new InvalidOperationException("Attach original currency windows before use."); }
        }
        public IHomeCurrencyWindowPlayer Player { get { RequireAlive(); return player; } }
        public int MoneyId => assets.MoneyId;
        public int DiamonId => assets.DiamonId;
        public int FlareStarId => assets.FlareStarId;
        public HomeCurrencyPaySettings Pay => assets.Pay;
        public Color White => assets.White;
        public Color LightRed => assets.LightRed;
        public HomeCurrencyWindowItemInfo GetItemInfo(int itemId) => assets.Item(itemId);
        public QuickBuyInfo GetQuickBuyInfo(int itemId) => assets.QuickBuy(itemId);
        public long GetItemCountLimit(int itemId) => assets.CountLimit(itemId);
        public string GetItemNameKey(int itemId) => "item_" + itemId + "_name";
        public string QuickBuyLimitedCountTextKey(EQuickBuyRefreshType type) => "ui_quick_buy_" + type + "_limited";
        public IProfessionAssetOperation<Sprite> LoadSprite(string path) => Windows.LoadSprite(path);
        public bool IconServiceAvailable => !disposed && windows != null;
        public ItemIcon GetIcon(HomeCurrencyWindowItemInfo item, Transform slot) => Windows.GetIcon(item, slot);
        public void RecycleIcon(ItemIcon icon) => Windows.RecycleIcon(icon);

        public IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> listener)
        {
            RequireAlive();
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var token = presentation.SubscribeEnterNewDay(listener)
                ?? throw new InvalidOperationException("The home calendar must return a subscription.");
            return Track(token.Dispose);
        }
        public IDisposable SubscribeAutoIncrease(Action<HomeCurrencyAutoIncreaseEvent> listener)
        {
            RequireAlive();
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            player.OnAutoIncrease += listener;
            return Track(() => player.OnAutoIncrease -= listener);
        }
        public UniTask<bool> CheckBuyLimitCountItem(int itemId, long awardItemCount)
        {
            RequireAlive();
            return HomeCurrencyBuyLimit.CheckBuyLimitCountItem(itemId, awardItemCount, this,
                assets.Text, initialize => Windows.PopupConfirm(initialize));
        }
        public void PlayerQuickBuy(int itemId, long count)
        {
            RequireAlive();
            if (itemId != MoneyId) throw new NotSupportedException("Only the selected home money transaction is bound.");
            var result = quickBuy.Buy(createRequestId("quick-buy"), count);
            quantities.SynchronizeCommittedPlayer();
            if (!result.IsApplied) presentation.TransactionFailed(itemId, result);
        }
        public void PlayerDiamonChange(long count, Action success)
        {
            RequireAlive();
            var result = exchange.Exchange(createRequestId("exchange"), count);
            quantities.SynchronizeCommittedPlayer();
            if (result.IsApplied) success?.Invoke();
            else presentation.TransactionFailed(DiamonId, result);
        }
        public void ShowLackOfItem(int itemId, long count, IWindowOperator owner) => presentation.ShowLackOfItem(itemId, count, owner);
        public void ShowError(int error, string itemName) => presentation.ShowError(error, itemName);
        public void SystemMessage(string text) => presentation.SystemMessage(text);
        public void PopupItemDetail(IWindowOperator owner, int itemId, long count) => presentation.PopupItemDetail(owner, itemId, count);
        public void PopupConfirm(IWindowOperator owner, string key, Action confirm) => Windows.ShowConfirm(key, null, confirm);
        public void PopupDirectPay(IWindowOperator owner, Action initialized) => presentation.PopupDirectPay(owner, initialized);
        public void Close(GameObject window) => Windows.Close(window);
        public void ClosePayMoneyChangeDialog() => Windows.CloseExchange();
        public bool AwardServiceAvailable => !disposed && presentation.AwardServiceAvailable;
        public void RegisterAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler) { RequireAlive(); presentation.RegisterAwardHandler(handler); }
        public void RemoveAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler) => presentation.RemoveAwardHandler(handler);
        public bool IsPayAward(HomeCurrencyAwardMessage message) => presentation.IsPayAward(message);
        public void PopupAward(HomeCurrencyAwardMessage message) => presentation.PopupAward(message);

        private IDisposable Track(Action release)
        {
            var token = new Subscription(() => release());
            token.Removed = () => subscriptions.Remove(token);
            subscriptions.Add(token);
            return token;
        }
        private void RequireAlive() { if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeCurrencyWindowServices)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            while (subscriptions.Count > 0) subscriptions[subscriptions.Count - 1].Dispose();
            windows = null;
        }
        private sealed class Subscription : IDisposable
        {
            private Action release;
            public Action Removed;
            public Subscription(Action release) { this.release = release; }
            public void Dispose()
            {
                var action = release;
                if (action == null) return;
                release = null;
                try { action(); }
                finally { Removed(); }
            }
        }
    }
}
