namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
public static class ProfessionBlessDescription
{
private static IProfessionPropertyBindings Bindings => ProfessionPropertyBindings.Required;
	public static string GetBlessDesc(string rawValueDesc, string blessValueDesc, bool hasBless = false, bool isEffect = true)
	{
		if (hasBless)
		{
			if (isEffect)
			{
				return Bindings.TextFormatted("ui_equip_attribuate_kNormalWithBlessKey", rawValueDesc, blessValueDesc);
			}
			return Bindings.TextFormatted("ui_equip_attribuate_kUnEffectKey", rawValueDesc, blessValueDesc);
		}
		return Bindings.TextFormatted("{0}", rawValueDesc);
	}
}
}
