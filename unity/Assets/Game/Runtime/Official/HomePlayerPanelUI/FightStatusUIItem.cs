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
using UnityEngine.UI;

public class FightStatusUIItem : FightStatusItemBase, IGameObjectPoolItem
{
	public Image BgImg;

	public Image IconImg;

	public MultiLanguageText CountText;

	public void OnGameObjectPoolClear()
	{
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void Show(Sprite iconSprite)
	{
		IconImg.sprite = iconSprite;
	}

	public override void Show(Sprite iconSprite, Sprite bgSprite, int count)
	{
		IconImg.sprite = iconSprite;
		BgImg.sprite = bgSprite;
		UpdateCount(count);
	}

	public override void Show(string icon, string iconBg, int count)
	{
		if (!string.IsNullOrEmpty(icon))
		{
			IconImg.sprite = HomePanelDisposable.AddTo<IHomeHudAssetHandle<Sprite>>(ResourceLoader.LoadSprite(icon), ref mDisposableBag).Result;
		}
		else
		{
			IconImg.sprite = null;
		}
		if (!string.IsNullOrEmpty(iconBg))
		{
			BgImg.sprite = HomePanelDisposable.AddTo<IHomeHudAssetHandle<Sprite>>(ResourceLoader.LoadSprite(iconBg), ref mDisposableBag).Result;
		}
		else
		{
			IconImg.sprite = null;
		}
		UpdateCount(count);
	}

	public override void UpdateCount(int count)
	{
		string countText = FightStatusItemBase.GetCountText(count);
		bool flag = !string.IsNullOrEmpty(countText);
		((Component)CountText).gameObject.SetActive(flag);
		if (flag)
		{
			CountText.Format(countText);
		}
	}
}

}
