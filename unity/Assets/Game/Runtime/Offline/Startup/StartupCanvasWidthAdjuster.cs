using UnityEngine;

namespace Zjcs.Offline.Startup
{
    // CanvasWidthAdjuster.GetAdaptationResult / AdjustContentSize, Windows
    // safe-area branch. Original aspect limits and two-pixel border overlap.
    [DefaultExecutionOrder(-2)]
    public sealed class StartupCanvasWidthAdjuster : MonoBehaviour
    {
        public RectTransform mCanvas, mContent, mLeft, mRight;
        private RectTransform top, bottom;
        private int width, height;
        private Vector2 canvasSize;
        private void Start() => Apply();
        private void LateUpdate() { if (width != Screen.width || height != Screen.height || canvasSize != mCanvas.rect.size) Apply(); }
        private void Apply()
        {
            width = Screen.width; height = Screen.height;
            Canvas.ForceUpdateCanvases();
            Vector2 size = canvasSize = mCanvas.rect.size;
            float horizontal = size.x / size.y > 121f / 160f ? (size.x - size.y * (121f / 160f)) / 2f + 2f : 0f;
            float vertical = size.x / size.y < .45f ? (size.y - size.x / .45f) / 2f + 2f : 0f;
            mContent.offsetMin = new Vector2(horizontal, vertical);
            mContent.offsetMax = -mContent.offsetMin;
            if (mLeft != null) { mLeft.gameObject.SetActive(horizontal > 0f); mLeft.sizeDelta = new Vector2(horizontal, mLeft.sizeDelta.y); }
            if (mRight != null) { mRight.gameObject.SetActive(horizontal > 0f); mRight.sizeDelta = new Vector2(horizontal, mRight.sizeDelta.y); }
            if (vertical > 0f && mLeft != null)
            {
                if (top == null) top = CreateVerticalBorder("TopBorder (Dynamic Generated)", 1f);
                if (bottom == null) bottom = CreateVerticalBorder("BottomBorder (Dynamic Generated)", 0f);
                top.sizeDelta = new Vector2(0f, vertical); bottom.sizeDelta = new Vector2(0f, vertical);
            }
            if (top != null) top.gameObject.SetActive(vertical > 0f);
            if (bottom != null) bottom.gameObject.SetActive(vertical > 0f);
        }
        private RectTransform CreateVerticalBorder(string name, float y)
        {
            RectTransform border = Instantiate(mLeft, mLeft.parent);
            border.name = name;
            border.anchorMin = new Vector2(0f, y); border.anchorMax = new Vector2(1f, y);
            border.pivot = new Vector2(.5f, y); border.sizeDelta = Vector2.zero; border.anchoredPosition = Vector2.zero;
            return border;
        }
    }
}
