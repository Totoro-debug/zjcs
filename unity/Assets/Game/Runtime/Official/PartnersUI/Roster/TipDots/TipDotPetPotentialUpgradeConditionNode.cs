// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;

public class TipDotPetPotentialUpgradeConditionNode : TipDotConditionNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public bool IsAllPet;

	private long mPetIndex;

	private PartnerPanelCondition mCondition;

	public override void Init()
	{
		base.Init();
		mCondition = Services.GetPetPotentialCondition();
		Services.RegisterCondition(mCondition, SetDirty);
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
		if (Services != null)
		{
			Services.DeregisterCondition(mCondition, SetDirty);
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
				if (mainPlayer.PetCanUpgradePotentialLevel(item.Index))
				{
					return true;
				}
			}
			return false;
		}
		return mainPlayer.PetCanUpgradePotentialLevel(mPetIndex);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotPetPotentialUpgradeConditionNode tipDotPetPotentialUpgradeConditionNode = new TipDotPetPotentialUpgradeConditionNode();
		tipDotPetPotentialUpgradeConditionNode.Fill(this);
		return tipDotPetPotentialUpgradeConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotPetPotentialUpgradeConditionNode tipDotPetPotentialUpgradeConditionNode = source as TipDotPetPotentialUpgradeConditionNode;
		if (tipDotPetPotentialUpgradeConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotPetPotentialUpgradeConditionNode);
	}

	public void Fill(TipDotPetPotentialUpgradeConditionNode obj)
	{
		IsAllPet = obj.IsAllPet;
	}
}

}
