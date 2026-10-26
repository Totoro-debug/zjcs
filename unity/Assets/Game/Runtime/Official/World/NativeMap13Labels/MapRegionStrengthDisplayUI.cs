// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using UnityEngine;

public class MapRegionStrengthDisplayUI : UIBase
{
	[Header("为True的时候是被境界压制")]
	[SerializeField]
	private StateSwitcher<bool> mRankSuppressionSwitcher;

	[Header("不同StrengthDisplayType显示的内容")]
	[SerializeField]
	private StateSwitcher<MapRegionStrengthDisplayType> mRegionStrengthDisplaySwitcher;

	[Header("区域描述表现切换工具")]
	[SerializeField]
	private MapRegionDescHelper mRegionDescHelper;

	[Header("变色组件")]
	[SerializeField]
	private UIColorByLevel mUIColorByLevel;

	private MapRegionInfo mRegionCfg;

	

	public void Init(MapRegionInfo regionInfo)
	{
		InitWrapper(() =>
		{
			mRegionCfg = regionInfo;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mRegionDescHelper.Services = Services;
		mRegionDescHelper.Init(mRegionCfg);
		int num = Services.Data.CalcRankSuppression(Services.Player.SubRank, mRegionCfg.SubRank);
		mRankSuppressionSwitcher.SetState(num > 0);
		mRegionStrengthDisplaySwitcher.SetState(mRegionCfg.StrengthDisplayType);
		if (Services.Player != null)
		{
			Services.Player.OnCombatRatingChanged += OnCombatRatingChanged;
		}
		Refresh();
	}

	protected override void OnDestroy()
	{
		mRegionCfg = null;
		base.OnDestroy();
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Player != null)
		{
			Services.Player.OnCombatRatingChanged -= OnCombatRatingChanged;
		}
		mRegionDescHelper.Release();
		base.OnDisableAndInited();
	}

	private void OnCombatRatingChanged(long preCbt, long nowCbt)
	{
		Refresh();
	}

	private void Refresh()
	{
		if (mRegionCfg != null)
		{
			int combatRatingLevel = Map13LabelFormula.GetCombatRatingLevel(Services.Player.CombatRating, mRegionCfg.CombatRatingLevelValues);
			mUIColorByLevel.SetColorByLevel(combatRatingLevel);
		}
	}
}

}
