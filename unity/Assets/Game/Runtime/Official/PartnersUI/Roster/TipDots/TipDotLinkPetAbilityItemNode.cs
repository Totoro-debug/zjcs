// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;

public class TipDotLinkPetAbilityItemNode : TipDotDynamicLinkNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	private long mPetIndex;

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is PetNodeInstanceInfo petNodeInstanceInfo)
		{
			mPetIndex = petNodeInstanceInfo.PetIndex;
		}
		InitLinkNode();
	}

	public override void InitLinkNode()
	{
		if (Services.Player == null || !Services.Player.PetManager.TryGetValue(mPetIndex, out var obj) || obj == null)
		{
			return;
		}
		PartnerPanelPetInfo petInfo = Services.GetPet(obj.ClassId);
		Dictionary<int, PartnerPanelPetAbilityInfo> dictionary = Services.GetPetAbilities(petInfo.AbilityId);
		_ = Services.PetAbilityPropInfoParser;
		List<TipDotNodeKey> list = new List<TipDotNodeKey>();
		List<int> list2 = new List<int>();
		foreach (PartnerPanelPetAbilityInfo value in dictionary.Values)
		{
			if (!list2.Contains(value.PageCode))
			{
				list2.Add(value.PageCode);
				TipDotNodeKey item = new TipDotNodeKey(LinkNodeKey.GraphName, LinkNodeKey.NodeName, new PetAbilityNodeInstanceInfo(mPetIndex, value.PageCode) { Services = Services });
				list.Add(item);
			}
		}
		((IPartnerDynamicTipDotNodeManager)NodeManager).ConnectNodes(NodeKey, list);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotLinkPetAbilityItemNode tipDotLinkPetAbilityItemNode = new TipDotLinkPetAbilityItemNode();
		tipDotLinkPetAbilityItemNode.Fill(this);
		return tipDotLinkPetAbilityItemNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipDotLinkPetAbilityItemNode tipDotLinkPetAbilityItemNode = source as TipDotLinkPetAbilityItemNode;
		if (tipDotLinkPetAbilityItemNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotLinkPetAbilityItemNode);
	}

	public void Fill(TipDotLinkPetAbilityItemNode obj)
	{
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
