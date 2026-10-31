// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
public class TipDotNpcTalentTreeGiftCanTransToTalentPoint : TipDotConditionNodeBase, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	private NpcNodeInstanceInfo InstanceInfo => NodeKey.InstanceInfo as NpcNodeInstanceInfo;

	public override void Init()
	{
		base.Init();
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	private void MainPlayer_OnItemCountUpdate(PartnerTipDotItem itemData, long count, object sourceData)
	{
		if (itemData != null && itemData.Info.ItemType == ItemType.NpcGift)
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		int npcId = InstanceInfo.NpcId;
		IPartnerTipDotPlayer mainPlayer = Services.Player;
		if (Services.IsTransformFunctionLimit(mainPlayer, npcId))
		{
			return false;
		}
		return Services.IsGiftCanTransformTalentPoint(Services.Player, npcId);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotNpcTalentTreeGiftCanTransToTalentPoint tipDotNpcTalentTreeGiftCanTransToTalentPoint = new TipDotNpcTalentTreeGiftCanTransToTalentPoint();
		tipDotNpcTalentTreeGiftCanTransToTalentPoint.Fill(this);
		return tipDotNpcTalentTreeGiftCanTransToTalentPoint;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotNpcTalentTreeGiftCanTransToTalentPoint tipDotNpcTalentTreeGiftCanTransToTalentPoint = source as TipDotNpcTalentTreeGiftCanTransToTalentPoint;
		if (tipDotNpcTalentTreeGiftCanTransToTalentPoint == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotNpcTalentTreeGiftCanTransToTalentPoint);
	}

	public void Fill(TipDotNpcTalentTreeGiftCanTransToTalentPoint obj)
	{
	}
}

}
