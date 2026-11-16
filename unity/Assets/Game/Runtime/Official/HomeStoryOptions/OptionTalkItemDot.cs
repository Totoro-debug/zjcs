// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public class OptionTalkItemDot : MonoBehaviour
{
	[SerializeField]
	private WorldStateSwitcher<EOptionTalkItemDotType> mStateSwitcher;

	[SerializeField]
	private WorldCountRedDotImage mBlueCountImage;

	public void SetContext(IOptionTalkDotContext context)
	{
		if (mStateSwitcher != null)
		{
			if (context == null)
			{
				mStateSwitcher.SetState(EOptionTalkItemDotType.None);
				return;
			}
			mStateSwitcher.SetState(context.DotType);
			RefreshDotContent(context);
		}
	}

	private void RefreshDotContent(IOptionTalkDotContext context)
	{
		EOptionTalkItemDotType dotType = context.DotType;
		if ((uint)dotType > 2u && dotType == EOptionTalkItemDotType.BlueCount && (Object)null != (Object)(object)mBlueCountImage)
		{
			mBlueCountImage.SetCount(context.GetCount());
		}
	}
}

}
