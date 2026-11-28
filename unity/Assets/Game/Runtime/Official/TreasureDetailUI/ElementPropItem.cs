using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.HomePlayerInfoUI;
using PlayerCostMoneyContent=Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent;
using IntroductionPopBtn=Zjcs.Official.HomePlayerInfoUI.HomeIntroductionPopBtn;
using TreasureState=Zjcs.Official.TreasureCultivation.TreasureState;
using PropType=System.Int32;
using SubRank=System.Int32;
using Rank=System.Int32;
using Object=UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

public class ElementPropItem : ProfessionPlayerPropItem
{
	[Header("元素 Icon")]
	[SerializeField]
	private Image mElementIcon;

	private Color? mTextInitColor;

	private Color? mTextColor;

	public override void Init(ProfessionPropertyType propType, long value, long nextValue = -1L, bool instant = true, object arg = null, bool isBless = false, string fomatKey = "{0}")
	{
		base.Init(propType, value, nextValue, instant, arg, isBless, fomatKey);
		ProfessionPropertyDefinition propCfgInfo = Bindings.GetProperty(propType);
		mElementIcon.sprite = mDisposableBag.Add(Bindings.Resources.LoadAsset<Sprite>("Assets/Image/"+propCfgInfo.SmallIcon)).Result;
	}

	protected override void ApplyText()
	{
		base.ApplyText();
		ApplyTextColor();
	}

	private void ResetTextColor()
	{
		if (!mTextInitColor.HasValue)
		{
			mTextInitColor = myPropTypeText.Color;
			return;
		}
		myPropTypeText.Color = mTextInitColor.Value;
		if ((Object)(object)myPropValueText != (Object)null)
		{
			myPropValueText.Color = mTextInitColor.Value;
		}
		if ((Object)(object)PropValueFromText != (Object)null)
		{
			PropValueFromText.Color = mTextInitColor.Value;
		}
		if ((Object)(object)PropValueToText != (Object)null)
		{
			PropValueToText.Color = mTextInitColor.Value;
		}
	}

	public ProfessionPlayerPropItem SetTextColor(Color color)
	{
		mTextColor = color;
		return this;
	}

	private void ApplyTextColor()
	{
		if (mTextColor.HasValue)
		{
			Color value = mTextColor.Value;
			myPropTypeText.Color = value;
			if ((Object)(object)myPropValueText != (Object)null)
			{
				myPropValueText.Color = value;
			}
			if ((Object)(object)PropValueFromText != (Object)null)
			{
				PropValueFromText.Color = value;
			}
			if ((Object)(object)PropValueToText != (Object)null)
			{
				PropValueToText.Color = value;
			}
		}
	}
}

}
