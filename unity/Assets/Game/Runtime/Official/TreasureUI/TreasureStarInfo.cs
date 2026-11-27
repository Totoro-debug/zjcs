// Original client star display; see treasure-ui-controls source receipt.
namespace Zjcs.Official.TreasureUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using UnityEngine;

public class TreasureStarInfo : MonoBehaviour
{
	public StarMiniInfo StarMiniInfo;

	public WorldStateSwitcher<bool> StarStateSwitcher;

	public void Show(Quality quality, int star, int maxStar, bool isSuper = false, bool showBlankStar = false, bool blink = false)
	{
		((Component)this).gameObject.SetActive(true);
		StarStateSwitcher.SetState(isSuper);
		if (!isSuper)
		{
			StarMiniInfo.Show(star, maxStar, quality, showBlankStar);
		}
		StarMiniInfo.StopAllBlink();
		if (blink)
		{
			StarMiniInfo.TryBlink(star);
		}
	}

	public void StopStarBlink()
	{
		StarMiniInfo.StopAllBlink();
	}

	public void Hide()
	{
		StarMiniInfo.Hide();
		((Component)this).gameObject.SetActive(false);
	}
}

}
