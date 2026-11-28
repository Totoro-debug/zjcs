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

public class ShareItemBtn : MonoBehaviour
{
    private ITreasureItemShareServices Services;
    public void Configure(ITreasureItemShareServices services)=>Services=services??throw new ArgumentNullException(nameof(services));

	public int SnapshotType;

	public Button ShareBtn;

	private IProfessionItemData mItemData;

	private void Awake()
	{
		if ((Object)(object)ShareBtn != (Object)null)
		{
			((UnityEvent)ShareBtn.onClick).AddListener((UnityAction)OnShareBtnClick);
		}
	}

	public void Init(IProfessionItemData itemData)
	{
		if (!((Object)(object)ShareBtn == (Object)null))
		{
			bool active = false;
			if (itemData!=null&&Services.IsLocalItem(itemData)&&Services.CanShare)
			{
				active = true;
			}
			mItemData = itemData;
			((Component)ShareBtn).gameObject.SetActive(active);
		}
	}

	private void OnShareBtnClick(){Services.ShareItem(mItemData,SnapshotType);}
}

}
