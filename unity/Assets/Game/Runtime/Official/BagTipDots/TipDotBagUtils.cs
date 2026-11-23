// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{

public static class TipDotBagUtils
{
	public static bool IsExcludedFromShowCanUseRedDot(IBagTipDotSettings settings, ItemType itemType)
	{
		if (settings.EquipTypes.Contains(itemType))
		{
			return false;
		}
		return !settings.ShowCanUseRedDotItemTypes.Contains(itemType);
	}
}

}
