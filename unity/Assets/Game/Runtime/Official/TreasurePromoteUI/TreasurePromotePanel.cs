// Complete original promotion controller; explicit saved-player, parser and window ports.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasurePromoteUI {
using Object=UnityEngine.Object;
using System;
using System.Collections.Generic;






using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TreasurePromotePanel : UIBase
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	public enum TabType
	{
		Levelup,
		Upgrade
	}

	[SerializeField]
	private WorldMultiLanguageText mName;

	[SerializeField]
	private Image mItemIcon;

	[SerializeField]
	private WorldMultiLanguageText mFakeIcon;

	[SerializeField]
	private Image mQualityIcon;

	[SerializeField]
	private PanelTitle mTitle;

	[SerializeField]
	private TreasureLevelupPanel mTreasureLevelupPanel;

	[SerializeField]
	private TreasureUpgradePanel mTreasureUpgradePanel;

	[SerializeField]
	private WorldTabGroup mToggleGroup;

	[SerializeField]
	private GameObject mTabGroupGo;

	[SerializeField]
	private List<TreasureRedDotItem> mReddots;

	[SerializeField]
	private RectTransform mMiddleRect;

	[SerializeField]
	private TreasurePageSelectItem mPageSelectItem;

	private IProfessionItemData mItemData;

	private ProfessionResourceOperationBag mDisposableBag;


	public IProfessionItemData ItemData => mItemData;

	protected void Awake()
	{

		((UnityEvent<WorldTabItem, bool>)mToggleGroup.OnTabItemChanged).AddListener((UnityAction<WorldTabItem, bool>)OnToggleGroupValueChanged);
		mDisposableBag = new ProfessionResourceOperationBag();
	}

	protected override void OnDestroy()
	{
		mDisposableBag.Dispose();
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		_ = Services.Tables.GetTreasure(mItemData.Info.ClassId);
		WorldTabItem tabItem = mToggleGroup.GetTabItem(0);
		mToggleGroup.ResetTabItem(needNotify: true, tabItem);
		mTabGroupGo.gameObject.SetActive(true);
		TreasurePageSelectItem treasurePageSelectItem = mPageSelectItem;
		treasurePageSelectItem.OnTreasureSelected = (Action<int>)Delegate.Combine(treasurePageSelectItem.OnTreasureSelected, new Action<int>(ChangedTreasure));
		RefreshReddots();
		RefreshPageSelectItem();
	}

	protected override void OnDisableAndInited()
	{
		TreasurePageSelectItem treasurePageSelectItem = mPageSelectItem;
		treasurePageSelectItem.OnTreasureSelected = (Action<int>)Delegate.Remove(treasurePageSelectItem.OnTreasureSelected, new Action<int>(ChangedTreasure));
		base.OnDisableAndInited();
	}

	private void OnToggleGroupValueChanged(WorldTabItem tabItem, bool isOn)
	{
		if (!isOn)
		{
			return;
		}
		TabType tabType = ((Component)tabItem).GetComponent<TreasurePromoteTab>().TabType;
		switch (tabType)
		{
		case TabType.Levelup:
			((Component)mTreasureUpgradePanel).gameObject.SetActive(false);
			mTitle.SetKey("TreasurePromotePanel.LevelUp.Title");
			mTreasureLevelupPanel.Show(mItemData);
			break;
		case TabType.Upgrade:
			((Component)mTreasureLevelupPanel).gameObject.SetActive(false);
			mTitle.SetKey("TreasurePromotePanel.RankUp.Title");
			mTreasureUpgradePanel.Show(mItemData);
			break;
		default: Debug.LogWarning("未处理的 TreasureTab 类型！"+tabType);break;
		}
		RefreshReddots();
	}

	public void Init(int index)
	{
		InitWrapper(() =>
		{
			mItemData = Services.Icons.GetItemById(index);
			if ((Object)(object)mFakeIcon != (Object)null)
			{
				((Component)mFakeIcon).gameObject.SetActive(false);
			}
			((Component)mItemIcon).gameObject.SetActive(true);
			mItemIcon.sprite = mDisposableBag.Add(Services.Images.LoadAsset<Sprite>("Assets/Image/"+mItemData.Info.Icon)).Result;
			if ((Object)(object)mItemIcon.sprite == (Object)null && (Object)(object)mFakeIcon != (Object)null)
			{
				mFakeIcon.SetKey("{0}", mItemData.Info.GetFakeIcon());
				((Component)mFakeIcon).gameObject.SetActive(true);
				((Component)mItemIcon).gameObject.SetActive(false);
			}
			mQualityIcon.sprite = mDisposableBag.Add(Services.Images.LoadAsset<Sprite>(Services.QualityBackground(mItemData.GetQuality()))).Result;
			mName.SetKey(Services.Materials.GetItemNameKey(mItemData.Id));
		});
	}

	private void RefreshPageSelectItem()
	{


		bool flag = Services.Player.IsTreasureInSlot(mItemData.Info.ClassId);
		((Component)mPageSelectItem).gameObject.SetActive(flag);
		if (flag && !mPageSelectItem.CheckSameElement((ElementType)Services.Icons.GetElement(mItemData.Info.ClassId)))
		{
			mPageSelectItem.Init(mItemData);
		}
		mMiddleRect.offsetMax = new Vector2(mMiddleRect.offsetMax.x, (float)(flag ? (-225) : (-140)));
	}

	private void ChangedTreasure(int id)
	{
		IProfessionItemData itemById = Services.Icons.GetItemById(id);
		if (itemById != null)
		{
			Init(itemById.Id);
		}
	}

	private void RefreshReddots()
	{
		foreach (TreasureRedDotItem mReddot in mReddots)
		{
			mReddot.Init(mItemData.Id);
		}
	}
}

}
