namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
public static class ProfessionPropertyLanguage
{
private static IProfessionPropertyBindings Bindings => ProfessionPropertyBindings.Required;
	public interface IColorfulPropStr
	{
		string ToString(Color? mainColor, Color? extraColor, LanguageType? language);

		(bool, bool) IsPropSameWith(IColorfulPropStr prop);
	}

	public class PropValue : IColorfulPropStr
	{
		public readonly PropType PropType;

		public readonly long PropVal;

		public readonly bool IncludeUnit;

		public readonly bool IncludePropDesc;

		public readonly long targetValue;

		public readonly bool isPercentProp;

		public readonly int decimalPlaces;

		private bool mNeedSpaceBeforeValue;

		public PropValue(PropType propType, long val, bool includeUnit = false, bool includePropDesc = false, long targetValue = -1L, bool isPercentProp = false, int decimalPlaces = -1)
		{
			PropType = propType;
			PropVal = val;
			IncludeUnit = includeUnit;
			IncludePropDesc = includePropDesc;
			this.targetValue = targetValue;
			this.isPercentProp = isPercentProp;
			this.decimalPlaces = decimalPlaces;
		}

		public PropValue SetNeedSpaceBeforeValue(bool needSpaceBeforeValue)
		{
			mNeedSpaceBeforeValue = needSpaceBeforeValue;
			return this;
		}

		public override string ToString()
		{
			return ToString(null);
		}

		public string ToString(Color? mainColor, Color? extraColor = null, LanguageType? language = null)
		{
			return FormatPropString(PropType, PropVal, IncludeUnit, IncludePropDesc, targetValue, isPercentProp, mainColor, decimalPlaces, language, mNeedSpaceBeforeValue);
		}

		public (bool, bool) IsPropSameWith(IColorfulPropStr prop)
		{
			if (prop is PropValue propValue)
			{
				bool flag = PropVal.ToString() == propValue.PropVal.ToString();
				return (flag, flag);
			}
			return (false, false);
		}

		public static PropValue operator -(PropValue v1, PropValue v2)
		{
			if (v2 == null)
			{
				return v1;
			}
			long num = v1.PropVal - v2.PropVal;
			if (num == 0L)
			{
				return null;
			}
			return new PropValue(v1.PropType, num, includeUnit: false, includePropDesc: false, -1L);
		}
	}

	public class PropValueOrig : IColorfulPropStr
	{
		private readonly string mValue;

		public PropValueOrig(string value)
		{
			mValue = value;
		}

		public override string ToString()
		{
			return ToString(null);
		}

		public string ToString(Color? mainColor, Color? extraColor = null, LanguageType? language = null)
		{
			if (mainColor.HasValue)
			{
				return SetTextColorFormatStr(mValue, mainColor.Value);
			}
			return mValue?.ToString();
		}

		public (bool, bool) IsPropSameWith(IColorfulPropStr prop)
		{
			if (prop is PropValueOrig propValueOrig)
			{
				bool flag = mValue.ToString() == propValueOrig.mValue.ToString();
				return (flag, flag);
			}
			return (false, false);
		}
	}

	public static string SetTextColorFormatStr(object text, Color color)
	{
		return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
	}

	public static string FormatPropString(PropType propType, long val, bool includeUnit = false, bool includePropDesc = false, long targetValue = -1L, bool isPercentProp = false, Color? textColor = null, int decimalPlaces = -1, LanguageType? languageType = null, bool needSpaceBeforeValue = false)
	{
		ProfessionPropertyDefinition propCfgInfo = Bindings.GetProperty(propType);
		LanguageType languageType2 = (languageType.HasValue ? languageType.Value : Bindings.Language);
		string text = "";
		string text2 = "";
		if (includeUnit)
		{
			text = Bindings.Text(Bindings.GetProperty(propType)?.Unit);
		}
		if (includePropDesc)
		{
			string text3 = "";
			if (needSpaceBeforeValue)
			{
				text3 = Bindings.TextFormatted("ui_space_before_value", "");
			}
			text2 = Bindings.Text(ProfessionPropertyConfig.GetPropTypeName(propType)) + text3 + "+";
		}
		string text4;
		if (propCfgInfo.Percent | isPercentProp)
		{
			text4 = ProfessionPropertyFormula.ToStandardSignificantPercent(ProfessionPropertyFormula.Long2Double(Math.Abs(val)), languageType2);
		}
		else if (propCfgInfo.Float)
		{
			text4 = ProfessionPropertyFormula.DoubleToString(ProfessionPropertyFormula.Long2Double(Math.Abs(val)));
		}
		else if (targetValue != -1)
		{
			text4 = ProfessionPropertyFormula.ToUpNumberString(Math.Abs(val), targetValue, Bindings.Language);
		}
		else
		{
			text4 = ((decimalPlaces > 0) ? ProfessionPropertyFormula.NumberToString(Math.Abs(val), Bindings.Language, decimalPlaces) : ProfessionPropertyFormula.NumberToString(Math.Abs(val), Bindings.Language));
		}
		if (textColor.HasValue)
		{
			return SetTextColorFormatStr(text2 + text4 + text, textColor.Value);
		}
		return text2 + text4 + text;
	}

	public static string GetEntryNameKey(int entryId)
	{
		return $"entry_{entryId}_name";
	}

	public static string GetEntryDescKey(int entryId)
	{
		return $"entry_{entryId}_desc";
	}
}
}
