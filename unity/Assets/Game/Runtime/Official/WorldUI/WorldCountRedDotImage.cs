using System.Globalization;
using UnityEngine;
namespace Zjcs.Official.WorldUI
{
    // client-source/CountRedDotImage.cs and Common.LanguageUtils.GetTipNumberWithPlus.
    public sealed class WorldCountRedDotImage : MonoBehaviour
    {
        public WorldMultiLanguageText mCountLanguage;
        public void SetCount(long count)
        {
            mCountLanguage.FontSize = count <= 10 ? 26f : 22f;
            var language = mCountLanguage.Localizer ?? WorldMultiLanguageText.Localize ?? WorldBattleReportTextService.GetText;
            string value = count > 99 ? string.Format(language("ui_common_count_plus"), 99L) : count.ToString(CultureInfo.CurrentCulture);
            mCountLanguage.SetKey("{0}", value);
        }
    }
}
