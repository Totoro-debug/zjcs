// Selected original client; local ownership and native animation endpoints.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;
using AppearanceUI = Zjcs.Official.CombatAppearance.AppearanceUI;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpProgressUI
{

public class FastHangUpProgressBarDialog : UIBase
{
    public IHomeFastHangUpProgressServices Services { protected get; set; }
	[Serializable]
	public class FastHangUpAnimConfig
	{
		public EFurniture Furniture;

		public string Anim;

		public Vector3 Pos;

		public PlayerAppearanceOverride AppearanceOverride;
	}

	public List<FastHangUpAnimConfig> FastHangUpAnimList;

	public List<Image> RewardImageList;

	public PlayerAppearance PlayerAppearance;

	public AppearanceUI AppearanceUI;

	public string DefaultHangUpAnim;

	public WorldStateSwitcher<EFurniture> ShowStateSwitcher;

	private int mFastHangUpId;

	private EFurniture mFurniture;

	private Action mOnFinished;

	private HomeWindowResourceBag mDisposableBag;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new HomeWindowResourceBag();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		mOnFinished?.Invoke();
		mDisposableBag?.Dispose();
		Services.SetActiveUIVisible(this, visible: true);
		base.OnDisableAndInited();
	}

	public void Init(int fastHangUpId, Action onFinished)
	{
		InitWrapper(() =>
		{
			mFastHangUpId = fastHangUpId;
			mFurniture = Services.FastHangUp.GetFastHangUpInfo(fastHangUpId).BindFurniture;
			mOnFinished = onFinished;
		});
	}

	private void Refresh()
	{
		List<ItemInfo> items = new List<ItemInfo>();
		Services.GetFastHangUpRewardItems(mFastHangUpId, ref items);
		int count = items.Count;
		foreach (Image rewardImage in RewardImageList)
		{
			rewardImage.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>("Assets/Image/" + items[Services.RandomRange(0, count)].Icon)).Result;
		}
		UpdatePlayerAnim();
		if (mFurniture == EFurniture.Bed)
		{
			Services.PublishBedFastHangUp();
		}
		Services.SetActiveUIVisible(this, visible: false);
	}

	private void UpdatePlayerAnim()
	{
		FastHangUpAnimConfig fastHangUpAnimConfig = FastHangUpAnimList.Find((FastHangUpAnimConfig c) => c.Furniture == mFurniture);
		ShowStateSwitcher.SetState(mFurniture);
		string animationName = DefaultHangUpAnim;
		if (fastHangUpAnimConfig != null)
		{
			AppearanceUI.GetComponent<RectTransform>().localPosition = fastHangUpAnimConfig.Pos;
			animationName = fastHangUpAnimConfig.Anim;
			PlayerAppearance.SetRuntimeDataOverride(fastHangUpAnimConfig.AppearanceOverride.AppearanceOverride);
		}
		else
		{
			AppearanceUI.GetComponent<RectTransform>().localPosition = Vector3.zero;
			PlayerAppearance.SetRuntimeDataOverride(null);
		}
		AppearanceUI.PlayAnimation(animationName, loop: true);
	}

	public void PlayEndAnim()
	{
		if (mFurniture != EFurniture.Bed)
		{
			AppearanceUI.PlayAnimation("map/pickup_01", loop: false);
		}
		Services.SetActiveUIVisible(this, visible: true);
	}

	public void CloseWindow()
	{
		Services.Close(gameObject);
	}
}

}
