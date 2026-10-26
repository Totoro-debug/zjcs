using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Official.WorldUI
{
    // Original NumberFormatHelper methods and CultureNumberFormatterBase/CN compact/percent path.
    public static class WorldBattleReportNumberFormat
    {
        private static readonly string[] Symbols = { "", "万", "亿", "兆", "京" };
        public static string Compact(long value)
        {
            var (number, index) = Divide(value, 10000, Symbols.Length);
            return ComposeFormattedNumber(TruncateBySignificantDigits(number, 3), index, Symbols, '.');
        }
        public static string Significant(double value) => value.ToString("G3", CultureInfo.InvariantCulture);
        public static string Percent(double value) => FormatStandardSignificantPercent(value, "%", '.', 3);
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
    }
}
