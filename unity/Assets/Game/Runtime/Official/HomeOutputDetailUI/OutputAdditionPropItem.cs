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
public class OutputAdditionPropItem : UIBase
{
    public IOutputAdditionDetailServices Services { get; set; }
	[SerializeField]
	private MultiLanguageText mPropTxt;

	private OutputAdditionInfo mInfo;

	public void Init(OutputAdditionInfo info)
	{
		InitWrapper(() =>
		{
			mInfo = info;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
	}

	private void Refresh()
	{
		mPropTxt.SetKey("{0}<color=#e37d38>+{1}%</color>", new CultureTextArg(Services.Text(OutputDetailLanguage.EnumKey(mInfo.OutputAdditionPropType)), Array.Empty<object>(), false), ((float)mInfo.OutputAdditionPropVal / 10000f * 100f));
	}
}
}
