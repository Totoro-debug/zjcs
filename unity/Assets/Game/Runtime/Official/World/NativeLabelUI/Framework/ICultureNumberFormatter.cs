// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System.Collections.Generic;


public interface ICultureNumberFormatter
{
	LanguageType LanguageType { get; }

	IReadOnlyList<string> NumberSymbols { get; }

	IReadOnlyList<string> BattleNumberSymbols { get; }

	int DivideBase { get; }

	char DecimalSeparator { get; }

	string PercentSign { get; }

	string FormatCompact(long value, int significantDigits = 3);

	string FormatWithDecimalPlaces(long value, int decimalPlaces);

	(string numberStr, string unitStr) GetNumberAndUnit(long value);

	string FormatUpCompare(long nextValue, long currentValue);

	(double number, int symbolIndex) Abbreviate(long value, int significantDigits = 3);

	(double number, int symbolIndex) AbbreviateWithDecimalPlaces(long value, int decimalPlaces);

	string FormatBattle(long value);

	string FormatStandardSignificantDecimals(double value);

	string FormatStandardSignificantPercent(double value);

	string FormatSpecifySignificantPercent(double value, int decimalPlaces);

	string FormatGachaFixedDecimalPlacesPercent(double value);

	string FormatRoman(long value);

	string NormalizeDecimalSeparator(string s);
}

}
