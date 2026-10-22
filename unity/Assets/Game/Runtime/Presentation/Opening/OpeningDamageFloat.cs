using TMPro;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// One floating damage label. The official record only carries the damage value,
    /// element and result flags; the official float clip is not migrated yet, so the
    /// label rises and fades with a fixed short motion while keeping the official font,
    /// size and colour.
    /// </summary>
    public sealed class OpeningDamageFloat : MonoBehaviour
    {
        public const float Duration = 0.9f;
        private const float Rise = 44f;

        private TMP_Text label;
        private RectTransform rect;
        private Vector2 start;
        private Color color;
        private float elapsed;

        public void Initialize(TMP_Text text, Vector2 anchoredPosition, Color tint)
        {
            label = text;
            rect = (RectTransform)transform;
            start = anchoredPosition;
            color = tint;
            elapsed = 0f;
            rect.anchoredPosition = start;
            label.color = color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Duration);
            if (rect != null) rect.anchoredPosition = start + new Vector2(0f, Rise * progress);
            if (label != null)
            {
                color.a = 1f - progress;
                label.color = color;
            }
            if (progress >= 1f) Destroy(gameObject);
        }
    }
}
