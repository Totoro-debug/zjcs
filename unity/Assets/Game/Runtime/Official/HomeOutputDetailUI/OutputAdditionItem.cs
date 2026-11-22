// Original selected output-addition UI; local data and input boundaries only.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using OutputGroupType = Zjcs.Official.HomeOutputAddition.OutputGroupType;
namespace Zjcs.Official.HomeOutputDetailUI
{
public class OutputAdditionItem : UIBase
{
    public IOutputAdditionDetailServices Services { get; set; }
	[SerializeField]
	private MultiLanguageText mNameTxt;

	[SerializeField]
	private ShowRemainTimeInfo mRemainTxt;

	[SerializeField]
	private MultiLanguageText mDescTxt;

	[SerializeField]
	private WorldStateSwitcher<bool> mIsDescSwitcher;

	[SerializeField]
	private Transform mTransform;

	[SerializeField]
	private OutputAdditionPropItem mPropItemPrefab;

	private HomeWindowResourceBag mDisposableBag;

	private List<OutputAdditionInfo> mInfos;

	private DateTime mEndDate;

	private OutputActionType mOutputActionType;

	private OutputGroupType mOutputGroupType;

	public void Init(List<OutputAdditionInfo> infos, DateTime endDate)
	{
		InitWrapper(() =>
		{
			mInfos = infos;
			mEndDate = endDate;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new HomeWindowResourceBag();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		mDisposableBag?.Dispose();
		base.OnDisableAndInited();
	}

	private void Refresh()
	{
		mRemainTxt.Init(mEndDate, 1f, "{0}", 1);
		mOutputActionType = mInfos.First().OutputActionType;
		mOutputGroupType = mInfos.First().OutputGroupType;
		mNameTxt.SetKey(OutputDetailLanguage.EnumKey(mOutputActionType) + ".Name");
		HashSet<string> hashSet = mInfos.Where((OutputAdditionInfo info) => !string.IsNullOrEmpty(info.ShowKey)).ToList().ConvertAll((OutputAdditionInfo info) => info.ShowKey)
			.ToHashSet();
		mIsDescSwitcher.SetState(hashSet.Count > 0);
		if (hashSet.Count > 0)
		{
			List<string> list = hashSet.ToList().ConvertAll((string key) => Services.Text(OutputDetailLanguage.EnumKey(mOutputGroupType) + "_" + key));
			mDescTxt.SetKey("{0}", string.Join<string>('\n', (IEnumerable<string>)list));
		}
		foreach (Transform child in mTransform) Destroy(child.gameObject);
		foreach (OutputAdditionInfo mInfo in mInfos)
		{
			Services.Instantiate(mPropItemPrefab.gameObject, mTransform).GetComponent<OutputAdditionPropItem>().Init(mInfo);
		}
	}
}
}
