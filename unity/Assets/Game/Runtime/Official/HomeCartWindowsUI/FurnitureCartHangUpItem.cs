// Selected 903_156948 client controllers; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.Map13MiniMap;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using HangUpInfo = Zjcs.Official.HomeCartWindowsUI.HomeCartHangUpInfo;
using Player = Zjcs.Official.HomeCartWindowsUI.IHomeCartWindowPlayer;
using ItemAwardIcon = Zjcs.Official.HomeAwardUI.HomeAwardIcon;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using ItemType = Zjcs.Official.HomeAwardUI.ItemType;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
namespace Zjcs.Official.HomeCartWindowsUI
{
public class FurnitureCartHangUpItem : UIBase
{
    public IHomeCartWindowServices Services { protected get; set; }
	public enum HangUpItemStatus
	{
		Unlock,
		Lock,
		CanUnlock
	}

	public Button LevelUpBtn;

	public Button UnlockBtn;

	public Button ClickBtn;

	public MultiLanguageText NameTxt;

	public MultiLanguageText LevelTxt;

	public Image Icon;

	public Image Bg;

	public HomeNotice HomeNotice;

	public TipDotNodeLinker TipDotNodeLinker;

	[Space]
	public WorldStateSwitcher<HangUpItemStatus> UnlockStateSwitcher;

	public WorldStateSwitcher<bool> LevelFunctionStateSwitcher;

	public Action OnRefreshFinish;

	public Action<int> OnHangUpItemClick;

	private int mHangUpId;

	private HangUpItemStatus mHangUpItemStatus;

	private HomeCartWindowResourceBag mDisposable = new HomeCartWindowResourceBag();

	private HangUpInfo mHangUpInfo;

	public HangUpInfo HangUpInfo => mHangUpInfo;

	public void Init(int hangUpId)
	{
		InitWrapper(() =>
		{
			mHangUpId = hangUpId;
		});
	}

	private void Awake()
	{
		mDisposable ??= new HomeCartWindowResourceBag();
		LevelUpBtn.onClick.AddListener(OnClickLevelUp);
		UnlockBtn.onClick.AddListener(OnClickUnlock);
		ClickBtn.onClick.AddListener(OnClick);
	}

	protected override void OnDestroy()
	{
		mDisposable?.Dispose();
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (mHangUpId != 0)
		{
			mDisposable ??= new HomeCartWindowResourceBag();
			mHangUpInfo = Services.GetHangUpInfo(mHangUpId);
			Icon.sprite = mDisposable.Add(Services.Resources.LoadAsset<Sprite>(Services.GetHangUpRewardItem(mHangUpId).Icon)).Result;
			Bg.sprite = mDisposable.Add(Services.Resources.LoadAsset<Sprite>(Services.GetCartItemBgPath(mHangUpId))).Result;
			TipDotNodeLinker.SetNodeInstanceInfo(new HangUpNodeInstanceInfo(mHangUpId));
			Refresh();
			if (mHangUpItemStatus == HangUpItemStatus.Lock)
			{
				HomeNotice.Init(mHangUpInfo.NoticeCondition, mHangUpInfo.UnlockCondition, OnMetNoticeCondition, null);
				mDisposable.Add(Services.SubscribeCondition(mHangUpInfo.UnlockCondition, OnUnlockCondition));
			}
			Services.Player.OnHangUpLevelUp += OnHangUpLevelUp;
			Services.Player.OnHangUpUnlock += OnHangUpUnlock;
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		mDisposable?.Dispose();
		if (Services.Player != null)
		{
			Services.Player.OnHangUpLevelUp -= OnHangUpLevelUp;
			Services.Player.OnHangUpUnlock -= OnHangUpUnlock;
		}
	}

	private void OnClick()
	{
		OnHangUpItemClick?.Invoke(mHangUpId);
	}

	private void OnUnlockCondition()
	{
		Refresh();
	}

	private void OnHangUpUnlock(int hangUpId)
	{
		if (hangUpId == mHangUpId)
		{
			TipDotNodeLinker.SetNodeInstanceInfo(new HangUpNodeInstanceInfo(hangUpId));
			Refresh();
		}
	}

	private void OnHangUpLevelUp(int hangUpId, int level)
	{
		if (hangUpId == mHangUpId)
		{
			Refresh();
		}
	}

	private void OnClickUnlock()
	{
		if (Services.CheckFunctionDisabled(IHomeCartWindowServices.ServerFunctionHangUpUnlock))
		{
			Services.BroadcastErrCode(IHomeCartWindowServices.ErrorHangUpUnlockDisabled);
		}
		if (!Services.Player.TryGetUnlockSingleHangUpData(mHangUpId, out var _) && Services.IsConditionMet(Services.GetHangUpInfo(mHangUpId).UnlockCondition))
		{
			Services.UnlockHangUp(mHangUpId, Refresh);
		}
	}

	private void OnClickLevelUp()
	{
		if (Services.CheckFunctionDisabled(IHomeCartWindowServices.ServerFunctionHangUpLevelUp))
		{
			Services.BroadcastErrCode(IHomeCartWindowServices.ErrorHangUpLevelUpDisabled);
			return;
		}
		Services.OpenHangUpLevelUp(mHangUpId, EFurniture.Cart);
	}

	private void OnMetNoticeCondition()
	{
		Refresh();
	}

	private void Refresh()
	{
		int hangUpId = mHangUpId;
		Player mainPlayer = Services.Player;
		HangUpItemStatus hangUpItemStatus = HangUpItemStatus.Unlock;
		if (!Services.Player.TryGetUnlockSingleHangUpData(hangUpId, out var singleHangUpData))
		{
			hangUpItemStatus = ((!Services.IsConditionMet(mHangUpInfo.UnlockCondition)) ? HangUpItemStatus.Lock : HangUpItemStatus.CanUnlock);
		}
		bool canLevelUp = mHangUpInfo.CanLevelUp;
		mHangUpItemStatus = hangUpItemStatus;
		UnlockStateSwitcher.SetState(hangUpItemStatus);
		LevelFunctionStateSwitcher.SetState(canLevelUp);
		if (hangUpItemStatus == HangUpItemStatus.Unlock)
		{
			double hangUpReward = Services.GetHangUpReward(hangUpId, 1.0, singleHangUpData.Level);
			NameTxt.SetKey("hang_up_output_hour", FurnitureOutputInfo.GetPropValue(hangUpReward, n => CultureNumberArg.Compact(n).ToString()));
			LevelTxt.SetKey("ui_common_level_style_11", Services.Player.GetHangUpLevel(hangUpId));
		}
		else
		{
			NameTxt.SetKey(Services.GetHangUpName(hangUpId));
		}
		OnRefreshFinish?.Invoke();
	}
}
}
