// Migrated from selected original language code; see Map13 language source receipt.
namespace Zjcs.Official.World.NativeLabelUI.Framework
{
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;


public static class NumberFormatHelper
{
	private const int kDefaultSignificantDigits = 3;

	private static readonly string[] sRomanNumberSymbols = new string[13]
	{
		"M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX",
		"V", "IV", "I"
	};

	private static readonly int[] sRomanNumberValues = new int[13]
	{
		1000, 900, 500, 400, 100, 90, 50, 40, 10, 9,
		5, 4, 1
	};

	public static (double number, int symbolIndex) Divide(long value, int divideBase, int symbolCount)
	{
		double num = value;
		int num2 = 0;
		while (num >= (double)divideBase && num2 < symbolCount - 1)
		{
			num2++;
			num /= (double)divideBase;
		}
		return (number: num, symbolIndex: num2);
	}

	public static double TruncateBySignificantDigits(double value, int significantDigits)
	{
		string text = value.ToString(CultureInfo.InvariantCulture);
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '.')
			{
				text = ((i >= significantDigits) ? text.Substring(0, i) : text.Substring(0, Math.Min(significantDigits + 1, text.Length)));
				break;
			}
		}
		return double.Parse(text, CultureInfo.InvariantCulture);
	}

	public static double TruncateByDecimalPlaces(double value, int decimalPlaces)
	{
		if (decimalPlaces < 0)
		{
			return TruncateBySignificantDigits(value, 3);
		}
		if (decimalPlaces == 0)
		{
			return Math.Floor(value);
		}
		string text = value.ToString(CultureInfo.InvariantCulture);
		int num = text.IndexOf('.');
		if (num >= 0)
		{
			text = text.Substring(0, Math.Min(num + 1 + decimalPlaces, text.Length));
			return double.Parse(text, CultureInfo.InvariantCulture);
		}
		return value;
	}

	public static string ApplyDecimalSeparator(string invariantStr, char decimalSeparator)
	{
		if (decimalSeparator == '.' || string.IsNullOrEmpty(invariantStr))
		{
			return invariantStr;
		}
		return invariantStr.Replace('.', decimalSeparator);
	}

	public static string ComposeFormattedNumber(double number, int symbolIndex, IReadOnlyList<string> symbols, char decimalSeparator)
	{
		return ApplyDecimalSeparator(number.ToString(CultureInfo.InvariantCulture), decimalSeparator) + symbols[symbolIndex];
	}

	public static string ToRoman(long value)
	{
		if (value < 1 || value > 3999)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}
		int num = (int)value;
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < sRomanNumberValues.Length; i++)
		{
			while (num >= sRomanNumberValues[i])
			{
				stringBuilder.Append(sRomanNumberSymbols[i]);
				num -= sRomanNumberValues[i];
			}
		}
		return stringBuilder.ToString();
	}

	public static string FormatBattleByThreeWidth(long value, IReadOnlyList<string> symbols)
	{
		double num = Math.Pow(10.0, 3.0);
		for (int i = 2; i < symbols.Count; i++)
		{
			double num2 = Math.Pow(num, i);
			if (!((double)Math.Abs(value) >= num2))
			{
				num2 = Math.Pow(num, i - 2);
				double num3 = 1.0 * (double)value / num2;
				if (Math.Floor(num3) >= num * num)
				{
					return $"{(int)(num3 / num):#0}{symbols[i - 1]}";
				}
				return $"{(int)num3:#0}{symbols[i - 2]}";
			}
		}
		return value.ToString(CultureInfo.InvariantCulture);
	}

	public static string FormatBattleByFourWidth(long value, IReadOnlyList<string> symbols)
	{
		double num = Math.Pow(10.0, 4.0);
		for (int i = 0; i < symbols.Count; i++)
		{
			double num2 = Math.Pow(num, i + 1) * 100.0;
			if (!((double)Math.Abs(value) >= num2))
			{
				num2 = Math.Pow(num, i);
				double num3 = 1.0 * (double)value / num2;
				if (Math.Floor(num3) >= num * 100.0)
				{
					return $"{(int)(num3 / num):#0}{symbols[i + 1]}";
				}
				if (num3 < 10.0)
				{
					return $"{(float)(int)(100.0 * num3) / 100f:#0.##}{symbols[i]}";
				}
				if (num3 < 100.0)
				{
					return $"{(float)(int)(10.0 * num3) / 10f:#0.#}{symbols[i]}";
				}
				return $"{(int)num3:#0}{symbols[i]}";
			}
		}
		return value.ToString(CultureInfo.InvariantCulture);
	}

	public static string FormatStandardSignificantPercent(double value, string percentSign, char decimalSeparator, int significantDigits)
	{
		List<char> list = value.ToString(CultureInfo.InvariantCulture).ToList();
		int num = list.IndexOf('.');
		if (num < 0)
		{
			num = list.Count;
			list.AddRange(".".PadRight(3, '0'));
		}
		int num2 = num + 2;
		if (list.Count <= num2)
		{
			int totalWidth = num2 - list.Count + 1;
			list.AddRange(string.Empty.PadRight(totalWidth, '0'));
		}
		char item = list[num];
		list.RemoveAt(num);
		list.Insert(num2, item);
		string text = new string(list.ToArray());
		text = text.TrimStart('0');
		if (text.Length <= 0 || text[0] == '.')
		{
			text = text.Insert(0, "0");
		}
		text = text.TrimEnd('.');
		int num3 = -1;
		int num4 = -1;
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '.')
			{
				num3 = i;
			}
			else if (text[i] != '0' && num4 == -1)
			{
				num4 = i;
			}
		}
		if (num3 == -1)
		{
			return text + percentSign;
		}
		string invariantStr;
		if (num4 >= num3)
		{
			invariantStr = double.Parse(text.Substring(0, Math.Min(num4 + significantDigits, text.Length)), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + percentSign;
		}
		else
		{
			invariantStr = ((num3 < significantDigits) ? (double.Parse(text.Substring(0, Math.Min(significantDigits + 1, text.Length)), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + percentSign) : (double.Parse(text.Substring(0, num3 + 2), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + percentSign));
		}
		return ApplyDecimalSeparator(invariantStr, decimalSeparator);
	}

	public static string FormatSpecifySignificantPercent(double value, int decimalPlaces, string percentSign, char decimalSeparator)
	{
		value *= 100.0;
		string text = ((decimalPlaces > 0) ? ("0." + new string('0', decimalPlaces)) : "0");
		return ApplyDecimalSeparator(value.ToString(text, CultureInfo.InvariantCulture) + percentSign, decimalSeparator);
	}

	public static string FormatUpCompare(long nextValue, long currentValue, int divideBase, IReadOnlyList<string> symbols, char decimalSeparator, int significantDigits)
	{
		(double number, int symbolIndex) tuple = Divide(nextValue, divideBase, symbols.Count);
		double item = tuple.number;
		int item2 = tuple.symbolIndex;
		(double number, int symbolIndex) tuple2 = Divide(currentValue, divideBase, symbols.Count);
		double item3 = tuple2.number;
		int item4 = tuple2.symbolIndex;
		double num = TruncateBySignificantDigits(item, significantDigits);
		if (nextValue <= currentValue || (int)item != (int)item3 || item2 != item4)
		{
			return ComposeFormattedNumber(num, item2, symbols, decimalSeparator);
		}
		double num2 = TruncateBySignificantDigits(item3, significantDigits);
		if (num != num2)
		{
			return ComposeFormattedNumber(num, item2, symbols, decimalSeparator);
		}
		string text = item.ToString(CultureInfo.InvariantCulture);
		string text2 = item3.ToString(CultureInfo.InvariantCulture);
		int length = text.Length;
		int length2 = text2.Length;
		int i;
		for (i = num.ToString(CultureInfo.InvariantCulture).Length; i < length && (text[i] == '.' || text[i] == '0' || (i < length2 && text2[i] == text[i])); i++)
		{
		}
		return ApplyDecimalSeparator(text.Substring(0, i + 1), decimalSeparator) + symbols[item2];
	}
}

}
