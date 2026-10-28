using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.Map13GridVisibility;
using MapRegionStatus=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
public class MiniMapRegionDescItem : UIBase
{
	[SerializeField]
	private GameObject mContent;

	public MiniMapKeepSizeObj MapKeepSizeObj;

	[Header("区域描述Ui")]
	[SerializeField]
	private MapRegionDescItemUI mMapRegionDescItemUI;

	private int mMapClassId;

	private int mRegion = -1;

	private MiniMapDescriptionContext mMiniMap;

	private MiniMapRegionDescManager mManager;

	private MiniMapDescriptionContext MapRootManagerProxy => mMiniMap;

	private MiniMapDescriptionContext MyMapPositionHandler => mMiniMap;

	public bool IsRegionUnlocked { get; private set; }

	public MapRegionStatus RegionStatus { get; private set; }

	public void Init(MiniMapRegionDescManager manager, int region, bool unlocked, MiniMapSettings.MiniMapSetting.MiniMapRegionDescSetting descSetting, List<Pos2d> regionPosList)
	{
		InitWrapper(() =>
		{
			mManager = manager;
			mMiniMap = mManager.MiniMap;
			Services = mMiniMap.LabelServices;
			mMapRegionDescItemUI.Services = Services;
			mMapClassId = mMiniMap.MapClassId;
			mRegion = region;
			TryRefreshByRegionStatus();
			mMapRegionDescItemUI.Init(mMapClassId, mRegion, mMiniMap, regionPosList);
			MapKeepSizeObj.Init(mMiniMap, descSetting?.ZoomThreshold ?? 0f);
			ShowContent(unlocked);
			if (!unlocked)
			{
				AddLockListener();
			}
		});
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		RemoveLockListener();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		TryRefreshByRegionStatus();
		if (RegionStatus != MapRegionStatus.Public)
		{
			mMiniMap.LabelServices.Player.OnMapStatusUpdate += OnMapStatusUpdate;
		}
	}

	protected override void OnDisableAndInited()
	{
		if (mMiniMap.LabelServices.Player != null)
		{
			mMiniMap.LabelServices.Player.OnMapStatusUpdate -= OnMapStatusUpdate;
		}
		base.OnDisableAndInited();
	}

	private void OnPosVisibilityUpdate(Pos2d pos, bool preVi, bool vi)
	{
		if (vi && MyMapPositionHandler.QueryRegion(pos) == mRegion && mManager.IsUsableJunctionPos(pos.X, pos.Y, mRegion))
		{
			ShowContent(show: true);
			RemoveLockListener();
		}
	}

	private void OnWorldMapProgressUpdate(int mapClassId, List<int> regionProgresses)
	{
		if (mapClassId == mMapClassId && mRegion >= 0 && mRegion < regionProgresses.Count && regionProgresses[mRegion] > 0)
		{
			ShowContent(show: true);
			RemoveLockListener();
		}
	}

	private void OnMapStatusUpdate(int mapClassId, int region, MapRegionStatus preVisibility, MapRegionStatus visibility)
	{
		if (mapClassId == mMapClassId && region == mRegion)
		{
			TryRefreshByRegionStatus();
		}
	}

	private void ShowContent(bool show)
	{
		mContent.SetActive(show);
		IsRegionUnlocked = show;
	}

	private void TryRefreshByRegionStatus()
	{
		MapRegionStatus mapRegionStatus = mMiniMap.LabelServices.Player.GetMapRegionStatus(mMapClassId, mRegion);
		if (mapRegionStatus != RegionStatus)
		{
			RegionStatus = mapRegionStatus;
		}
	}

	private void AddLockListener()
	{
		if (mMapClassId == MapRootManagerProxy.CurrentMapClassId)
		{
			MapRootManagerProxy.OnPosVisibilityUpdate += OnPosVisibilityUpdate;
		}
		if (mMiniMap.LabelServices.Player != null)
		{
			mMiniMap.LabelServices.Player.OnWorldMapProgressUpdate += OnWorldMapProgressUpdate;
		}
	}

	private void RemoveLockListener()
	{
		if (MapRootManagerProxy.HasGridVisibilityAgent)
		{
			MapRootManagerProxy.OnPosVisibilityUpdate -= OnPosVisibilityUpdate;
		}
		if (mMiniMap.LabelServices.Player != null)
		{
			mMiniMap.LabelServices.Player.OnWorldMapProgressUpdate -= OnWorldMapProgressUpdate;
		}
	}
}

}
