namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionBlessPropBtn : UIBase
{
    private IProfessionPropertyBindings mBindings;
    private IProfessionPropertyBindings Bindings => mBindings ?? ProfessionPropertyBindings.Required;
    public void Configure(IProfessionPropertyBindings bindings)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	[SerializeField]
	private Button mOpenBtn;

	private int mItemRank;

	private string mRawValueDesc;

	private string mBlessValueDesc;

	private bool mHasBlessLevel;

	private bool mEffect;

	private string mDesc;

	private string mUnEffectKey;

	private string mUnSeasonMatchKey;

	private const string kDefaultUnEffectKey = "ui_bless_prop_not_effect";

	private const string kDefaultUnSeasonMatchKey = "ui_daily_equip_suit_extra_rank_desc";

	private bool mCanClick = true;

	public void Init(int itemRank, int serverRank, PropType type, long rawValue, long blessValue, bool hasBless)
	{
		InitWrapper(() =>
		{
			string text = Bindings.Text(ProfessionPropertyConfig.GetPropTypeName(type));
			mItemRank = itemRank;
			mEffect = itemRank == serverRank;
			mHasBlessLevel = hasBless;
			mRawValueDesc = string.Format(GetPropFomatedText(), text, ProfessionPropertyLanguage.FormatPropString(type, rawValue, includeUnit: false, includePropDesc: false, -1L));
			mBlessValueDesc = $"{ProfessionPropertyLanguage.FormatPropString(type, blessValue, includeUnit: false, includePropDesc: false, -1L)}";
			mUnEffectKey = "ui_bless_prop_not_effect";
			mUnSeasonMatchKey = "ui_daily_equip_suit_extra_rank_desc";
			mDesc = ProfessionBlessDescription.GetBlessDesc(mRawValueDesc, mBlessValueDesc, hasBless, mEffect);
		});
	}

	public void Init(int serverRank, string propName, ProfessionPropertyConfig.PropItemValue value, ProfessionPropertyConfig.PropItemValue subValue, bool hasBless)
	{
		InitWrapper(() =>
		{
			string text = Bindings.Text(propName);
			mItemRank = serverRank;
			mEffect = true;
			mHasBlessLevel = !subValue.IsEmpty();
			mRawValueDesc = string.Format(GetPropFomatedText(), text, value.ToString());
			mUnEffectKey = "ui_bless_prop_not_effect";
			mUnSeasonMatchKey = "ui_daily_equip_suit_extra_rank_desc";
			if (subValue != null && !subValue.IsEmpty())
			{
				mBlessValueDesc = $"{subValue.ToSubString()}";
			}
			mDesc = ProfessionBlessDescription.GetBlessDesc(mRawValueDesc, mBlessValueDesc, mHasBlessLevel);
		});
	}

	public void InitPowerDesc(int equipRank, int serverRank, long rawPower, long blessPower)
	{
		InitWrapper(() =>
		{
			mHasBlessLevel = blessPower > 0;
			mItemRank = equipRank;
			mEffect = equipRank == serverRank;
			mUnEffectKey = "ui_bless_prop_not_effect";
			mRawValueDesc = Bindings.TextFormatted("ui_bless_raw_power_desc", Bindings.DecimalNumber(rawPower, 2));
			mBlessValueDesc = Bindings.TextFormatted("ui_bless_bless_power_desc", Bindings.DecimalNumber(blessPower, 2));
			mDesc = ProfessionBlessDescription.GetBlessDesc(mRawValueDesc, mBlessValueDesc, mHasBlessLevel, mEffect);
			mUnSeasonMatchKey = "ui_bless_bless_power_un_effect_desc";
		});
	}

	public void Init(int itemRank, string desc, bool isEffect, string unEffectKey = "ui_bless_prop_not_effect")
	{
		InitWrapper(() =>
		{
			mItemRank = itemRank;
			mEffect = isEffect;
			mUnEffectKey = unEffectKey;
			mUnSeasonMatchKey = "ui_daily_equip_suit_extra_rank_desc";
			mHasBlessLevel = true;
			mDesc = desc;
		});
	}

	public void SetCanClick(bool click)
	{
		mCanClick = click;
	}

	protected override void OnEnableAndInited()
	{
		mOpenBtn.onClick.AddListener(OnClick);
	}

	protected override void OnDisableAndInited()
	{
		mOpenBtn.onClick.RemoveListener(OnClick);
	}

	private void OnClick()
	{
		if (mHasBlessLevel && mCanClick)
		{
			if (string.IsNullOrEmpty(mUnSeasonMatchKey))
			{
				mUnSeasonMatchKey = "ui_daily_equip_suit_extra_rank_desc";
			}
			Bindings.PopupBless(this, mItemRank, mDesc, mEffect, mUnEffectKey, mUnSeasonMatchKey);
		}
	}

	private string GetPropFomatedText()
	{
		return Bindings.Text("ui_equip_attribuate_prop_ PropArg");
	}
}

}
