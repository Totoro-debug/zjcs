using System;
using System.Globalization;

namespace Zjcs.Offline.Progression
{
    /// <summary>
    /// Original <c>LanguageUtils.FormatPropString</c> and CN number formatter used by property rows.
    /// </summary>
    public static class OfficialPropFormat
    {
        private static readonly string[] Symbols = { string.Empty, "万", "亿", "兆", "京" };
        private const int DivideBase = 10000;

        public static string Value(OfficialPropDefinition definition, long value)
        {
            if (definition == null) return Compact(value);
            if (definition.Percent) return Percent(value);
            if (definition.Float) return Float(value / 10000.0);
            return Compact(value);
        }

        public static string Signed(OfficialPropDefinition definition, long value)
        {
            string text = Value(definition, value);
            return value > 0 ? "+" + text : text;
        }

        public static string Percent(long fixedPoint)
        {
            return Percent(fixedPoint / 10000.0);
        }

        public static string Percent(double ratio)
        {
            double value = ratio * 100.0;
            if (value == 0d) return "0%";
            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            int decimals = Math.Max(0, 2 - exponent);
            double factor = Math.Pow(10d, decimals);
            double truncated = Math.Truncate(value * factor) / factor;
            return truncated.ToString("0.###", CultureInfo.InvariantCulture) + "%";
        }

        public static string Float(double value)
        {
            return value.ToString(Math.Abs(value) >= 0.1 ? "#0.##" : "#0.###", CultureInfo.InvariantCulture);
        }

        public static string Compact(long value)
        {
            var (number, index) = Divide(value);
            return TruncateSignificant(number) + Symbols[index];
        }

        private static (double number, int index) Divide(long value)
        {
            double number = value;
            int index = 0;
            while (number >= DivideBase && index < Symbols.Length - 1)
            {
                index++;
                number /= DivideBase;
            }
            return (number, index);
        }

        private static string TruncateSignificant(double value)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);
            int point = text.IndexOf('.');
            if (point < 0) return text;
            text = point >= 3 ? text.Substring(0, point) : text.Substring(0, Math.Min(4, text.Length));
            return text;
        }
    }
}
