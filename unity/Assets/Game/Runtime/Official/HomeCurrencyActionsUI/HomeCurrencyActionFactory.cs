// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{
public static class HomeCurrencyActionFactory
{
    // HomeUI serializes exactly these three types. Other windows retain their
    // own factories and are deliberately rejected by this scoped entry point.
    public static IPlayerItemUIHandler Create(PlayerItemType type, PlayerItemUIItemIdProvider id, IHomeCurrencyActionServices services)
    {
        switch(type)
        {
            case PlayerItemType.Money: return new PlayerItemQuickyShopHandler(id.ItemId, services);
            case PlayerItemType.Diamon: return new PlayerDiamondItemUIHandler(id.ItemId, services);
            case PlayerItemType.TiliId: return new PlayerDefaultItemUIHandler(id.ItemId, services);
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
}
