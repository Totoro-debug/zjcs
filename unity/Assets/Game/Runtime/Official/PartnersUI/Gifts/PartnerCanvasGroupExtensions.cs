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


public static class PartnerCanvasGroupExtensions
{
	public static void SetAlphaAndInteractable(this CanvasGroup canvasGroup, float alpha)
	{
		canvasGroup.alpha = alpha;
		canvasGroup.interactable = alpha > 0f;
		canvasGroup.blocksRaycasts = alpha > 0f;
	}
}

}
