// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;

public class TipDotLinkNpcProfileNode : TipDotDynamicLinkNodeBase, IPartnerTipDotNode
{
	public IPartnerTipDotServices Services { get; set; }

	private int NpcId;

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is NpcNodeInstanceInfo npcNodeInstanceInfo)
		{
			NpcId = npcNodeInstanceInfo.NpcId;
		}
		InitLinkNode();
	}

	public override void InitLinkNode()
	{
		if (Services.Player == null)
		{
			return;
		}
		List<PartnerTipDotProfile> npcProfileInfos = Services.GetNpcProfiles(NpcId);
		if (npcProfileInfos == null || npcProfileInfos.Count <= 0)
		{
			return;
		}
		List<TipDotNodeKey> list = new List<TipDotNodeKey>();
		foreach (PartnerTipDotProfile item2 in npcProfileInfos)
		{
			TipDotNodeKey item = new TipDotNodeKey(LinkNodeKey.GraphName, LinkNodeKey.NodeName, new NpcProfileNodeInstanceInfo(NpcId, item2.ProfileId));
			list.Add(item);
		}
		((IPartnerDynamicTipDotNodeManager)NodeManager).ConnectNodes(NodeKey, list);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotLinkNpcProfileNode tipDotLinkNpcProfileNode = new TipDotLinkNpcProfileNode();
		tipDotLinkNpcProfileNode.Fill(this);
		return tipDotLinkNpcProfileNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipDotLinkNpcProfileNode tipDotLinkNpcProfileNode = source as TipDotLinkNpcProfileNode;
		if (tipDotLinkNpcProfileNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotLinkNpcProfileNode);
	}

	public void Fill(TipDotLinkNpcProfileNode obj)
	{
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
