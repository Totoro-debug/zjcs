// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{
public static class HomeCurrencyActionFactory
{
    // HomeUI and its original exchange popup share these selected branches.
    public static IPlayerItemUIHandler Create(PlayerItemType type, PlayerItemUIItemIdProvider id, IHomeCurrencyActionServices services)
    {
        switch(type)
        {
            case PlayerItemType.Money: return new PlayerItemQuickyShopHandler(id.ItemId, services);
            case PlayerItemType.Diamon: return new PlayerDiamondItemUIHandler(id.ItemId, services);
            case PlayerItemType.TiliId: return new PlayerDefaultItemUIHandler(id.ItemId, services);
            case PlayerItemType.FlareStar: return new PlayerFlareStarItemUIHandler(id.ItemId,
                services as IHomeFlareStarActionServices ?? throw new ArgumentException("Original direct-pay confirmation services are required.", nameof(services)));
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
public interface IHomeFlareStarActionServices : IHomeCurrencyActionServices
{
    void PopupPayConfirm(string key, Action confirm);
    void PopupDirectPay();
}
}
