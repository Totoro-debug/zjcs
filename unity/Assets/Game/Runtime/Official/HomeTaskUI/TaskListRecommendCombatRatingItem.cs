// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using UnityEngine;

public class TaskListRecommendCombatRatingItem : MonoBehaviour
{
	public IHomeTaskServices Services { get; set; }
	[SerializeField]
	private MultiLanguageText mCombatRatingText;

	[SerializeField]
	private UIColorByLevel mColorByLevel;

	[SerializeField]
	private WorldStateSwitcher<bool> mIsRankOverStates;

	private MapRegionInfo mRegionCfg;

	private void OnEnable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnCombatRatingChanged += OnCombatRatingChanged;
			Services.Player.OnSubRankUpdate += OnSubRankUpdate;
		}
	}

	private void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnCombatRatingChanged -= OnCombatRatingChanged;
			Services.Player.OnSubRankUpdate -= OnSubRankUpdate;
		}
	}

	private void OnDestroy()
	{
		mRegionCfg = null;
	}

	public void SetMapRegion(MapRegionInfo regionInfo)
	{
		if (regionInfo != null)
		{
			mRegionCfg = regionInfo;
			RefreshContent();
		}
	}

	private void RefreshContent()
	{
		RefreshCombatDesc();
		RefreshState();
	}

	private void OnCombatRatingChanged(long preCbt, long nowCbt)
	{
		RefreshState();
	}

	private void OnSubRankUpdate(SubRank rank)
	{
		RefreshContent();
	}

	private void RefreshCombatDesc()
	{
		if (Services.CalcRankSuppression(Services.Player.SubRank, mRegionCfg.SubRank) > 0)
		{
			mCombatRatingText.Format(new CultureTextArg("ui_map_region_rank_suppression_text"));
			mIsRankOverStates.SetState(state: true);
		}
		else
		{
			mCombatRatingText.Format(CultureNumberArg.Compact(mRegionCfg.CombatRating));
			mIsRankOverStates.SetState(state: false);
		}
	}

	private void RefreshState()
	{
		if (mRegionCfg != null && Services.Player != null)
		{
			int num = 0;
			num = ((Services.CalcRankSuppression(Services.Player.SubRank, mRegionCfg.SubRank) > 0) ? (-1) : Services.GetCombatRatingLevel(Services.Player.CombatRating, mRegionCfg.CombatRatingLevelValues));
			mColorByLevel.SetColorByLevel(num);
		}
	}
}
}
