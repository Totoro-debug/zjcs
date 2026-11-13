// Selected original client; local ownership and native animation endpoints.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;
using AppearanceUI = Zjcs.Official.CombatAppearance.AppearanceUI;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpProgressUI
{
public static class HangUpUtils
{
	public static void GetFastHangUpRewardItems(Zjcs.Official.HomeFastHangUpUI.IHomeFastHangUpServices services, int fastHangUpId, ref List<ItemInfo> items)
	{
		Zjcs.Official.HomeFastHangUpUI.HomeFastHangUpInfo fastHangUpInfo = services.GetFastHangUpInfo(fastHangUpId);
		foreach (int hangUpId in fastHangUpInfo.HangUpIdList)
		{
			if (services.Player.HasUnlockedHangUp(hangUpId))
			{
				ItemInfo hangUpRewardItem = services.GetHangUpRewardItem(hangUpId);
				if (hangUpRewardItem != null)
				{
					items.Add(hangUpRewardItem);
				}
			}
		}
		foreach (int extraHangUpReward in fastHangUpInfo.ExtraHangUpRewardList)
		{
			ItemInfo hangUpRewardItem2 = services.GetHangUpRewardItem(extraHangUpReward);
			if (hangUpRewardItem2 != null)
			{
				items.Add(hangUpRewardItem2);
			}
		}
	}

}

}
