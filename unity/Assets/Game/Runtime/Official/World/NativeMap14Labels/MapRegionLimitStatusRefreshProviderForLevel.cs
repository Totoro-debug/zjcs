// Selected official Map14 world-label implementation; see source receipt.
namespace Zjcs.Official.World.NativeMap14Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using System;

public class MapRegionLimitStatusRefreshProviderForLevel : IMapRegionLimitStatusRefreshProvider, IDisposable
{
	public IMap14LabelServices Services { get; set; }

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
