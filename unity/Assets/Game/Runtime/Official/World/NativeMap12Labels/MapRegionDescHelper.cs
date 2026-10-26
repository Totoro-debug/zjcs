// Selected official Map12 world-label implementation; see source receipt.
namespace Zjcs.Official.World.NativeMap12Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using System;
using UnityEngine;

[Serializable]
public class MapRegionDescHelper
{
	public IMap12LabelServices Services { get; set; }

	public enum MapRegionDescType
	{
		None,
		Public,
		PrivateNoLimit,
		PrivateLimitIsMet,
		PrivateLimitNotMet
	}

	[SerializeField]
	private StateSwitcher<MapRegionDescType> mRegionStrengthDisplaySwitcher;

	[SerializeField]
	private MapRegionLimitStatusRefreshHelper mLimitStatusRefreshHelper;

	private MapRegionInfo mMapRegionInfo;

	public void Init(MapRegionInfo mapRegionInfo)
	{
		mLimitStatusRefreshHelper.Services = Services;
		mMapRegionInfo = mapRegionInfo;
		Services.Player.OnMapStatusUpdate += MainPlayer_OnMapStatusUpdate;
		Services.Player.OnPlanesRankUnlock += new Action<int>(OnPlanesRankUnlock);
		if (mMapRegionInfo.IsLimited(Services.Player.Level))
		{
			mLimitStatusRefreshHelper.OnStatusUpdate += new Action<MapRegionLimitType>(OnStatusUpdate);
		}
		mLimitStatusRefreshHelper.Start(mMapRegionInfo);
		Refresh();
	}

	public void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnMapStatusUpdate -= MainPlayer_OnMapStatusUpdate;
			Services.Player.OnPlanesRankUnlock -= new Action<int>(OnPlanesRankUnlock);
		}
		mLimitStatusRefreshHelper.OnStatusUpdate -= new Action<MapRegionLimitType>(OnStatusUpdate);
		mLimitStatusRefreshHelper.Stop();
		mMapRegionInfo = null;
	}

	private void OnStatusUpdate(MapRegionLimitType limitType)
	{
		if (!mMapRegionInfo.IsLimited(Services.Player.Level))
		{
			mLimitStatusRefreshHelper.OnStatusUpdate -= new Action<MapRegionLimitType>(OnStatusUpdate);
		}
		Refresh();
	}

	private void OnPlanesRankUnlock(int rank)
	{
		Refresh();
	}

	private void MainPlayer_OnMapStatusUpdate(int mapClassId, int region, MapRegionStatus preStatus, MapRegionStatus status)
	{
		if (mMapRegionInfo != null && mMapRegionInfo.ClassId == mapClassId && mMapRegionInfo.Region == region)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		mRegionStrengthDisplaySwitcher.SetState(GetRegionDescType());
	}

	private MapRegionDescType GetRegionDescType()
	{
		if (Services.Player.RegionIsActived(mMapRegionInfo.ClassId, mMapRegionInfo.Region))
		{
			return MapRegionDescType.Public;
		}
		if (!mMapRegionInfo.ExistLimit())
		{
			return MapRegionDescType.PrivateNoLimit;
		}
		if (mMapRegionInfo.IsLimited(Services.Player.Level))
		{
			return MapRegionDescType.PrivateLimitNotMet;
		}
		return MapRegionDescType.PrivateLimitIsMet;
	}
}

}
