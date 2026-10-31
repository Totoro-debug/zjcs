// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
public class TipDotNpcTalentTreeGiftCanActivePoint : TipDotConditionNodeBase, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	private NpcNodeInstanceInfo InstanceInfo => NodeKey.InstanceInfo as NpcNodeInstanceInfo;

	private PartnerTipDotTalentGraph mGraph => Services.GetTalentGraph(InstanceInfo.NpcId);

	public override void Init()
	{
		base.Init();
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		if (mGraph != null)
		{
			foreach (int node in mGraph.Nodes)
			{
				PartnerTipDotTalentNode nodeInfo = mGraph.GetNodeInfo(node);
				Services.RegisterCondition(nodeInfo.Condition, OnTalentPointConditionUpdate);
			}
		}
		if (TryGetTalentTreeUnlockCondition(out var condition) && IsTalentTreeFunctionLimit())
		{
			Services.RegisterCondition(condition, OnTalentTreeFunctionLimitConditionUpdate);
		}
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		if (mGraph != null)
		{
			foreach (int node in mGraph.Nodes)
			{
				PartnerTipDotTalentNode nodeInfo = mGraph.GetNodeInfo(node);
				Services.DeregisterCondition(nodeInfo.Condition, OnTalentPointConditionUpdate);
			}
		}
		if (TryGetTalentTreeUnlockCondition(out var condition) && IsTalentTreeFunctionLimit())
		{
			Services.DeregisterCondition(condition, OnTalentTreeFunctionLimitConditionUpdate);
		}
		base.Release();
	}

	private void MainPlayer_OnItemCountUpdate(PartnerTipDotItem itemData, long count, object sourceData)
	{
		if (itemData != null && itemData.Info.ItemType == ItemType.NpcTalentTreePoint)
		{
			SetDirty();
		}
	}

	private void OnTalentPointConditionUpdate()
	{
		SetDirty();
	}

	private void OnTalentTreeFunctionLimitConditionUpdate()
	{
		SetDirty();
	}

	public override bool CheckShowTip()
	{
		int npcId = InstanceInfo.NpcId;
		if (IsTalentTreeFunctionLimit() || mGraph == null)
		{
			return false;
		}
		IReadOnlyCollection<int> npcTalentTreeUnlockedNodeIds = Services.Player.GetNpcTalentTreeUnlockedNodeIds(npcId);
		foreach (int node in mGraph.Nodes)
		{
			if (Services.GetTalentPointState(mGraph.GetNodeInfo(node), npcTalentTreeUnlockedNodeIds) == PartnerTipDotTalentPointState.CanActive)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsTalentTreeFunctionLimit()
	{
		if (Services.Player == null)
		{
			return true;
		}
		return Services.IsTalentTreeFunctionLimit(Services.Player, InstanceInfo.NpcId);
	}

	private bool TryGetTalentTreeUnlockCondition(out PartnerTipDotCondition condition)
	{
		condition = null;
		PartnerTipDotTalentItem npcTalentItemInfo = Services.GetTalentItem(InstanceInfo.NpcId);
		if (npcTalentItemInfo == null)
		{
			return false;
		}
		condition = npcTalentItemInfo.TalentTreeUnlockCondition;
		return condition != null;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotNpcTalentTreeGiftCanActivePoint tipDotNpcTalentTreeGiftCanActivePoint = new TipDotNpcTalentTreeGiftCanActivePoint();
		tipDotNpcTalentTreeGiftCanActivePoint.Fill(this);
		return tipDotNpcTalentTreeGiftCanActivePoint;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotNpcTalentTreeGiftCanActivePoint tipDotNpcTalentTreeGiftCanActivePoint = source as TipDotNpcTalentTreeGiftCanActivePoint;
		if (tipDotNpcTalentTreeGiftCanActivePoint == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotNpcTalentTreeGiftCanActivePoint);
	}

	public void Fill(TipDotNpcTalentTreeGiftCanActivePoint obj)
	{
	}
}

}
