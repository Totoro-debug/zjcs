// Selected original currency-window implementation; typed local service boundaries.
using Object=UnityEngine.Object;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
using StateSwitcher=Zjcs.Official.WorldUI.WorldStateSwitcher<bool>;
using PlayerItemDataWrap=Zjcs.Official.ProfessionUI.IProfessionItemData;
using AwardSourceData=System.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeCurrencyWindowsUI
{
public sealed class HomeCurrencyPaySettings
{
    public long DiamonChangeScale, DiamonChangeMin, DiamonChangeMax;
}
public sealed class HomeCurrencyWindowItemInfo
{
    public int Id;
    public string Icon, Name;
    public bool HasAutoIncrease;
    public HomeCurrencyWindowItemCountLimitInfo ItemCountLimitInfo;
    public bool IsCountLimit => ItemCountLimitInfo != null;
}
public sealed class HomeCurrencyWindowItemCountLimitInfo { public long MaxCount; }
public sealed class HomeCurrencyAutoIncreaseEvent { public int ItemId; }
public sealed class HomeCurrencyEnterNewDay { }
public sealed class HomeCurrencyAwardMessage { public object Value; }
public interface IHomeCurrencyWindowPlayer
{
    int Level { get; }
    int SubRank { get; }
    long GetItemAmount(int itemId);
    bool HasItem(int itemId, long count);
    bool IsBanCostItem(int itemId);
    long QuickBuyGetItemBuyCount(int itemId, EQuickBuyRefreshType refreshType);
    event Action<IProfessionItemData,long,object> OnItemCountUpdate;
}
public interface IHomeCurrencyWindowServices
{
    IHomeCurrencyWindowPlayer Player { get; }
    int MoneyId { get; }
    int DiamonId { get; }
    int FlareStarId { get; }
    HomeCurrencyPaySettings Pay { get; }
    Color White { get; }
    Color LightRed { get; }
    HomeCurrencyWindowItemInfo GetItemInfo(int itemId);
    QuickBuyInfo GetQuickBuyInfo(int itemId);
    long GetItemCountLimit(int itemId);
    string GetItemNameKey(int itemId);
    string QuickBuyLimitedCountTextKey(EQuickBuyRefreshType type);
    IProfessionAssetOperation<Sprite> LoadSprite(string path);
    bool IconServiceAvailable { get; }
    ItemIcon GetIcon(HomeCurrencyWindowItemInfo item, Transform slot);
    void RecycleIcon(ItemIcon icon);
    IDisposable SubscribeEnterNewDay(Action<HomeCurrencyEnterNewDay> action);
    IDisposable SubscribeAutoIncrease(Action<HomeCurrencyAutoIncreaseEvent> action);
    UniTask<bool> CheckBuyLimitCountItem(int itemId, long awardItemCount);
    void PlayerQuickBuy(int itemId, long count);
    void PlayerDiamonChange(long count, Action success);
    void ShowLackOfItem(int itemId, long count, IWindowOperator owner);
    void ShowError(int error, string itemName);
    void SystemMessage(string text);
    void PopupItemDetail(IWindowOperator owner, int itemId, long count);
    void PopupConfirm(IWindowOperator owner, string key, Action confirm);
    void PopupDirectPay(IWindowOperator owner, Action initialized);
    void Close(GameObject window);
    void ClosePayMoneyChangeDialog();
    bool AwardServiceAvailable { get; }
    void RegisterAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler);
    void RemoveAwardHandler(Func<HomeCurrencyAwardMessage,bool> handler);
    bool IsPayAward(HomeCurrencyAwardMessage message);
    void PopupAward(HomeCurrencyAwardMessage message);
}
}
