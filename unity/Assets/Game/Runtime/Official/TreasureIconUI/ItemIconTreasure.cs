// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {
using System.Collections.Generic;

using UnityEngine;

public class ItemIconTreasure : ItemIcon
{
    protected ITreasureIconServices Services { get; private set; }
    public void ConfigureTreasure(ITreasureIconServices services) => Services=services??throw new System.ArgumentNullException(nameof(services));

	[Header("宝具")]
	[SerializeField]
	private TreasureStarInfo myStarInfo;

	[SerializeField]
	private List<TreasureRedDotItem> mRedDots;

	[SerializeField]
	private BlessLevelTextViewChanger mLevelTextView;

	[SerializeField]
	private GameObject mHasHinit;

	private bool mShowStarFlag;

	private bool mLevelFlag;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.OnTreasureRefresh += MainPlayer_OnTreasureRefresh;
		Services.OnTreasureSlotShareLevelUpdate += MainPlayer_OnTreasureSlotShareLevelUpdate;
		Services.OnAllTreasureBlessUpdate += MainPlayer_OnAllTreasureBlessUpdate;
	}

	protected override void OnDisableAndInited()
	{
		if (Services != null)
		{
			Services.OnTreasureRefresh -= MainPlayer_OnTreasureRefresh;
			Services.OnTreasureSlotShareLevelUpdate -= MainPlayer_OnTreasureSlotShareLevelUpdate;
			Services.OnAllTreasureBlessUpdate -= MainPlayer_OnAllTreasureBlessUpdate;
		}
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnAllTreasureBlessUpdate()
	{
		RefreshLevel();
	}

	private void MainPlayer_OnTreasureSlotShareLevelUpdate(int type)
	{
		if (type == Services.GetElement(myInfo.ClassId))
		{
			RefreshLevel();
		}
	}

	protected virtual void MainPlayer_OnTreasureRefresh(IProfessionItemData itemData, bool isNew)
	{
		if (myInfo != null && myInfo.ClassId == itemData.Info.ClassId)
		{
			if (isNew)
			{
				myItemData = itemData;
				InitQualityImage();
			}
			RefreshCenterHintUI();
			RefreshStar();
			RefreshLevel();
		}
	}

	public override void Clear()
	{
		mShowStarFlag = false;
		mLevelFlag = false;
		HideText();
		HideRedDot();
		RefreshStar();
		RefreshLevel();
		SetHasHinitActive(active: false);
		base.Clear();
	}

	public ItemIconTreasure ShowStar()
	{
		mShowStarFlag = true;
		RefreshStar();
		return this;
	}

	public ItemIconTreasure HideStar()
	{
		mShowStarFlag = false;
		RefreshStar();
		return this;
	}

	public ItemIconTreasure RefreshHasHinitActive()
	{
		bool hasHinitActive = Services.HasItem(myInfo.ClassId, 1L);
		SetHasHinitActive(hasHinitActive);
		return this;
	}

	public ItemIconTreasure SetHasHinitActive(bool active)
	{
		if (mHasHinit!=null)
		{
			mHasHinit.SetActive(active);
		}
		return this;
	}

	protected override bool SetCenterHintUIText()
	{
		if (!Services.HasItem(myInfo.ClassId, 1L))
		{
			return false;
		}
		IProfessionItemData playerItemDataWrap = myItemData;
		if (playerItemDataWrap != null && !Services.GetState(playerItemDataWrap).Active)
		{
			ShowUnactivate();
			return true;
		}
		return base.SetCenterHintUIText();
	}

	protected virtual void ShowUnactivate()
	{
		SetCenterHintText("treasure_common_unactivate");
	}

	private void RefreshStar()
	{
		if (!((Object)(object)myStarInfo == (Object)null))
		{
			if (mShowStarFlag && myItemData != null && Services.HasItem(myInfo.ClassId, 1L))
			{
				DoShowStar();
			}
			else
			{
				myStarInfo.Hide();
			}
		}
	}

	private void DoShowStar()
	{
		int starMax = Services.GetStarMax(myItemData);
		int star = Services.GetState(myItemData).Star;
		myStarInfo.Show(myItemData.GetQuality(), star, starMax, star >= starMax);
	}

	public ItemIconTreasure SetRedDot()
	{
		mRedDots.ForEach((TreasureRedDotItem it) =>
		{
			it.Init(myInfo.ClassId);
		});
		((Component)mRedDots[0]).gameObject.SetActive(true);
		return this;
	}

	public ItemIconTreasure HideRedDot()
	{
		mRedDots.ForEach((TreasureRedDotItem it) =>
		{
			it.SetRedDotActive(isActive: false);
		});
		return this;
	}

	public ItemIconTreasure SetLevel()
	{
		mLevelFlag = true;
		RefreshLevel();
		return this;
	}

	private void RefreshLevel()
	{
		if (!mLevelFlag)
		{
			return;
		}
		if (!Services.GetState(myItemData).Active)
		{
			HideText();
			return;
		}
		int treasureFinalLevel = Services.GetTreasureFinalLevel(myInfo.ClassId);
		if (treasureFinalLevel <= 1)
		{
			HideText();
			return;
		}
		bool isBlessing = treasureFinalLevel > Services.GetMaxTreasureBaseLevel();
		mLevelTextView.Refresh(isBlessing, needChangeOutline: false);
		SetTextArg($"+{treasureFinalLevel}");
	}
}

}
