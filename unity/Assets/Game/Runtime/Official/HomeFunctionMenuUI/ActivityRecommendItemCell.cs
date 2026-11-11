// Original menu recommendation implementation; explicit data/resource bindings.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System.Collections.Generic;
using UnityEngine;

public class ActivityRecommendItemCell : FancyCell<ActivtyEntryKeyData, ActivityRecommendContext>
{
	[SerializeField]
	private Transform mSlot;

	private RectTransform mRectTrans;

	private ActivityEntryBaseItem mCurItem;

	private HomePanelDisposableBag mHomePanelDisposableBag;

	public ActivityEntryBaseItem CurItem => mCurItem;
	internal IHomeMenuActivityServices ActivityServices => Context.Services;



	private void Awake()
	{
		mHomePanelDisposableBag = default;
	}

	private void OnDestroy()
	{
		mCurItem = null;
		mHomePanelDisposableBag.Dispose();
	}

	public override void SetVisible(bool visible)
	{
		base.SetVisible(visible);
	}

	public override void UpdateContent(ActivtyEntryKeyData itemData)
	{
		(ActivityEntryType, int) key = (itemData.EntryType, itemData.ActivityId);
		if (!Context.EntryItemPoolDict.TryGetValue(key, out var value))
		{
			value = new Queue<ActivityEntryBaseItem>();
			Context.EntryItemPoolDict.Add(key, value);
		}
		ActivityEntryBaseItem activityEntryBaseItem = default;
		if (!value.TryDequeue(out activityEntryBaseItem) && Context.Services.GetEntries(itemData.EntryType).TryGetValue(itemData.ActivityId, out var value2))
		{
			GameObject result = Context.Services.LoadPrefab(value2.EntryPrefabPath).AddTo(ref mHomePanelDisposableBag).Result;
			if (result != null)
			{
				activityEntryBaseItem = HomeMenuUIUtils.Instantiate<ActivityEntryBaseItem>(Context.Services, result, Context.PoolItemSlot);
			}
		}
		if (!(activityEntryBaseItem != null))
		{
			return;
		}
		foreach (IHomeMenuActivityEntryData activityEntryData in Context.Services.ActivityEntryDatas)
		{
			if (activityEntryData.Type == itemData.EntryType && activityEntryData.Id == itemData.ActivityId)
			{
				activityEntryBaseItem.Init(activityEntryData, showTipDot: false);
				break;
			}
		}
		TryRecoverLastItem();
		mCurItem = activityEntryBaseItem;
		activityEntryBaseItem.transform.SetParent(mSlot);
		activityEntryBaseItem.transform.localPosition = Vector3.zero;
		activityEntryBaseItem.transform.localScale = Vector3.one;
		activityEntryBaseItem.gameObject.SetActive(value: true);
	}

	private void TryRecoverLastItem()
	{
		if (!(mCurItem == null))
		{
			(ActivityEntryType, int) key = (mCurItem.ActivityEntryData.Type, mCurItem.ActivityEntryData.Id);
			if (Context.EntryItemPoolDict.TryGetValue(key, out var value))
			{
				mCurItem.transform.SetParent(Context.PoolItemSlot);
				mCurItem.transform.localPosition = Vector3.zero;
				mCurItem.transform.localScale = Vector3.one;
				value.Enqueue(mCurItem);
			}
		}
	}

	public override void UpdatePosition(float position)
	{
		if (mRectTrans == null)
		{
			mRectTrans = transform.parent.GetComponent<RectTransform>();
		}
		transform.localPosition = new Vector3(Mathf.Lerp(0f - mRectTrans.rect.width, mRectTrans.rect.width, position), 0f, 0f);
	}
}
}
