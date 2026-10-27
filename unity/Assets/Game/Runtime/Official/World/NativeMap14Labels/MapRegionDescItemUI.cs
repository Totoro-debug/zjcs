// Selected official Map14 world-label implementation; see source receipt.
namespace Zjcs.Official.World.NativeMap14Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MapRegionDescItemUI : MonoBehaviour
{
	public IMap14LabelServices Services { get; set; }

	[Header("区域名字")]
	[SerializeField]
	private List<MultiLanguageText> mRegionNameTexts;

	[Header("区域探索度")]
	[SerializeField]
	private List<MultiLanguageText> mProgressTexts;

	[Header("区域等级")]
	[SerializeField]
	public MultiLanguageText mRegionLevelText;

	[Header("区域推荐战力")]
	[SerializeField]
	private MultiLanguageText mRegionCombatRatingText;

	[Header("区域强度表现")]
	[SerializeField]
	private MapRegionStrengthDisplayUI mRegionStrengthDisplayUI;

	[Header("区域名以外显示内容的节点")]
	[SerializeField]
	private GameObject mExtendSlot;

	[Header("区域强调类物件的挂载父节点，不缩放")]
	[SerializeField]
	private Transform mHighlightSlotWithoutScale;

	private int mMapClassId;

	private int mRegion = -1;

	private MapRegionInfo mRegionCfg;

	private bool mIsMember;











	public void Init(int mapClassId, int region)
	{
		mMapClassId = mapClassId;
		mRegion = region;
		mRegionCfg = Services.GetRegion(mMapClassId, mRegion);
		mRegionStrengthDisplayUI.Init(mRegionCfg);
		mIsMember = Services.IsTeamMember;
		Refresh(mRegionCfg);
		RefreshProgressText();
		RefreshExtendShow();

	}

	private void OnEnable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnWorldMapProgressUpdate += MyMap_OnProgressUpdate;
			Services.Player.OnMapStatusUpdate += MainPlayer_OnMapStatusUpdate;
		}
		RefreshProgressText();
	}

	private void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnMapStatusUpdate -= MainPlayer_OnMapStatusUpdate;
			Services.Player.OnWorldMapProgressUpdate -= MyMap_OnProgressUpdate;
		}
	}



	private void MyMap_OnProgressUpdate(int mapClassId, List<int> regionProgresses)
	{
		if (mapClassId == mMapClassId)
		{
			RefreshProgressText();
		}
	}

	private void MainPlayer_OnMapStatusUpdate(int mapClassId, int region, MapRegionStatus preStatus, MapRegionStatus status)
	{
		if (mMapClassId == mapClassId && mRegionCfg != null && mRegionCfg.Region == region)
		{
			RefreshExtendShow();
		}
	}





	private void Refresh(MapRegionInfo regionInfo)
	{
		string regionNameKey = Services.GetRegionNameKey(regionInfo.ClassId, regionInfo.Region);
		foreach (MultiLanguageText text in mRegionNameTexts) text.SetKey(regionNameKey);
		if ((Object)(object)mRegionLevelText != (Object)null)
		{
			mRegionLevelText.Format(regionInfo.Level);
		}
		if ((Object)(object)mRegionCombatRatingText != (Object)null)
		{
			mRegionCombatRatingText.Format(Services.CompactNumber(regionInfo.CombatRating));
		}
	}

	private void RefreshExtendShow()
	{
		if (!((Object)(object)mExtendSlot == (Object)null))
		{
			bool flag = false;
			if (!mIsMember)
			{
				flag = Services.Player.GetMapRegionStatus(mRegionCfg.ClassId, mRegionCfg.Region) == MapRegionStatus.Private;
			}
			if (flag != mExtendSlot.activeSelf)
			{
				mExtendSlot.SetActive(flag);
			}
		}
	}

	private void RefreshProgressText()
	{
		if (mProgressTexts == null || mProgressTexts.Count <= 0)
		{
			return;
		}
		int worldMapRegionProgress = Services.Player.GetWorldMapRegionProgress(mMapClassId, mRegion);
		int num = Services.CalcWorldMapRegionProgress(mMapClassId, mRegion, worldMapRegionProgress);
		if (Services.Player.RegionIsActived(mMapClassId, mRegion))
		{
			num = 1;
		}
		if (((float)num / 10000f) >= 1f)
		{
			Services.Player.OnWorldMapProgressUpdate -= MyMap_OnProgressUpdate;
		}
		string text = (num / 100).ToString();
		foreach (MultiLanguageText mProgressText in mProgressTexts)
		{
			mProgressText.Format(text);
		}
	}

}

}
