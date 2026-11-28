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
using System.Collections.Generic;



public class TreasureCanMergeRedDotItem : ProfessionRedDotItemBase
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	private int mItemId;

	private int mTreasureClassId;

	private Dictionary<int, long> mMergeMaterialDict;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Icons.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		Services.OnTreasureAdd += MainPlayer_OnTreasureAdd;
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (services != null)
		{
			Services.Icons.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
			Services.OnTreasureAdd -= MainPlayer_OnTreasureAdd;
		}
		mItemId = 0;
		mMergeMaterialDict?.Clear();
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnTreasureAdd(IProfessionItemData obj)
	{
		int treasureIdByPieceId = Services.GetTreasureIdByPieceId(mItemId);
		if (obj.Info.ClassId == treasureIdByPieceId)
		{
			Refresh();
		}
	}

	public void Init(int itemId) {
            mItemId=itemId;
            if(Services.GetItemType(itemId)!=306)UnityEngine.Debug.LogError("TreasureCanMergeRedDotItem 物品："+itemId+" 不是 TreasurePiece");
            else {mTreasureClassId=Services.GetTreasureIdByPieceId(itemId);mMergeMaterialDict=Services.GetMergeMaterials(mTreasureClassId);}
        }

	public override bool ShowRedDot()
	{
		return Services.CanMergeTreasure(mTreasureClassId) == 0;
	}

	protected void MainPlayer_OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		if (mMergeMaterialDict != null && mMergeMaterialDict.ContainsKey(itemData.Info.ClassId))
		{
			Refresh();
		}
	}
}

}
