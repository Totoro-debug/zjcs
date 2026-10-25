using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // PlayerCombatRatingText.Refresh, NumberWithSizeArg and the original CN
    // CultureNumberFormatterBase / NumberFormatHelper number-and-unit path.
    public sealed class WorldPlayerCombatRatingText : MonoBehaviour
    {
        public TMP_Text Text;
        [SerializeField] private int mNumberSize;
        private static readonly string[] Symbols = { "", "万", "亿", "兆", "京" };

        public void Bind(TMP_Text text, int numberSize)
        {
            Text = text;
            mNumberSize = numberSize;
        }

        public void Refresh(long combatRating)
        {
            double number = combatRating;
            int unit = 0;
            while (number >= 10000 && unit < Symbols.Length - 1)
            {
                number /= 10000;
                unit++;
            }
            string formatted = number.ToString(CultureInfo.InvariantCulture);
            int point = formatted.IndexOf('.');
            if (point >= 0)
            {
                formatted = point >= 3 ? formatted.Substring(0, point)
                    : formatted.Substring(0, Math.Min(4, formatted.Length));
                formatted = double.Parse(formatted, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }
            Text.text = (mNumberSize > 0 ? "<size=" + mNumberSize + ">" + formatted + "</size>" : formatted)
                + Symbols[unit];
        }
    }
}
