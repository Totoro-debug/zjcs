using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ProfessionUI
{
    // Official ImageLocator; sprite pivot correction and update order are retained.
    [ExecuteAlways]
    public sealed class ImageLocator : MonoBehaviour
    {
        [SerializeField] private Image mImage;
        public bool IsAvaliable => mImage != null && mImage.sprite != null;
        public Image Image => mImage;
        public Vector2 FixedPos
        {
            get
            {
                if (!IsAvaliable) return Vector2.zero;
                Rect rect = ((RectTransform)transform).rect;
                Vector2 pivot = mImage.sprite.pivot / mImage.sprite.rect.size;
                return new Vector2(rect.width * (mImage.rectTransform.pivot.x - pivot.x),
                    rect.height * (mImage.rectTransform.pivot.y - pivot.y));
            }
        }
        private void OnEnable() => Refresh();
        public void Refresh()
        {
            if (!IsAvaliable) return;
            mImage.SetNativeSize();
            mImage.rectTransform.anchoredPosition = FixedPos;
        }
        public void SetSprite(Sprite sprite)
        {
            mImage.sprite = sprite;
            Refresh();
        }
    }
}
