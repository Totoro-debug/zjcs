// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {
using System;
using System.Collections.Generic;





using UnityEngine;

public class TreasureRedDotItem : TreasurePlayerRedDotItemBase
{
	[SerializeField]
	private List<TreasureRedDotEnum> mRedDots;

	private IDisposable mSettingSubscription;

	private int mItemId;



	protected override void OnEnable()
	{

		base.OnEnable();
		Services.OnTreasureRefresh += MainPlayer_OnTreasureRefresh;
		Services.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;

		mSettingSubscription = Services.SubscribeSettings(OnSettingChanged);
	}

	protected override void OnDisable()
	{
		mSettingSubscription?.Dispose();
        mSettingSubscription=null;
		if (Services != null)
		{
			Services.OnTreasureRefresh -= MainPlayer_OnTreasureRefresh;
			Services.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		mItemId = 0;
		base.OnDisable();
	}

	public void Init(int itemId)
	{
		InitWrapper(() =>
		{
			mItemId = itemId;
		});
	}

	public override bool ShowRedDot()
	{
		IProfessionItemData itemById = Services.GetItemById(mItemId);
		if (itemById == null)
		{
			return false;
		}

		return Services.ShowTip(itemById, mRedDots, Services.AutoMergeWhenLevelUp, Services.AwakeAutoMergeEnable);
	}

	protected void MainPlayer_OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		if (Services.GetItemById(mItemId) != null && Services.IsPromotionItem(itemData))
		{
			SetDirty();
		}
	}

	private void MainPlayer_OnTreasureRefresh(IProfessionItemData itemData, bool isNew)
	{
		SetDirty();
	}

	private void OnSettingChanged()
	{
		SetDirty();
	}
}

}
