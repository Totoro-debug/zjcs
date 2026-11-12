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
public static class HomeCurrencyBuyLimit
{
	public static async UniTask<bool> CheckBuyLimitCountItem(int itemId, long count, IHomeCurrencyWindowServices services, Func<string,string> localize, Action<Action<Zjcs.Official.WorldUI.WorldConfirmDialog>> popup)
	{
		HomeCurrencyWindowItemInfo itemInfo = services.GetItemInfo(itemId);
		if (itemInfo == null)
		{
			return false;
		}
		if (!itemInfo.IsCountLimit)
		{
			return true;
		}
		if (services.Player.GetItemAmount(itemId) + count <= itemInfo.ItemCountLimitInfo.MaxCount)
		{
			return true;
		}
		bool confirm = false;
		bool cancle = false;
		string text = localize(services.GetItemNameKey(itemId));
		object[] confirmText = new object[] { text };
		popup(go =>
		{
			go.Init("ui_item_max_count_buy_confirm_text", confirmText, confirmAction, cancelAction);
			go.SetCancelCallbackAutoExecute(auto: true);
		});
		await UniTask.WaitUntil(() => confirm | cancle);
		if (confirm)
		{
			return true;
		}
		return false;
		void cancelAction()
		{
			cancle = true;
		}
		void confirmAction()
		{
			confirm = true;
		}
	}

}
}
