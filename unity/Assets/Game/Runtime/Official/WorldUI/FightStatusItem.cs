using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // Original FightStatusItem.Show/UpdateCount. The local catalog supplies the
    // loaded sprites; the original "{0}" language binding formats the count only.
    public sealed class FightStatusItem : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer mBgSprite;
        [SerializeField] private SpriteRenderer mIconSprite;
        [SerializeField] private TMP_Text mStackCountText;

        public void Show(Sprite iconSprite, Sprite bgSprite, int count)
        {
            mIconSprite.sprite = iconSprite;
            mBgSprite.sprite = bgSprite;
            UpdateCount(count);
        }

        public void UpdateCount(int count)
        {
            string text = count > 0 ? count.ToString() : string.Empty;
            bool visible = !string.IsNullOrEmpty(text);
            mStackCountText.gameObject.SetActive(visible);
            if (visible) mStackCountText.text = text;
        }
    }
}
