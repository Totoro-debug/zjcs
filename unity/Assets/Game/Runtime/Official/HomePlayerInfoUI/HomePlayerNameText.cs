// Selected original player-info implementation; only service boundaries and C# decompilation repairs differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerInfoUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
public class HomePlayerNameText : HomePlayerInfoUIBase
{
	public MultiLanguageText myText;

	protected override void OnEnableAndInited()
	{
		InfoBindings.MainPlayer.OnNameUpdate += MainPlayer_OnNameUpdate;
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (InfoBindings.MainPlayer != null)
		{
			InfoBindings.MainPlayer.OnNameUpdate -= MainPlayer_OnNameUpdate;
		}
	}

	private void MainPlayer_OnNameUpdate()
	{
		Refresh();
	}

	private void Refresh()
	{
		myText.SetKey("{0}", InfoBindings.MainPlayer.Name);
	}
}

}
