// Selected original Home TipDot condition body; explicit local ports.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeTipDots
{

public class TreasureElementNodeInstanceInfo : BaseNodeInstanceInfo
{
	public ElementType ElementType;

	public TreasureElementNodeInstanceInfo(ElementType elementType)
	{
		ElementType = elementType;
	}

	public override string GetInstanceDesc()
	{
		return $"{ElementType}";
	}

	public override bool Equals(object obj)
	{
		if (obj is TreasureElementNodeInstanceInfo treasureElementNodeInstanceInfo)
		{
			return ElementType == treasureElementNodeInstanceInfo.ElementType;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<ElementType>(ElementType);
	}
}

}
