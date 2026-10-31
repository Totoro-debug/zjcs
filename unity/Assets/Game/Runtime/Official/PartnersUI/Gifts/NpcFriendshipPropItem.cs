// Migrated selected official client; see partner-gift-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using LanguageUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyLanguage;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipNpcInfo;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using UnityEngine;

public class NpcFriendshipPropItem : MonoBehaviour
{
    private IPartnerFriendshipUiServices services;
    public void Configure(IPartnerFriendshipUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        mPropItem.Configure(value.Properties);
    }

	[SerializeField]
	private PlayerPropItem mPropItem;

	[SerializeField]
	private MultiLanguageText mTravelPropText;

	[SerializeField]
	private MultiLanguageText mTxtPropValueAdd;

	[SerializeField]
	private WorldStateSwitcher<bool> mTravelStateSwitcher;

	public void Init(PropType propType, long value, bool isShowBg, long addVal)
	{
		bool state = false;
		if (services.TravelNpcPropTypeList.Contains(propType))
		{
			state = true;
			CultureTextArg cultureTextArg = new CultureTextArg(PropConfigUtils.GetPropTypeName(propType));
			CultureTextArg cultureTextArg2 = new CultureTextArg("ui_common_prop_plus", new LanguageUtils.PropValue(propType, value, includeUnit: false, includePropDesc: false, -1L));
			mTravelPropText.SetKey("{0}{1}", cultureTextArg, cultureTextArg2);
		}
		else
		{
			mPropItem.Init(propType, value, -1L);
			((Component)mTxtPropValueAdd).gameObject.SetActive(addVal > 0);
			if (addVal > 0)
			{
				mTxtPropValueAdd.SetKey("+{0}", LanguageUtils.FormatPropString(propType, addVal, includeUnit: false, includePropDesc: false, -1L));
			}
		}
		mPropItem.SetBgVisible(isShowBg);
		mTravelStateSwitcher.SetState(state);
	}
}

}
