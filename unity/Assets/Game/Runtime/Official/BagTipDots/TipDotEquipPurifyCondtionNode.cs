// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{
[Serializable]
public class TipDotEquipPurifyCondtionNode : TipDotConditionNodeBase, IBagTipDotNode
{
	public IBagTipDotServices Services { get; set; }

	public List<EquipPos> EquipPosList;

	public bool IsBackPack;

	public override void Init()
	{
		base.Init();
		if (NodeKey.InstanceInfo is EquipSlotNodeInstanceInfo equipSlotNodeInstanceInfo)
		{
			EquipPosList = new List<EquipPos> { equipSlotNodeInstanceInfo.EquipPos };
		}
		AddEventListener();
	}

	public override void Release()
	{
		RemoveEventListener();
		base.Release();
	}

	private void AddEventListener()
	{
		Services.Player.OnEquipUpdate += MainPlayer_OnEquipUpdate;
		Services.Player.OnSplitItem += MainPlayer_OnSplitItem;
		Services.Player.OnEquipAttribuatePurifyStateChange += MainPlayer_OnEquipPurifyStateChange;
	}

	private void RemoveEventListener()
	{
		if (Services.Player != null)
		{
			Services.Player.OnEquipUpdate -= MainPlayer_OnEquipUpdate;
			Services.Player.OnSplitItem -= MainPlayer_OnSplitItem;
			Services.Player.OnEquipAttribuatePurifyStateChange -= MainPlayer_OnEquipPurifyStateChange;
		}
	}

	private void MainPlayer_OnEquipPurifyStateChange(ulong toItemIndex)
	{
		SetDirty();
	}

	private void MainPlayer_OnSplitItem(IDictionary<int, long> _)
	{
		SetDirty();
	}

	private void MainPlayer_OnEquipUpdate(EquipPos pos, ulong index, ulong toItemIndex)
	{
		if (EquipPosList.Contains(pos) || IsBackPack)
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		IBagTipDotPlayer mainPlayer = Services.Player;
		if (Services.Player.IsFunctionLimit(FunctionType.EquipPurify))
		{
			return false;
		}
		if (IsBackPack)
		{
			foreach (BagTipItem item in mainPlayer.FindItemsByType(Services.Settings.EquipTypes))
			{
				if (!mainPlayer.IsEquipWorn(item.ItemIndex) && mainPlayer.IsEquipInPurify(item.ItemIndex))
				{
					return true;
				}
			}
		}
		else
		{
			foreach (EquipPos equipPos in EquipPosList)
			{
				if (Services.Player.TryGetEquip(equipPos, out var itemData) && mainPlayer.IsEquipInPurify(itemData.ItemIndex))
				{
					return true;
				}
			}
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotEquipPurifyCondtionNode tipDotEquipPurifyCondtionNode = new TipDotEquipPurifyCondtionNode();
		tipDotEquipPurifyCondtionNode.Fill(this);
		return tipDotEquipPurifyCondtionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotEquipPurifyCondtionNode tipDotEquipPurifyCondtionNode = source as TipDotEquipPurifyCondtionNode;
		if (tipDotEquipPurifyCondtionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotEquipPurifyCondtionNode);
	}

	public void Fill(TipDotEquipPurifyCondtionNode obj)
	{
		EquipPosList = obj.EquipPosList == null ? null : new List<EquipPos>(obj.EquipPosList);
		IsBackPack = obj.IsBackPack;
	}
}

}
