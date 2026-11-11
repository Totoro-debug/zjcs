// Original menu recommendation implementation; explicit data/resource bindings.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class FunctionEntryDialog : UIBase
{
	public IHomeMenuActivityServices Services { private get; set; }

	[SerializeField]
	private ActivityRecommendItemView mItemView;

	[SerializeField]
	private GameObject mActivityRecommondView;


	public void Init()
	{
		InitWrapper(null);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.OnActivityOpen += new Action<object>(OnActivityChange);
		Services.OnActivityClose += new Action<object>(OnActivityChange);
		UpdateView();
	}

	protected override void OnDisableAndInited()
	{
		if (Services.ActivityDataManagerExists)
		{
			Services.OnActivityOpen -= new Action<object>(OnActivityChange);
			Services.OnActivityClose -= new Action<object>(OnActivityChange);
		}
		base.OnDisableAndInited();
	}

	private void OnActivityChange(object data)
	{
		UpdateView();
	}

	private void UpdateView()
	{
		List<ActivtyEntryKeyData> openActivityRecommendEntries = GetOpenActivityRecommendEntries();
		bool flag = openActivityRecommendEntries.Count > 0;
		mActivityRecommondView.SetActive(flag);
		if (flag)
		{
			openActivityRecommendEntries.Sort((ActivtyEntryKeyData a, ActivtyEntryKeyData b) => a.Order - b.Order);
			int startIndex = Services.RandomRange(0, openActivityRecommendEntries.Count);
			mItemView.UpdateData(openActivityRecommendEntries, startIndex);
		}
	}

	private List<ActivtyEntryKeyData> GetOpenActivityRecommendEntries()
	{
		List<ActivtyEntryKeyData> list = new List<ActivtyEntryKeyData>();
		foreach (IHomeMenuActivityEntryData activityEntryData in Services.ActivityEntryDatas)
		{
			HomeMenuActivityRecommendInfo activityRecommendInfo = Services.GetRecommend(activityEntryData.Type, activityEntryData.Id);
			if (activityRecommendInfo != null && (!Services.IsMapScene || activityRecommendInfo.ShowInMap))
			{
				ActivtyEntryKeyData item = new ActivtyEntryKeyData
				{
					EntryType = activityRecommendInfo.ActivityEntryType,
					ActivityId = activityRecommendInfo.Id,
					Order = activityRecommendInfo.Order
				};
				list.Add(item);
			}
		}
		return list;
	}
}
}
