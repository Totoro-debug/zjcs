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
public class HangUpLevelUpSwitchItem : MonoBehaviour
{
    public IHomeCartWindowServices Services { protected get; set; }
	public Button SwitchBtn;

	public Image Icon;

	public GameObject SelectGo;

	public MultiLanguageText LevelTxt;

	public TipDotNodeLinker TipDotNodeLinker;

	private HomeCartWindowResourceBag mDisposableBag = new HomeCartWindowResourceBag();

	private int mHangUpId;

	private bool mIsSelect;

	private Action<int, HangUpLevelUpSwitchItem> mOnSwitch;

	private void Awake()
	{
		mDisposableBag ??= new HomeCartWindowResourceBag();
		SwitchBtn.onClick.AddListener(OnClickSwitchBtn);
	}

	private void OnDestroy()
	{
		mDisposableBag?.Dispose();
	}

	private void OnEnable()
	{
		Services.Player.OnHangUpLevelUp += OnHangUpLevelUp;
	}

	private void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnHangUpLevelUp -= OnHangUpLevelUp;
		}
	}

	public void Init(int hangUpId, Action<int, HangUpLevelUpSwitchItem> onSwitch)
	{
		mHangUpId = hangUpId;
		mOnSwitch = onSwitch;
		TipDotNodeLinker.SetNodeInstanceInfo(new HangUpNodeInstanceInfo(hangUpId));
		RefreshIcon();
		RefreshLevel();
	}

	private void RefreshIcon()
	{
		if (mHangUpId != 0)
		{
			HomeWindowItemInfo hangUpRewardItem = Services.GetHangUpRewardItem(mHangUpId);
			Icon.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(hangUpRewardItem.Icon)).Result;
		}
	}

	private void OnHangUpLevelUp(int hangUpId, int _)
	{
		if (mHangUpId == hangUpId)
		{
			RefreshLevel();
		}
	}

	public void RefreshSelect(bool select)
	{
		SelectGo.SetActive(select);
		mIsSelect = select;
	}

	private void OnClickSwitchBtn()
	{
		if (!mIsSelect)
		{
			mOnSwitch?.Invoke(mHangUpId, this);
		}
	}

	private void RefreshLevel()
	{
		LevelTxt.SetKey("ui_common_level_style_11", Services.Player.GetHangUpLevel(mHangUpId));
	}
}
}
