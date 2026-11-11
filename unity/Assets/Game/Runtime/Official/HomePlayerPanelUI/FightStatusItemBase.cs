// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public abstract class FightStatusItemBase : MonoBehaviour
{
	protected HomePanelDisposableBag mDisposableBag;

	protected IHomePanelResourceLoader ResourceLoader => HomePlayerPanelScope.Required(this).Resources;

	private void OnEnable()
	{

		mDisposableBag = default;
	}

	private void OnDisable()
	{
		mDisposableBag.Dispose();
	}

	public abstract void Show(Sprite iconSprite, Sprite bgSprite, int count);

	public abstract void Show(string icon, string iconBg, int count);

	public abstract void UpdateCount(int count);

	protected static string GetCountText(int count)
	{
		string result = string.Empty;
		if (count > 0)
		{
			result = count.ToString();
		}
		return result;
	}
}

}
