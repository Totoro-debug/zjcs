namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
using System;

public class ProfessionComplexProp
{
	public PropType PropType { get; set; }

	public string PropName { get; set; } = string.Empty;

	public long CurValue { get; set; } = -1L;

	public long NextValue { get; set; } = -1L;

	public bool IsBless { get; set; }

	public ProfessionPropertyLanguage.IColorfulPropStr CurValueObj { get; set; }

	public ProfessionPropertyLanguage.IColorfulPropStr NextValueObj { get; set; }

	public bool IsPercentValueNegative { get; set; }

	public bool IsPercentValueIncreasing => NextValue > CurValue;

	public bool IsShowPreview
	{
		get
		{
			if (NextValueObj != null)
			{
				return string.Compare(NextValueObj.ToString(), CurValueObj.ToString()) != 0;
			}
			return false;
		}
	}

	public static ProfessionPropertyLanguage.IColorfulPropStr MakePropValueObj(PropType propType, long value)
	{
		return new ProfessionPropertyLanguage.PropValue(propType, Math.Abs(value), includeUnit: false, includePropDesc: false, -1L);
	}

	public static ProfessionPropertyLanguage.IColorfulPropStr MakePropValueObj(PropType propType, long value, long nextValue)
	{
		if (nextValue != -1 && nextValue != value)
		{
			return new ProfessionPropertyLanguage.PropValue(propType, Math.Abs(nextValue), includeUnit: false, includePropDesc: false, value);
		}
		return null;
	}

	public static ProfessionPropertyLanguage.IColorfulPropStr MakePropValueObj(string value)
	{
		return new ProfessionPropertyLanguage.PropValueOrig(value);
	}
}

}
