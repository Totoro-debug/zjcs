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
public class HangUpLevelUpPanel : UIBase
{
    public IHomeCartWindowServices Services { protected get; set; }
	[SerializeField]
	private Image mIcon;

	[SerializeField]
	private MultiLanguageText mNameTxt;

	public FurnitureOutputInfo OutputInfo;

	[Header("可升级、满级、功能限制显示的对象")]
	public PlayerUseItemContent UseItemContent;

	[SerializeField]
	private Transform mSwitchItemSlot;

	[SerializeField]
	private HangUpLevelUpSwitchItem mSwitchItemPrefab;

	[SerializeField]
	private ScrollRect mScrollRect;

	[SerializeField]
	private Button mCalcLevelUpBtn;

	[SerializeField]
	private MultiLanguageText mCalcLevelUpText;

	private readonly List<HangUpInfo> mHangUpInfoList = new List<HangUpInfo>();

	private readonly List<HangUpLevelUpSwitchItem> mSwitchItems = new List<HangUpLevelUpSwitchItem>();

	private int mHangUpId;

	private int mMaxLevel;

	private Action<int, HangUpLevelUpSwitchItem> mSwitchHangUp;

	private HangUpLevelUpSwitchItem mItem;

	private HomeCartWindowResourceBag mDisposableBag = new HomeCartWindowResourceBag();

	private void Awake()
	{
		UseItemContent.UseItemBtn.onClick.AddListener(OnLevelUpBtnClick);
		mCalcLevelUpBtn.onClick.AddListener(mCalcLevelUpBtnClick);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Player.OnHangUpLevelUp += MainPlayer_OnFurnitureLevelUp;
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		mDisposableBag ??= new HomeCartWindowResourceBag();
		Refresh();
		RefreshItems();
		FocusSwitchItem().Forget();
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Player != null)
		{
			Services.Player.OnHangUpLevelUp -= MainPlayer_OnFurnitureLevelUp;
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		mDisposableBag?.Dispose();
	}

	protected override void OnDestroy()
	{
		UseItemContent.UseItemBtn.onClick.RemoveListener(OnLevelUpBtnClick);
		mCalcLevelUpBtn.onClick.RemoveListener(mCalcLevelUpBtnClick);
		base.OnDestroy();
	}

	private void MainPlayer_OnFurnitureLevelUp(int hangUpId, int level)
	{
		if (hangUpId == mHangUpId)
		{
			Refresh(instant: false);
		}
	}

	private void MainPlayer_OnItemCountUpdate(IProfessionItemData dataWrap, long count, object awardSourceData)
	{
		Refresh();
	}

	public void Init(int hangUpId, EFurniture furniture)
	{
		mHangUpId = hangUpId;
		if (mSwitchHangUp == null)
		{
			mSwitchHangUp = SwitchHangUp;
		}
		mHangUpInfoList.Clear();
		Player mainPlayer = Services.Player;
		if (Services.TryGetHangUpInfos(furniture, out var value))
		foreach (HangUpInfo item in value)
		{
			if (item.CanLevelUp && Services.Player.TryGetUnlockSingleHangUpData(item.Id, out var _))
			{
				mHangUpInfoList.Add(item);
			}
		}
		mHangUpInfoList.Sort((HangUpInfo a, HangUpInfo b) =>
		{
			int sortOrder = a.SortOrder;
			return sortOrder.CompareTo(b.SortOrder);
		});
		InitWrapper(null);
	}

	private void RefreshItems()
	{
		while (mSwitchItems.Count < mHangUpInfoList.Count)
		{
			HangUpLevelUpSwitchItem item = Services.InstantiateLevelUpSwitchItem(mSwitchItemPrefab, mSwitchItemSlot);
			mSwitchItems.Add(item);
		}
		int i = 0;
		for (int count = mSwitchItems.Count; i < count; i++)
		{
			HangUpLevelUpSwitchItem hangUpLevelUpSwitchItem = mSwitchItems[i];
			if (i >= mHangUpInfoList.Count) { hangUpLevelUpSwitchItem.gameObject.SetActive(false); continue; }
			hangUpLevelUpSwitchItem.gameObject.SetActive(true);
			int id = mHangUpInfoList[i].Id;
			bool flag = id == mHangUpId;
			hangUpLevelUpSwitchItem.Init(id, mSwitchHangUp);
			hangUpLevelUpSwitchItem.RefreshSelect(flag);
			if (flag)
			{
				mItem = hangUpLevelUpSwitchItem;
			}
		}
	}

	private async UniTaskVoid FocusSwitchItem()
	{
		await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
		if (this == null || !gameObject.activeInHierarchy) return;
		if (mHangUpInfoList.Count <= 1 || mItem == null)
		{
			mScrollRect.horizontalNormalizedPosition = 0f;
			return;
		}
		int num = mSwitchItems.IndexOf(mItem);
		float value = 1f * (float)num / (float)(mHangUpInfoList.Count - 1);
		mScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(value);
	}

	private void Refresh(bool instant = true)
	{
		mIcon.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(Services.GetCartHangUpIconPath(mHangUpId))).Result;
		mNameTxt.SetKey(Services.GetHangUpName(mHangUpId));
		mMaxLevel = Services.GetUpgradeMaxLevel(mHangUpId);
		int hangUpLevel = Services.Player.GetHangUpLevel(mHangUpId);
		int toLevel = Mathf.Min(hangUpLevel + 1, mMaxLevel);
		OutputInfo.ShowLevelUpInfo(mHangUpId, hangUpLevel >= mMaxLevel, hangUpLevel, toLevel, instant);
		RefreshBeforeUpgrade();
	}

	private void RefreshBeforeUpgrade()
	{
		int hangUpLevel = Services.Player.GetHangUpLevel(mHangUpId);
		if (hangUpLevel >= mMaxLevel)
		{
			UseItemContent.SetUseItemState(PlayerUseItemContent.UseItemState.Max);
			return;
		}
		Dictionary<int, long> items = Services.GetUpgradeMaterialCost(mHangUpId, hangUpLevel + 1);
		UseItemContent.Init(items);
		if (!IsEligibleForLevelUp(out var errorText))
		{
			UseItemContent.SetUseItemState(PlayerUseItemContent.UseItemState.Limit);
			UseItemContent.CannotMakeText.SetKey(errorText);
		}
		else
		{
			UseItemContent.SetUseItemState(PlayerUseItemContent.UseItemState.Normal);
			RefreshCalcLevelUpBtn(PlayerUseItemContent.UseItemState.Normal);
		}
	}

	private void RefreshCalcLevelUpBtn(PlayerUseItemContent.UseItemState state)
	{
		if (state != PlayerUseItemContent.UseItemState.Normal)
		{
			return;
		}
		mCalcLevelUpBtn.gameObject.SetActive(value: false);
		if (!Services.Player.IsFunctionLimit(IHomeCartWindowServices.FunctionHangUpCalcLevelUp) && Services.Player.GetHangUpLevel(mHangUpId) < mMaxLevel - 1)
		{
			int num = Services.Player.CalcLevelUpNum(mHangUpId, mMaxLevel);
			if (num == Services.HangUpCalcMaxLevelUp)
			{
				mCalcLevelUpText.Format(CultureNumberArg.Compact(num));
				mCalcLevelUpBtn.gameObject.SetActive(value: true);
			}
		}
	}

	private void OnLevelUpBtnClick()
	{
		if (Services.CheckFunctionDisabled(IHomeCartWindowServices.ServerFunctionHangUpLevelUp))
		{
			Services.BroadcastErrCode(IHomeCartWindowServices.ErrorHangUpLevelUpDisabled);
			return;
		}
		if (IsEligibleForLevelUp(out var _))
		{
			Services.LevelUpHangUp(mHangUpId, 1);
		}
	}

	private void mCalcLevelUpBtnClick()
	{
		if (!Services.Player.IsFunctionLimit(IHomeCartWindowServices.FunctionHangUpCalcLevelUp))
		{
			int num = Services.Player.CalcLevelUpNum(mHangUpId, mMaxLevel);
			if (num == Services.HangUpCalcMaxLevelUp)
			{
				Services.LevelUpHangUp(mHangUpId, num);
			}
		}
	}

	private bool IsEligibleForLevelUp(out string errorText)
	{
		int hangUpLevel = Services.Player.GetHangUpLevel(mHangUpId);
		int hangUpMaxLevel = Services.Player.GetHangUpMaxLevel();
		if (hangUpLevel >= hangUpMaxLevel)
		{
			errorText = Services.TextFormatted("function_limit_homelevel", Services.Player.GetBlessingLevel() + 1);
			return false;
		}
		Dictionary<int, long> items = Services.GetUpgradeMaterialCost(mHangUpId, hangUpLevel + 1);
		if (!Services.Player.HasItems(items))
		{
			errorText = Services.Text("ui_level_up_item_not_enough");
			return false;
		}
		errorText = null;
		return true;
	}

	private void SwitchHangUp(int hangUpId, HangUpLevelUpSwitchItem item)
	{
		if (mHangUpId != hangUpId)
		{
			mItem.RefreshSelect(select: false);
			item.RefreshSelect(select: true);
			mItem = item;
			mHangUpId = hangUpId;
			Refresh();
		}
	}
}
}
