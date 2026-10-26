// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Collections.Generic;
using System.Globalization;


internal abstract class CultureNumberFormatterBase : ICultureNumberFormatter
{
	private const int kSignificantDigits = 3;

	public abstract LanguageType LanguageType { get; }

	public abstract IReadOnlyList<string> NumberSymbols { get; }

	public abstract IReadOnlyList<string> BattleNumberSymbols { get; }

	public abstract int DivideBase { get; }

	public abstract char DecimalSeparator { get; }

	public abstract string PercentSign { get; }

	protected virtual bool UseRomanNumerals => false;

	public virtual string FormatCompact(long value, int significantDigits = 3)
	{
		var (number, symbolIndex) = Abbreviate(value, significantDigits);
		return NumberFormatHelper.ComposeFormattedNumber(number, symbolIndex, NumberSymbols, DecimalSeparator);
	}

	public virtual string FormatWithDecimalPlaces(long value, int decimalPlaces)
	{
		var (number, symbolIndex) = AbbreviateWithDecimalPlaces(value, decimalPlaces);
		return NumberFormatHelper.ComposeFormattedNumber(number, symbolIndex, NumberSymbols, DecimalSeparator);
	}

	public virtual (string numberStr, string unitStr) GetNumberAndUnit(long value)
	{
		var (num, index) = Abbreviate(value);
		return (numberStr: NumberFormatHelper.ApplyDecimalSeparator(num.ToString(CultureInfo.InvariantCulture), DecimalSeparator), unitStr: NumberSymbols[index]);
	}

	public virtual string FormatUpCompare(long nextValue, long currentValue)
	{
		return NumberFormatHelper.FormatUpCompare(nextValue, currentValue, DivideBase, NumberSymbols, DecimalSeparator, 3);
	}

	public virtual (double number, int symbolIndex) Abbreviate(long value, int significantDigits = 3)
	{
		var (value2, item) = NumberFormatHelper.Divide(value, DivideBase, NumberSymbols.Count);
		return (number: NumberFormatHelper.TruncateBySignificantDigits(value2, significantDigits), symbolIndex: item);
	}

	public virtual (double number, int symbolIndex) AbbreviateWithDecimalPlaces(long value, int decimalPlaces)
	{
		var (value2, item) = NumberFormatHelper.Divide(value, DivideBase, NumberSymbols.Count);
		return (number: NumberFormatHelper.TruncateByDecimalPlaces(value2, decimalPlaces), symbolIndex: item);
	}

	public abstract string FormatBattle(long value);

	public virtual string FormatStandardSignificantDecimals(double value)
	{
		return NumberFormatHelper.ApplyDecimalSeparator(value.ToString($"G{3}", CultureInfo.InvariantCulture), DecimalSeparator);
	}

	public virtual string FormatStandardSignificantPercent(double value)
	{
		return NumberFormatHelper.FormatStandardSignificantPercent(value, PercentSign, DecimalSeparator, 3);
	}

	public virtual string FormatSpecifySignificantPercent(double value, int decimalPlaces)
	{
		return NumberFormatHelper.FormatSpecifySignificantPercent(value, decimalPlaces, PercentSign, DecimalSeparator);
	}

	public virtual string FormatGachaFixedDecimalPlacesPercent(double value)
	{
		return FormatSpecifySignificantPercent(value, 4);
	}

	public virtual string FormatRoman(long value)
	{
		if (!UseRomanNumerals)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}
		return NumberFormatHelper.ToRoman(value);
	}

	public virtual string NormalizeDecimalSeparator(string s)
	{
		return NumberFormatHelper.ApplyDecimalSeparator(s, DecimalSeparator);
	}
}

}
