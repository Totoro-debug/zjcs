// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using UnityEngine;

[Serializable]
public class MapRegionLimitStatusRefreshHelper
{
	public IMap13LabelServices Services { get; set; }

	[SerializeField]
	private MultiLanguageText mMultiLanguage;

	[SerializeField]
	private ShowRemainTimeInfo mRemainTimeInfoUI;

	[SerializeField]
	private StateSwitcher<MapRegionLimitType> mRegionLimitSwitcher;

	private MapRegionLimitType mRegionLimitType;

	private MapRegionInfo mMapRegionInfo;

	private IMapRegionLimitStatusRefreshProvider mRefreshProvider;

	public GAction<MapRegionLimitType> OnStatusUpdate { get; set; } = new GAction<MapRegionLimitType>();

	public void Start(MapRegionInfo regionInfo)
	{
		if (regionInfo != null && regionInfo.ExistLimit())
		{
			mMapRegionInfo = regionInfo;
			RefreshStatus();
		}
	}

	public void Stop()
	{
		if (mRefreshProvider != null)
		{
			mRefreshProvider.Dispose();
			mRefreshProvider = null;
		}
	}

	private void RefreshStatus()
	{
		MapRegionLimitType mapRegionLimitType = mMapRegionInfo.IsLimitedByType(Services.Player.Level);
		IMapRegionLimitStatusRefreshProvider mapRegionLimitStatusRefreshProvider = mRefreshProvider;
		if (mapRegionLimitStatusRefreshProvider == null || mapRegionLimitStatusRefreshProvider.RegionLimitType != mapRegionLimitType)
		{
			mRegionLimitType = mapRegionLimitType;
			OnStatusUpdate.Invoke(mRegionLimitType);
			mRegionLimitSwitcher.SetState(mapRegionLimitType);
			mRefreshProvider?.Dispose();
			switch (mRegionLimitType)
			{
			case MapRegionLimitType.RegionUnlockTime:
				throw new InvalidOperationException("Verified Map13 source has no server-rank/date region limits.");
				break;
			case MapRegionLimitType.Level:
				((MapRegionLimitStatusRefreshProviderForLevel)(mRefreshProvider = new MapRegionLimitStatusRefreshProviderForLevel { Services = Services })).Init(mMapRegionInfo, mMultiLanguage, (Action)RefreshStatus);
				break;
			case MapRegionLimitType.None:
				break;
			}
		}
	}
}

}
