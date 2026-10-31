// Ported from active official client; see partner-roster-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerRosterLanguage;
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.SkillsUI;
using UnityEngine;
using UnityEngine.UI;

public static class LoopScrollRectUtil
{
	public static float GetCurPosition(this LoopScrollRectBase scrollRect)
	{
		if (scrollRect.horizontal)
		{
			return scrollRect.horizontalNormalizedPosition;
		}
		return scrollRect.verticalNormalizedPosition;
	}

	public static void RefillCellsByPosition(this LoopScrollRectBase scrollRect, float position)
	{
		scrollRect.RefillCells(0, 0f, () =>
		{
			if (scrollRect.horizontal)
			{
				scrollRect.horizontalNormalizedPosition = System.Math.Max(0f, System.Math.Min(1f, position));
			}
			else
			{
				scrollRect.verticalNormalizedPosition = System.Math.Max(0f, System.Math.Min(1f, position));
			}
		});
	}

	public static GameObject GetItemByIndex(this LoopScrollRectBase scrollRect, int index)
	{
		if ((Object)(object)scrollRect.content == (Object)null)
		{
			return null;
		}
		if (index < scrollRect.ItemTypeStart || index > scrollRect.ItemTypeEnd)
		{
			return null;
		}
		int num = index - scrollRect.ItemTypeStart;
		if (num < 0 || num >= ((Transform)scrollRect.content).childCount)
		{
			return null;
		}
		Transform child = ((Transform)scrollRect.content).GetChild(num);
		if ((Object)(object)child == (Object)null)
		{
			return null;
		}
		return ((Component)child).gameObject;
	}
}

}
