// Original selected home currency handler; typed local parser/player/window ports.
using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeCurrencyActionsUI
{

public class PlayerDefaultItemUIHandler : IPlayerItemUIHandler, IPlayerItemUIWithLimit
{
    private readonly IHomeCurrencyActionServices services;
    public void HandleClick(Vector2 mousePosition) => services.PopupItemDetail(ItemId, 0L);

	public bool ShowAddBtn => false;

	public int ItemId { get; set; }

	public PlayerDefaultItemUIHandler(int itemId, IHomeCurrencyActionServices services)
	{
        this.services = services ?? throw new ArgumentNullException(nameof(services));
		ItemId = itemId;
	}

	public void HandleAddBtn()
	{
		throw new NotImplementedException();
	}

	public bool TryGetMaxCountLimit(out long maxLimitCount)
	{
		HomeCurrencyActionItemInfo itemInfo = services.GetItemInfo(ItemId);
		maxLimitCount = 0L;
		if (!itemInfo.HasAutoIncrease)
		{
			return false;
		}
		maxLimitCount = services.GetAutoIncreaseMaxCount(itemInfo.ClassId);
		return true;
	}
}

}
