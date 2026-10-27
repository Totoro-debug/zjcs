using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // DailyDungeonBigMapDialogGradeGrid. Initialize restores its serialized
    // references before installing the listener originally installed in Awake.
    public sealed class WorldDailyDungeonBigMapDialogGradeGrid : MonoBehaviour
    {
        public LoopVerticalScrollRect ScrollRect;
        public RectTransform RectTransform;
        public void Initialize(LoopVerticalScrollRect scroll, RectTransform rect)
        {
            if (ScrollRect != null) ScrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
            ScrollRect = scroll;
            RectTransform = rect;
            ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }
        private void OnDestroy()
        {
            if (ScrollRect != null) ScrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
        }
        public void Refresh()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
            OnScrollRectValueChanged(ScrollRect.normalizedPosition);
        }
        private void OnScrollRectValueChanged(Vector2 value)
        {
            float y = RectTransform.sizeDelta.y;
            Rect rect = ScrollRect.viewport.rect;
            float distance = y - rect.height;
            if (distance < 0f)
                RectTransform.anchoredPosition = new Vector2(RectTransform.anchoredPosition.x, ScrollRect.content.anchoredPosition.y);
            else
                RectTransform.anchoredPosition = new Vector2(RectTransform.anchoredPosition.x, distance * (value.y - 1f));
        }
    }
}
