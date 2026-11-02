// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;

public class TipDotPetCanEvolutionConditionNode : TipDotConditionNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public bool IsAllPet;

	private long mPetIndex;

	public override void Init()
	{
		base.Init();
		if (!IsAllPet && NodeKey.InstanceInfo is PetNodeInstanceInfo petNodeInstanceInfo)
		{
			mPetIndex = petNodeInstanceInfo.PetIndex;
		}
		Services.Player.OnPetDataUpdate += Player_OnPetDataUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnPetDataUpdate -= Player_OnPetDataUpdate;
		}
		base.Release();
	}

	private void Player_OnPetDataUpdate(PartnerPanelPetRecord dataWrap)
	{
		if (dataWrap != null && (IsAllPet || dataWrap.Index == mPetIndex))
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		IPartnerPanelTipDotPlayer mainPlayer = Services.Player;
		if (IsAllPet)
		{
			foreach (PartnerPanelPetRecord item in mainPlayer.PetManager.GetAllPet())
			{
				if (mainPlayer.PetCanEvolution(item.Index))
				{
					return true;
				}
			}
			return false;
		}
		return mainPlayer.PetCanEvolution(mPetIndex);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotPetCanEvolutionConditionNode tipDotPetCanEvolutionConditionNode = new TipDotPetCanEvolutionConditionNode();
		tipDotPetCanEvolutionConditionNode.Fill(this);
		return tipDotPetCanEvolutionConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotPetCanEvolutionConditionNode tipDotPetCanEvolutionConditionNode = source as TipDotPetCanEvolutionConditionNode;
		if (tipDotPetCanEvolutionConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotPetCanEvolutionConditionNode);
	}

	public void Fill(TipDotPetCanEvolutionConditionNode obj)
	{
		IsAllPet = obj.IsAllPet;
	}
}

}
