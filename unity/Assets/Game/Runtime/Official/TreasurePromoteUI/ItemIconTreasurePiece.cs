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

public class ItemIconTreasurePiece : ItemIcon
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	[SerializeField]
	private TreasureCanMergeRedDotItem mTreasurePieceRed;

	public override void Clear()
	{
		base.Clear();
		((Component)mProgress).gameObject.SetActive(false);
	}

	public ItemIconTreasurePiece SetRedDot()
	{
		mTreasurePieceRed.Init(myInfo.ClassId);
		((Component)mTreasurePieceRed).gameObject.SetActive(true);
		return this;
	}

	public override void SetAgiotage(long agiotage)
	{
		SetAttachment(ItemIconAttachmentType.PieceTip, agiotage <= 0);
	}

	public ItemIconTreasurePiece SetProgress()
	{
		SetRequireCount(GetUpgradePieceCount(myInfo.ClassId));
		SetAttachment(ItemIconAttachmentType.PieceTip, show: false);
		return this;
	}

	private long GetUpgradePieceCount(int pieceClassId)=>Services.GetUpgradePieceCount(pieceClassId);

	protected override bool SetCenterHintUIText()
	{
		if (Services.PieceCanMerge(myInfo.ClassId))
		{
			SetCenterHintText("ui_ItemIcon_centerHint_canMerge");
			return true;
		}
		return base.SetCenterHintUIText();
	}

	protected override void OnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		base.OnItemCountUpdate(itemData, preCount, sourceData);
		RefreshCenterHintUI();
	}
}

}
