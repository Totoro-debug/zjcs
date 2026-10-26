// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;

public class MapRegionLimitStatusRefreshProviderForLevel : IMapRegionLimitStatusRefreshProvider, IDisposable
{
	public IMap13LabelServices Services { get; set; }

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
