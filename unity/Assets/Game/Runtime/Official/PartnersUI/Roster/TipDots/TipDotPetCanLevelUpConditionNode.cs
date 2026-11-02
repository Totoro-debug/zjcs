// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;

public class TipDotPetCanLevelUpConditionNode : TipDotConditionNodeBase, IPartnerPanelTipDotNode
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public bool IsAllPet;

	private long mPetIndex;

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is PetNodeInstanceInfo petNodeInstanceInfo)
		{
			mPetIndex = petNodeInstanceInfo.PetIndex;
		}
		Services.Player.OnPetDataUpdate += Player_OnPetDataUpdate;
		Services.Player.OnGetPet += Player_OnGetPet;
		Services.Player.OnItemCountUpdate += Player_OnItemUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnPetDataUpdate -= Player_OnPetDataUpdate;
			Services.Player.OnGetPet -= Player_OnGetPet;
			Services.Player.OnItemCountUpdate -= Player_OnItemUpdate;
		}
		base.Release();
	}

	private void Player_OnPetDataUpdate(PartnerPanelPetRecord dataWrap)
	{
		if (dataWrap != null)
		{
			SetDirty();
		}
	}

	private void Player_OnGetPet(PartnerPanelPetRecord dataWrap)
	{
		if (dataWrap != null)
		{
			SetDirty();
		}
	}

	private void Player_OnItemUpdate(PartnerTipDotItem data, long amount, object sourceData)
	{
		if (data.Info.ItemType == ItemType.PetFood)
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		if (!IsAllPet && mPetIndex <= 0)
		{
			return false;
		}
		if (IsAllPet)
		{
			foreach (PartnerPanelPetRecord item in Services.Player.PetManager.GetAllPet())
			{
				if (CheckPetCanLevelUp(item))
				{
					return true;
				}
			}
			return false;
		}
		PartnerPanelPetRecord pet = Services.Player.PetManager[mPetIndex];
		return CheckPetCanLevelUp(pet);
	}

	private bool CheckPetCanLevelUp(PartnerPanelPetRecord pet)
	{
		if (pet == null)
		{
			return false;
		}
		if (!Services.TryCalcLevelUpExpRequire(pet, 1, out var expRequire))
		{
			return false;
		}
		IReadOnlyList<(int, long)> sortedFoods = Services.SortedFoods;
		int count = sortedFoods.Count;
		long num = 0L;
		for (int num2 = count - 1; num2 >= 0; num2--)
		{
			(int, long) tuple = sortedFoods[num2];
			int item = tuple.Item1;
			long item2 = tuple.Item2;
			long itemAmount = Services.Player.GetItemAmount(item);
			num += itemAmount * item2;
			if (num > expRequire)
			{
				return true;
			}
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotPetCanLevelUpConditionNode tipDotPetCanLevelUpConditionNode = new TipDotPetCanLevelUpConditionNode();
		tipDotPetCanLevelUpConditionNode.Fill(this);
		return tipDotPetCanLevelUpConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotPetCanLevelUpConditionNode tipDotPetCanLevelUpConditionNode = source as TipDotPetCanLevelUpConditionNode;
		if (tipDotPetCanLevelUpConditionNode == null)
		{
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotPetCanLevelUpConditionNode);
	}

	public void Fill(TipDotPetCanLevelUpConditionNode obj)
	{
		IsAllPet = obj.IsAllPet;
	}
}

}
