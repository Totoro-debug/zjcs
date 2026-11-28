using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.HomePlayerInfoUI;
using PlayerCostMoneyContent=Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent;
using IntroductionPopBtn=Zjcs.Official.HomePlayerInfoUI.HomeIntroductionPopBtn;
using TreasureState=Zjcs.Official.TreasureCultivation.TreasureState;
using PropType=System.Int32;
using SubRank=System.Int32;
using Rank=System.Int32;
using Object=UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

public class TreasureNpcEffectItem : UIBase
{
    private ITreasureNpcEffectServices Services;
    public void Configure(ITreasureNpcEffectServices services) => Services=services??throw new ArgumentNullException(nameof(services));

	[SerializeField]
	private ImageProxy mNpcBgIcon;

	[SerializeField]
	private ImageProxy mNpcIcon;

	[SerializeField]
	private WorldMultiLanguageText mFixedPropValueText2;

	private int mNpcId;

	public void Init(int npcId)
	{
		InitWrapper(() =>
		{
			mNpcId = npcId;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		mNpcIcon.Clear();
		mNpcBgIcon.Clear();
		base.OnDisableAndInited();
	}

	private void Refresh()
	{
		TreasureNpcViewInfo npcInfo = Services.GetNpc(mNpcId);
		if (npcInfo != null)
		{
			mNpcIcon.Load(npcInfo.Icon);
			mNpcBgIcon.Load(npcInfo.PartnerAdditionBg);
			object[] args = new object[2]
			{
				new CultureTextArg(npcInfo.NameKey),
				GetTreasureToNpcFriendshipPercent(npcInfo.TreasureClassId)
			};
			mFixedPropValueText2.SetKey("treasure_func_npc_desc", args);
		}
	}

	public string GetTreasureToNpcFriendshipPercent(int treasureClassId)
	{
		double value = Math.Round(Services.GetFriendshipPercent(treasureClassId), 4);
		return ProfessionPropertyFormula.ToStandardSignificantPercent(value,Services.Language);
	}
}

}
