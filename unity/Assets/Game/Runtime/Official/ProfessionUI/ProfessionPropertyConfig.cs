namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;

public static class ProfessionPropertyConfig
{
private static IProfessionPropertyBindings Bindings => ProfessionPropertyBindings.Required;
	public class PropItemValue : ProfessionPropertyLanguage.IColorfulPropStr
	{
		public ProfessionPropertyLanguage.PropValue PropValue;

		public ProfessionPropertyLanguage.PropValue ExtraPropValue;

		public long DeltaPropValue;

		public long DeltaExtraPropValue;

		public override string ToString()
		{
			return ToString(null, null, null);
		}

		public string ToString(Color? mainColor, Color? extraColor, LanguageType? language)
		{
			string text = null;
			string text2 = null;
			string text3 = null;
			if (PropValue != null)
			{
				text = (mainColor.HasValue ? ProfessionPropertyLanguage.SetTextColorFormatStr(PropValue, mainColor.Value) : PropValue.ToString());
				text2 = GetDeltaValueString(PropValue.PropType, DeltaPropValue);
			}
			string text4 = null;
			if (ExtraPropValue != null)
			{
				if (extraColor.HasValue)
				{
					text4 = ((text == null) ? ("+" + ProfessionPropertyLanguage.SetTextColorFormatStr(ExtraPropValue, extraColor.Value)) : ProfessionPropertyLanguage.SetTextColorFormatStr($"+{ExtraPropValue}", extraColor.Value));
				}
				else
				{
					text4 = $"+{ExtraPropValue}";
				}
				text3 = GetDeltaValueString(ExtraPropValue.PropType, DeltaExtraPropValue);
			}
			if (text != null && text4 != null)
			{
				return string.Format(text + text2 + text4 + text3);
			}
			if (text == null)
			{
				return string.Format(text4 + text3);
			}
			if (text4 == null)
			{
				return string.Format(text + text2);
			}
			return "";
		}

		public string ToSubString()
		{
			string text = null;
			string text2 = null;
			string text3 = null;
			string text4 = null;
			if (PropValue != null)
			{
				text = PropValue.ToString();
				text2 = GetDeltaValueString(PropValue.PropType, DeltaPropValue);
			}
			if (ExtraPropValue != null)
			{
				text4 = ((PropValue == null) ? $"{ExtraPropValue}" : $"+{ExtraPropValue}");
				text3 = GetDeltaValueString(ExtraPropValue.PropType, DeltaExtraPropValue);
			}
			if (text != null && text4 != null)
			{
				return string.Format(text + text2 + text4 + text3);
			}
			if (text == null)
			{
				return string.Format(text4 + text3);
			}
			if (text4 == null)
			{
				return string.Format(text + text2);
			}
			return "";
		}

		public static string GetDeltaValueString(PropType propType, long value)
		{
			if (value == 0L)
			{
				return string.Empty;
			}
			string text = ProfessionPropertyLanguage.FormatPropString(propType, value, includeUnit: false, includePropDesc: false, -1L);
			if (value <= 0)
			{
				return Bindings.TextFormatted("ui_prop_descrease_show", text);
			}
			return Bindings.TextFormatted("ui_prop_increase_show", text);
		}

		public (bool, bool) IsPropSameWith(ProfessionPropertyLanguage.IColorfulPropStr prop)
		{
			if (prop is PropItemValue propItemValue)
			{
				return (PropValue.IsPropSameWith(propItemValue.PropValue).Item1, PropValue.IsPropSameWith(propItemValue.ExtraPropValue).Item1);
			}
			return (false, false);
		}

		public bool IsEmpty()
		{
			if (PropValue == null)
			{
				return ExtraPropValue == null;
			}
			return false;
		}

		public static PropItemValue operator -(PropItemValue v1, PropItemValue v2)
		{
			return new PropItemValue
			{
				PropValue = v1.PropValue - v2.PropValue,
				ExtraPropValue = v1.ExtraPropValue - v2.ExtraPropValue
			};
		}
	}

	public static string GetPropTypeName(PropType propType)
	{
		return $"{"PropType"}.{propType}";
	}

	public static string GetPropTypeName(PropType propType, int propId)
	{
		return $"{propId}.{propType}";
	}
}

}
