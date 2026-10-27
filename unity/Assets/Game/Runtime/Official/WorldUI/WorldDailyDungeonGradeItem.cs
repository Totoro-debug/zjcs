using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // DailyDungeonGradeItem.Init; localization is injected by the offline binding.
    public sealed class WorldDailyDungeonGradeItem : MonoBehaviour
    {
        public Image Image, FillImage;
        public WorldMultiLanguageText GradeText;
        public void Init(Color color, string key, bool showFillImg)
        {
            Image.color = color;
            FillImage.color = color;
            FillImage.gameObject.SetActive(showFillImg);
            GradeText.SetKey(key);
        }
    }
}
