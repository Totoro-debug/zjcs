// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{

public class PlayerFlareStarItemUIHandler : IPlayerItemUIHandler
{
    private readonly IHomeFlareStarActionServices services;
    public void HandleClick(Vector2 mousePosition) => services.PopupItemDetail(ItemId, 0L);

	public bool ShowAddBtn => !services.IsPayFunctionLimited;

	public int ItemId { get; set; }

	public PlayerFlareStarItemUIHandler(int itemId, IHomeFlareStarActionServices services)
	{
        this.services = services ?? throw new ArgumentNullException(nameof(services));
		ItemId = itemId;
	}

	public void HandleAddBtn()
	{
		services.PopupPayConfirm("UI_Pay_Confirm_Goto_Pay_Panel", () => services.PopupDirectPay());
	}
}

}
