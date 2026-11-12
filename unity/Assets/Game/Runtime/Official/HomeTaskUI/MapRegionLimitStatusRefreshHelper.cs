// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using System;
using UnityEngine;

[Serializable]
public class MapRegionLimitStatusRefreshHelper
{
	public IHomeTaskServices Services { get; set; }
	[SerializeField]
	private MultiLanguageText mMultiLanguage;

	[SerializeField]
	private ShowRemainTimeInfo mRemainTimeInfoUI;

	[SerializeField]
	private WorldStateSwitcher<MapRegionLimitType> mRegionLimitSwitcher;

	private MapRegionLimitType mRegionLimitType;

	private MapRegionInfo mMapRegionInfo;

	private IMapRegionLimitStatusRefreshProvider mRefreshProvider;

	public Action<MapRegionLimitType> OnStatusUpdate { get; set; } = delegate { };

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
		MapRegionLimitType mapRegionLimitType = mMapRegionInfo.IsLimitedByType(Services.Player.Level, Services.PlanesRank);
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
				((MapRegionLimitStatusRefreshForRankUnlockTime)(mRefreshProvider = Services.CreateTimedRegionLimitProvider())).Init(mMapRegionInfo, mRemainTimeInfoUI, (Action)RefreshStatus);
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
