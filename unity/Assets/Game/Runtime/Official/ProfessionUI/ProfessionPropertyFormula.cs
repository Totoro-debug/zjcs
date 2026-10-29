namespace Zjcs.Official.ProfessionUI
{
using System;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Object = UnityEngine.Object;
public static class ProfessionPropertyFormula
{
	public static double Long2Double(long val)
	{
		return (double)val / 10000.0;
	}

	public static string DoubleToString(double value)
	{
		if (Math.Abs(value) >= 0.1)
		{
			return value.ToString("#0.##");
		}
		return value.ToString("#0.###");
	}

	private static ICultureNumberFormatter ResolveNumberFormatter(LanguageType languageType)
	{
		return LanguageCultureResolver.Resolve(languageType).Number;
	}

	public static string ToUpNumberString(long nextValue, long currentValue, LanguageType languageType)
	{
		return ResolveNumberFormatter(languageType).FormatUpCompare(nextValue, currentValue);
	}

	public static string NumberToString(long val, LanguageType languageType, int vaildNumCount = 3)
	{
		return ResolveNumberFormatter(languageType).FormatCompact(val, vaildNumCount);
	}

	public static string ToStandardSignificantPercent(double value, LanguageType language)
	{
		return ResolveNumberFormatter(language).FormatStandardSignificantPercent(value);
	}
}
}
