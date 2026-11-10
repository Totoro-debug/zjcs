using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleStageStatusUI
{
    // Original FightStatusUIItem sprite binding and count visibility.
    public sealed class FightStatusUIItem : MonoBehaviour
    {
        public Image BgImg;
        public Image IconImg;
        public WorldMultiLanguageText CountText;

        public void Show(Sprite iconSprite) => IconImg.sprite = iconSprite;
        public void Show(Sprite iconSprite, Sprite bgSprite, int count)
        {
            IconImg.sprite = iconSprite;
            BgImg.sprite = bgSprite;
            UpdateCount(count);
        }
        public void UpdateCount(int count)
        {
            string text = count > 0 ? count.ToString() : string.Empty;
            bool visible = !string.IsNullOrEmpty(text);
            CountText.gameObject.SetActive(visible);
            if (visible) CountText.SetKey("{0}", text);
        }
    }
}
