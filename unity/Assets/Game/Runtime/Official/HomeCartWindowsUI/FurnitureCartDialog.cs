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
public class FurnitureCartDialog : UIBase
{
    public IHomeCartWindowServices Services { protected get; set; }
	public MultiLanguageText Name;

	public MultiLanguageText Desc;

	public Button GetHangUpRewardBtn;

	public Transform ItemSlot;

	public FurnitureCartHangUpItem HangUpItemPrefab;

	[SerializeField]
	private FurnitureHangUpQuickReceive mFurnitureHangUpQuickReceive;

	[SerializeField]
	private WorldStateSwitcher<int> mTipLevelDotSwitcher;

	protected EFurniture mFurniture;

	private readonly List<HangUpInfo> mHangUpInfoList = new List<HangUpInfo>();

	private readonly List<int> mGetAwardHangUpIdList = new List<int>();

	private readonly List<FurnitureCartHangUpItem> mItems = new List<FurnitureCartHangUpItem>();

	private readonly Dictionary<int, FurnitureCartHangUpItem> mItemDict = new Dictionary<int, FurnitureCartHangUpItem>();

	private Action<int> mOnHangUpItemClick;

	private IDisposable mDisposable;

	private readonly List<FurnitureCartHangUpItem> mStoryItems = new List<FurnitureCartHangUpItem>();

	protected virtual void Awake()
	{
		GetHangUpRewardBtn.onClick.AddListener(OnGetHangUpReward);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Player.OnHangUpUnlock += OnHangUpUnlock;
		mDisposable = Services.SubscribeTips(OnTipLevelChange);
		Refresh();
		UpdateTipLevelDotSwitcher(Services.GetTipLevel(mFurniture));
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Player != null)
		{
			Services.Player.OnHangUpUnlock -= OnHangUpUnlock;
		}
		mDisposable?.Dispose();
		mDisposable = null;
		base.OnDisableAndInited();
	}

	private void OnHangUpUnlock(int hangUpId)
	{
		Refresh();
	}

	private void OnTipLevelChange(HangUpTipLevelChangeData data)
	{
		if (data.FurnitureType == mFurniture)
		{
			UpdateTipLevelDotSwitcher(data.TipLevel);
		}
	}

	private void UpdateTipLevelDotSwitcher(int tipLevel)
	{
		mTipLevelDotSwitcher.SetState(tipLevel);
	}

	private void OnGetHangUpReward()
	{
		mGetAwardHangUpIdList.Clear();
		foreach (HangUpInfo mHangUpInfo in mHangUpInfoList)
		{
			if (Services.CanGetHangUpReward(mHangUpInfo.Id))
			{
				mGetAwardHangUpIdList.Add(mHangUpInfo.Id);
			}
		}
		if (mGetAwardHangUpIdList.Count == 0)
		{
			Services.SystemMessage(Services.Text("fast_hang_up_no_award"));
			return;
		}
		Services.Collect(mGetAwardHangUpIdList, (IHomeHangUpAwardMessage rep) =>
		{
			if (rep.DataList != null && rep.DataList.Count > 0)
			{
				Services.AwardCollected(rep.DataList, rep.SourceData);
				Services.MergeAward(rep.DataList);
				if (mFurniture == EFurniture.Cart)
				{
					Services.PopupAwardPanel(rep);
				}
				else
				{
					Services.PopupAwardList(rep.DataList);
				}
			}
			else
			{
				Services.SystemMessage(Services.Text("fast_hang_up_no_award"));
			}
			Services.PublishCollected();
		});
	}

	public void Init(EFurniture furniture)
	{
		mFurniture = furniture;
		int classId = Services.GetFurnitureInfo(furniture).ClassId;
		Name.SetKey(Services.GetItemNameKey(classId));
		if ((Object)Desc != (Object)null)
		{
			Desc.SetKey(Services.GetItemDescKey(classId));
		}
		InitWrapper(null);
		mFurnitureHangUpQuickReceive.Init(furniture);
	}

	private void Refresh()
	{
		if (mOnHangUpItemClick == null)
		{
			mOnHangUpItemClick = OnHangUpItemClick;
		}
		UpdateHangUpList();
	}

	private void UpdateHangUpList()
	{
		Player mainPlayer = Services.Player;
		mHangUpInfoList.Clear();
		Services.TryGetHangUpInfos(mFurniture, out var value);
		if (value != null)
		foreach (HangUpInfo item in value)
		{
			if (Services.IsConditionMet(item.UnlockCondition) || Services.IsConditionMet(item.NoticeCondition))
			{
				mHangUpInfoList.Add(item);
			}
		}
		foreach (HangUpInfo mHangUpInfo in mHangUpInfoList)
		{
			if (!mItemDict.ContainsKey(mHangUpInfo.Id))
			{
				FurnitureCartHangUpItem furnitureCartHangUpItem = Services.InstantiateCartHangUpItem(HangUpItemPrefab, ItemSlot);
				furnitureCartHangUpItem.OnHangUpItemClick = OnHangUpItemClick;
				furnitureCartHangUpItem.Init(mHangUpInfo.Id);
				mItemDict.Add(mHangUpInfo.Id, furnitureCartHangUpItem);
				mItems.Add(furnitureCartHangUpItem);
			}
		}
		mItems.Sort((FurnitureCartHangUpItem a, FurnitureCartHangUpItem b) => a.HangUpInfo.SortOrder.CompareTo(b.HangUpInfo.SortOrder));
		for (int num = mItems.Count - 1; num >= 0; num--)
		{
			mItems[num].transform.SetSiblingIndex(num);
		}
	}

	private void OnHangUpItemClick(int hangUpId)
	{
		Services.OpenCartItemDetail(hangUpId, Input.mousePosition);
	}

	public List<FurnitureCartHangUpItem> GetActiveUnlockButtonItems()
	{
		mStoryItems.Clear();
		FurnitureCartHangUpItem[] componentsInChildren = ItemSlot.GetComponentsInChildren<FurnitureCartHangUpItem>();
		if (componentsInChildren == null)
		{
			return null;
		}
		FurnitureCartHangUpItem[] array = componentsInChildren;
		foreach (FurnitureCartHangUpItem furnitureCartHangUpItem in array)
		{
			if (furnitureCartHangUpItem.UnlockBtn.gameObject.activeInHierarchy)
			{
				mStoryItems.Add(furnitureCartHangUpItem);
			}
		}
		return mStoryItems;
	}
}
}
