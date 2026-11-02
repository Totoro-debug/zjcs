// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;

public class TipDotLinkNpcNode : TipDotDynamicLinkNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public List<NpcRoleType> NpcRoleTypes;

	public override void Init()
	{
		base.Init();
		InitLinkNode();
	}

	public override void InitLinkNode()
	{
		if (Services.Player == null)
		{
			return;
		}
		IEnumerable<PartnerPanelNpcInfo> npcs = Services.MapExploreNpcs();
		List<TipDotNodeKey> list = new List<TipDotNodeKey>();
		foreach (PartnerPanelNpcInfo item2 in npcs)
		{
			if (NpcRoleTypes.Contains(item2.NpcRoleType))
			{
				TipDotNodeKey item = new TipDotNodeKey(LinkNodeKey.GraphName, LinkNodeKey.NodeName, new NpcNodeInstanceInfo(item2.Id));
				list.Add(item);
			}
		}
		((IPartnerDynamicTipDotNodeManager)NodeManager).ConnectNodes(NodeKey, list);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotLinkNpcNode tipDotLinkNpcNode = new TipDotLinkNpcNode();
		tipDotLinkNpcNode.Fill(this);
		return tipDotLinkNpcNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipDotLinkNpcNode tipDotLinkNpcNode = source as TipDotLinkNpcNode;
		if (tipDotLinkNpcNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotLinkNpcNode);
	}

	public void Fill(TipDotLinkNpcNode obj)
	{
		NpcRoleTypes = obj.NpcRoleTypes == null ? null : new List<NpcRoleType>(obj.NpcRoleTypes);
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
