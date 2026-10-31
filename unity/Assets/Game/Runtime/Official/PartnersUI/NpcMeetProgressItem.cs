// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using UnityEngine;

public class NpcMeetProgressItem : MonoBehaviour
{
	public Zjcs.Official.WorldUI.WorldStateSwitcher<bool> ShowShadowStates;

	public Zjcs.Official.WorldUI.WorldStateSwitcher<bool> CanMeetStates;

	public MultiLanguageText WaitMeetProgressText;

	public MultiLanguageText CanMeetProgressText;

	public void SetShowShadowBgStyle(bool isShow)
	{
		ShowShadowStates.SetState(isShow);
	}

	public void SetProgress(long curValue, long targetValue)
	{
		CanMeetStates.SetState(curValue >= targetValue);
		if (curValue >= targetValue)
		{
			CanMeetProgressText.SetKey("{0}/{1}", curValue, targetValue);
		}
		else
		{
			WaitMeetProgressText.SetKey("{0}/{1}", curValue, targetValue);
		}
	}
}

}
