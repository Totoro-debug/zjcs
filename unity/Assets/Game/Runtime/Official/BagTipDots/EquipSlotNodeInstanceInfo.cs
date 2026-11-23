// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{

public class EquipSlotNodeInstanceInfo : BaseNodeInstanceInfo
{
	public EquipPos EquipPos;

	public EquipSlotNodeInstanceInfo(EquipPos equipPos)
	{
		EquipPos = equipPos;
	}



	public override bool Equals(object obj)
	{
		if (obj is EquipSlotNodeInstanceInfo equipSlotNodeInstanceInfo)
		{
			return EquipPos == equipSlotNodeInstanceInfo.EquipPos;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<EquipPos>(EquipPos);
	}
}

}
