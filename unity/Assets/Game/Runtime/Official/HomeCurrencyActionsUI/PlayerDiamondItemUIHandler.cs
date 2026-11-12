// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{

public class PlayerDiamondItemUIHandler : IPlayerItemUIHandler
{
    private readonly IHomeCurrencyActionServices services;
    public void HandleClick(Vector2 mousePosition) => services.PopupItemDetail(ItemId, 0L);

	public bool ShowAddBtn => !services.IsPayFunctionLimited;

	public int ItemId { get; set; }

	public PlayerDiamondItemUIHandler(int itemId, IHomeCurrencyActionServices services)
	{
        this.services = services ?? throw new ArgumentNullException(nameof(services));
		ItemId = itemId;
	}

	public void HandleAddBtn()
	{
		services.PopupPayMoneyChange(0L);
	}
}

}
