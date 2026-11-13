// Selected original acceleration UI; explicit local services only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Player = Zjcs.Official.HomeFastHangUpUI.IHomeFastHangUpPlayer;
using FastHangUpInfo = Zjcs.Official.HomeFastHangUpUI.HomeFastHangUpInfo;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpUI
{
public static class HangUpUIUtils
{
	public static void OperFastHangUp(IHomeFastHangUpServices services, int fastHangUpId, bool isReceive, Dictionary<int, long> itemCost = null, Action onComplete = null, Action onSuccess = null)
	{
		HomeFastHangUpRequest request = new HomeFastHangUpRequest
		{
			FastHangUpId = fastHangUpId,
			ItemCost = itemCost,
			IsReceive = isReceive,
			IsFree = (itemCost == null)
		};
		services.RequestFastHangUp(request, (HomeFastHangUpResponse rep, int err) =>
		{
			if (err == 0)
			{
				onSuccess?.Invoke();
				services.OpenProgress(fastHangUpId, () =>
					{
						IHomeWindowAwardMessage awardMsg = rep.AwardMsg;
						if (awardMsg != null)
						{
							services.Window.MergeAward(awardMsg.DataList);
							services.Player.Award(awardMsg.DataList, awardMsg.SourceData);
							services.OpenAward(awardMsg);
						}
						onComplete?.Invoke();
				});
			}
		});
	}
}

}
