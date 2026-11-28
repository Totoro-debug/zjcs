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
public static class TreasureSuitLanguage {
public static string GetItemNameKey(int id)
	{
		return $"item_{id}_name";
	}
public static string GetEnumKey(object obj)
	{
		return $"{obj.GetType().Name}.{obj}";
	}
}

}
