// Selected original game-owned scrolling helper.
namespace Zjcs.Official.PartnersUI
{
using UnityEngine;
using UnityEngine.UI;

public static class ScrollRectUtil
{
    public static void ScrollToItem(ScrollRect scrollRect, RectTransform item, bool center = true)
    {
        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Rect rect = content.rect;
        Vector2 size = rect.size;
        rect = viewport.rect;
        Vector2 size2 = rect.size;
        rect = item.rect;
        Vector2 size3 = rect.size;
        Vector2 val = (Vector2)((Transform)content).InverseTransformPoint(((Transform)item).position);
        Vector2 anchoredPosition = content.anchoredPosition;
        if (scrollRect.horizontal)
        {
            float num = val.x - item.pivot.x * size3.x;
            if (center)
            {
                num -= (size2.x - size3.x) * 0.5f;
            }
            float num2 = Mathf.Max(0f, size.x - size2.x);
            num = Mathf.Clamp(num, 0f, num2);
            anchoredPosition.x = Mathf.Lerp(0f - num, num, content.pivot.x);
        }
        if (scrollRect.vertical)
        {
            rect = content.rect;
            float num3 = rect.yMax - val.y - (1f - item.pivot.y) * size3.y;
            if (center)
            {
                num3 -= (size2.y - size3.y) * 0.5f;
            }
            float num4 = Mathf.Max(0f, size.y - size2.y);
            num3 = Mathf.Clamp(num3, 0f, num4);
            float y = ((Transform)content).localPosition.y;
            rect = content.rect;
            float num5 = y + rect.yMax;
            rect = viewport.rect;
            float num6 = num5 - rect.yMax;
            anchoredPosition.y += num3 - num6;
        }
        content.anchoredPosition = anchoredPosition;
        scrollRect.velocity = Vector2.zero;
    }
}

}
