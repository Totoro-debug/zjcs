// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{
public interface IHomeCurrencyPurchaseCondition { }
public sealed class HomeCurrencyQuickBuyInfo { public int ItemId; public IHomeCurrencyPurchaseCondition Condition; }
public sealed class HomeCurrencyActionItemInfo { public int ClassId; public bool HasAutoIncrease; }
public interface IHomeCurrencyActionServices
{
    HomeCurrencyQuickBuyInfo GetQuickBuyInfo(int itemId);
    bool IsMet(IHomeCurrencyPurchaseCondition condition);
    bool IsPayFunctionLimited { get; }
    HomeCurrencyActionItemInfo GetItemInfo(int itemId);
    long GetAutoIncreaseMaxCount(int itemId);
    void PopupItemDetail(int itemId, long count);
    void PopupQuickBuy(int itemId, long count);
    void PopupPayMoneyChange(long count);
}
}
