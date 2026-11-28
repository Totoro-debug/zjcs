// Complete original promotion controller; explicit saved-player, parser and window ports.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasurePromoteUI {
using Object=UnityEngine.Object;

using UnityEngine;

public class TreasureUpgradePanelStarInfo : MonoBehaviour
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	public enum TreasureStarStat
	{
		Upgrade,
		Wake,
		Waked
	}

	[SerializeField]
	private TreasureStarInfo mCurrentStarInfo;

	[SerializeField]
	private WorldStateSwitcher<TreasureStarStat> mStateSwitcher;

	private IProfessionItemData mItemData;

	public void Refresh(IProfessionItemData itemData)
	{
		mItemData = itemData;
		TreasureStarStat currentStat = GetCurrentStat();
		mStateSwitcher.SetState(currentStat);
		int starMax = Services.Icons.GetStarMax(mItemData);
		TreasureState itemParamTreasure = Services.Icons.GetState(mItemData);
		Quality quality = mItemData.GetQuality();
		int star = itemParamTreasure.Star;
		switch (currentStat)
		{
		case TreasureStarStat.Upgrade:
			mCurrentStarInfo.Show(quality, star + 1, starMax - 1, isSuper: false, showBlankStar: true, blink: true);
			break;
		case TreasureStarStat.Wake:
			mCurrentStarInfo.Show(quality, star, starMax);
			break;
		}
	}

	private TreasureStarStat GetCurrentStat()
	{
		int starMax = Services.Icons.GetStarMax(mItemData);
		int star = Services.Icons.GetState(mItemData).Star;
		if (star >= starMax)
		{
			return TreasureStarStat.Waked;
		}
		if (star == starMax - 1)
		{
			return TreasureStarStat.Wake;
		}
		return TreasureStarStat.Upgrade;
	}
}

}
