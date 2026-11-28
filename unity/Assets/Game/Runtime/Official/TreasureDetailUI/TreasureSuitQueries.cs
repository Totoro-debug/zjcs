// Selected original suit UI; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using PropType = System.Int32;
using Object = UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {
public static class TreasureSuitQueries {
public static void GetSuitStartInfo(TreasureSuitInfo info, ITreasurePropertyPlayer player, Dictionary<int, int> result)
	{
		result.Clear();
		foreach (int treasure in info.Treasures)
		{
			Zjcs.Official.TreasureCultivation.TreasureState itemById = player.GetTreasureState(treasure);
			if (itemById != null)
			{
				Zjcs.Official.TreasureCultivation.TreasureState param = itemById;
				if (param != null && param.Active)
				{
					result[treasure] = param.Star;
				}
			}
		}
	}
}

}
