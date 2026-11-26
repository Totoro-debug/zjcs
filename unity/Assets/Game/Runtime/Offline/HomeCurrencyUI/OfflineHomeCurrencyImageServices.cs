using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // A feature may supply additional original item images while sharing the
    // existing currency/player/window behavior and its immutable base catalog.
    public sealed class OfflineHomeCurrencyImageServices : IHomeCurrencyWindowServices
    {
        private readonly IHomeCurrencyWindowServices inner;
        private readonly OfflineProfessionImageResourceLoader images;

        public OfflineHomeCurrencyImageServices(IHomeCurrencyWindowServices inner, OfflineProfessionImageCatalog catalog)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            images = new OfflineProfessionImageResourceLoader(catalog);
        }

        public IProfessionAssetOperation<Sprite> LoadSprite(string path)
        {
            var operation = images.LoadAsset<Sprite>("Assets/Image/" + path);
            if (operation.Result) return operation;
            operation.Dispose();
            return inner.LoadSprite(path);
        }

        public IHomeCurrencyWindowPlayer Player => inner.Player;
        public int MoneyId => inner.MoneyId;
        public int DiamonId => inner.DiamonId;
        public int FlareStarId => inner.FlareStarId;
        public HomeCurrencyPaySettings Pay => inner.Pay;
        public Color White => inner.White;
        public Color LightRed => inner.LightRed;
        public HomeCurrencyWindowItemInfo GetItemInfo(int id) => inner.GetItemInfo(id);
        public QuickBuyInfo GetQuickBuyInfo(int id) => inner.GetQuickBuyInfo(id);
        public long GetItemCountLimit(int id) => inner.GetItemCountLimit(id);
        public string GetItemNameKey(int id) => inner.GetItemNameKey(id);
        public string QuickBuyLimitedCountTextKey(EQuickBuyRefreshType type) => inner.QuickBuyLimitedCountTextKey(type);
        public bool IconServiceAvailable => inner.IconServiceAvailable;
        public ItemIcon GetIcon(HomeCurrencyWindowItemInfo item, Transform slot) => inner.GetIcon(item, slot);
        public void RecycleIcon(ItemIcon icon) => inner.RecycleIcon(icon);
        public IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> action) => inner.SubscribeEnterNewDay(action);
        public IDisposable SubscribeAutoIncrease(Action<HomeCurrencyAutoIncreaseEvent> action) => inner.SubscribeAutoIncrease(action);
        public UniTask<bool> CheckBuyLimitCountItem(int id, long count) => inner.CheckBuyLimitCountItem(id, count);
        public void PlayerQuickBuy(int id, long count) => inner.PlayerQuickBuy(id, count);
        public void PlayerDiamonChange(long count, Action success) => inner.PlayerDiamonChange(count, success);
        public void ShowLackOfItem(int id, long count, IWindowOperator owner) => inner.ShowLackOfItem(id, count, owner);
        public void ShowError(int error, string itemName) => inner.ShowError(error, itemName);
        public void SystemMessage(string text) => inner.SystemMessage(text);
        public void PopupItemDetail(IWindowOperator owner, int id, long count) => inner.PopupItemDetail(owner, id, count);
        public void PopupConfirm(IWindowOperator owner, string key, Action confirm) => inner.PopupConfirm(owner, key, confirm);
        public void PopupDirectPay(IWindowOperator owner, Action initialized) => inner.PopupDirectPay(owner, initialized);
        public void Close(GameObject window) => inner.Close(window);
        public void ClosePayMoneyChangeDialog() => inner.ClosePayMoneyChangeDialog();
        public bool AwardServiceAvailable => inner.AwardServiceAvailable;
        public void RegisterAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler) => inner.RegisterAwardHandler(handler);
        public void RemoveAwardHandler(Func<HomeCurrencyAwardMessage, bool> handler) => inner.RemoveAwardHandler(handler);
        public bool IsPayAward(HomeCurrencyAwardMessage message) => inner.IsPayAward(message);
        public void PopupAward(HomeCurrencyAwardMessage message) => inner.PopupAward(message);
    }
}
