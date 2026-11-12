// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{

public class PlayerItemQuickyShopHandler : IPlayerItemUIHandler
{
    private readonly IHomeCurrencyActionServices services;
    public void HandleClick(Vector2 mousePosition) => services.PopupItemDetail(ItemId, 0L);

	private PlayerDefaultItemUIHandler mPlayerDefaultItemUIHandler;

	private HomeCurrencyQuickBuyInfo mInfo;

	public int ItemId
	{
		get
		{
			return mPlayerDefaultItemUIHandler.ItemId;
		}
		set
		{
			mPlayerDefaultItemUIHandler.ItemId = value;
		}
	}

	public bool ShowAddBtn
	{
		get
		{
			if (mInfo == null)
			{
				return false;
			}
			if (mInfo.Condition != null && !services.IsMet(mInfo.Condition))
			{
				return false;
			}
			return true;
		}
	}

	public PlayerItemQuickyShopHandler(int itemId, IHomeCurrencyActionServices services)
	{
        this.services = services ?? throw new ArgumentNullException(nameof(services));
		mInfo = services.GetQuickBuyInfo(itemId);
		mPlayerDefaultItemUIHandler = new PlayerDefaultItemUIHandler(itemId, services);
	}

	public void HandleAddBtn()
	{
		if (mInfo != null)
		{
			services.PopupQuickBuy(mInfo.ItemId, 0L);
		}
	}
}

}
