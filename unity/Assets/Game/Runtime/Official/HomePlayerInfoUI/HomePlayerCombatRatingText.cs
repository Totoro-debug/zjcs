// Selected original player-info implementation; only service boundaries and C# decompilation repairs differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerInfoUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public class HomePlayerCombatRatingText : HomePlayerInfoUIBase
{
	public MultiLanguageText Text;

	[SerializeField]
	private int mNumberSize;

	protected override void OnEnableAndInited()
	{
		InfoBindings.MainPlayer.OnCombatRatingChanged += MainPlayer_OnCombatRatingChanged;
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (InfoBindings.MainPlayer != null)
		{
			InfoBindings.MainPlayer.OnCombatRatingChanged -= MainPlayer_OnCombatRatingChanged;
		}
	}

	private void MainPlayer_OnCombatRatingChanged(long arg1, long arg2)
	{
		Refresh();
	}

	private void Refresh()
	{
		if (mNumberSize > 0)
		{
			HomeNumberWithSizeArg numberWithSizeArg = new HomeNumberWithSizeArg(InfoBindings.MainPlayer.CombatRating, mNumberSize);
			Text.Format(numberWithSizeArg);
		}
		else
		{
			CultureNumberArg cultureNumberArg = CultureNumberArg.Compact(InfoBindings.MainPlayer.CombatRating);
			Text.Format(cultureNumberArg);
		}
	}
}

}
