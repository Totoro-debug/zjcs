// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;

public class TipDotLinkPetSkillItemNode : TipDotDynamicLinkNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public PetEvolutionPhase Phase;

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
		if (Services.Player == null)
		{
			return;
		}
		PartnerPanelPetRecord playerPetDataWrap = Services.Player.PetManager[mPetIndex];
		if (playerPetDataWrap == null)
		{
			return;
		}
		List<TipDotNodeKey> list = new List<TipDotNodeKey>();
		PartnerPanelPetEvolutionInfo petEvolutionInfo = Services.GetPetEvolution(playerPetDataWrap.ClassId, Phase);
		if (petEvolutionInfo != null)
		{
			List<int> promotableSkills = petEvolutionInfo.PromotableSkills;
			switch (Phase)
			{
			case PetEvolutionPhase.Childhood:
				AddKey(petEvolutionInfo.PromotableSkills, promotableSkills, list);
				break;
			case PetEvolutionPhase.Adulthood:
				AddKey(petEvolutionInfo.PromotableSkills, promotableSkills, list);
				break;
			}
			((IPartnerDynamicTipDotNodeManager)NodeManager).ConnectNodes(NodeKey, list);
		}
	}

	private void AddKey(List<int> skillIds, List<int> promoteableSkils, List<TipDotNodeKey> myKeys)
	{
		if (skillIds == null)
		{
			return;
		}
		foreach (int skillId in skillIds)
		{
			AddKey(skillId, promoteableSkils, myKeys);
		}
	}

	private void AddKey(int skill, List<int> promoteableSkils, List<TipDotNodeKey> myKeys)
	{
		if (promoteableSkils.Contains(skill))
		{
			TipDotNodeKey item = new TipDotNodeKey(LinkNodeKey.GraphName, LinkNodeKey.NodeName, new PetSkillNodeInstanceInfo(mPetIndex, skill) { Services = Services });
			myKeys.Add(item);
		}
	}

	public override TipDotNodeBase Clone()
	{
		TipDotLinkPetSkillItemNode tipDotLinkPetSkillItemNode = new TipDotLinkPetSkillItemNode();
		tipDotLinkPetSkillItemNode.Fill(this);
		return tipDotLinkPetSkillItemNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipDotLinkPetSkillItemNode tipDotLinkPetSkillItemNode = source as TipDotLinkPetSkillItemNode;
		if (tipDotLinkPetSkillItemNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotLinkPetSkillItemNode);
	}

	public void Fill(TipDotLinkPetSkillItemNode obj)
	{
		Phase = obj.Phase;
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
