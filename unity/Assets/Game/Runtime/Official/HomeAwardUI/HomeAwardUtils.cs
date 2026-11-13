// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
public static class HomeAwardUtils
{
	public static bool IsAwardDataAgiotage(this AwardData data, out bool isAgiotage, out bool isRecycle)
	{
		isAgiotage = data.Agiotage > 0;
		isRecycle = data.RecycleAmount > 0;
		return isAgiotage | isRecycle;
	}

	public static void GetDetailAward(this AwardData data, out (int itemId, long count) agiotageAward, out (int itemId, long count) recycleAward)
	{
		agiotageAward.itemId = data.AgiotageId;
		agiotageAward.count = ((data.Agiotage > 0) ? data.Agiotage : data.Amount) - data.RecycleAmount;
		recycleAward = default;
		if (data.RecycleAwards != null && data.RecycleAwards.Count > 0)
		{
			RecycleAwardData recycleAwardData = data.RecycleAwards[0];
			recycleAward.itemId = recycleAwardData.AwardId;
			recycleAward.count = recycleAwardData.Amount;
		}
	}

}
}
