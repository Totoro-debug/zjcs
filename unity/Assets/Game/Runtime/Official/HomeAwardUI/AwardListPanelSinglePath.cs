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
public class AwardListPanelSinglePath : AwardListPanelBasePath
{
	[SerializeField]
	private float mMinNextIntervalTime = 0.6f;

	private AwardListPanelItem mAwardListPanelItem;

	private float mLastSetItemTime = -1f;

	public override bool CanSetItem()
	{
		if (!((Object)mAwardListPanelItem == (Object)null))
		{
			return Time.time - mLastSetItemTime >= mMinNextIntervalTime;
		}
		return true;
	}

	public override void SetItem(AwardListPanelItem awardListPanelItem)
	{
		base.SetItem(awardListPanelItem);
		if ((Object)mAwardListPanelItem != (Object)null)
		{
			mAwardListPanelItem.ReturnPool();
		}
		mLastSetItemTime = Time.time;
		mAwardListPanelItem = awardListPanelItem;
	}

	public override void RemoveItem(AwardListPanelItem item)
	{
		base.RemoveItem(item);
		mAwardListPanelItem = null;
	}
}
}
