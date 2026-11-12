// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using System;

public class MapRegionLimitStatusRefreshProviderForLevel : IMapRegionLimitStatusRefreshProvider, IDisposable
{
	public IHomeTaskServices Services { get; set; }
	private MapRegionInfo mMapRegionInfo;

	private MultiLanguageText mMultiLanguage;

	private Action mOnRefresh;

	public MapRegionLimitType RegionLimitType => MapRegionLimitType.Level;

	public void Init(MapRegionInfo mapRegionInfo, MultiLanguageText multiLanguage, Action onRefresh)
	{
		mMapRegionInfo = mapRegionInfo;
		mMultiLanguage = multiLanguage;
		mOnRefresh = onRefresh;
		Services.Player.OnLevelUpdate += Player_OnLevelUpdate;
		Player_OnLevelUpdate(Services.Player.Level);
	}

	void IDisposable.Dispose()
	{
		mOnRefresh = null;
		if (Services.Player != null)
		{
			Services.Player.OnLevelUpdate -= Player_OnLevelUpdate;
		}
	}

	private void Player_OnLevelUpdate(int level)
	{
		mMultiLanguage.Format(mMapRegionInfo.LimitLevel);
		mOnRefresh?.Invoke();
	}
}
}
