using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>SafeArea.SetDefaultSize / ApplySafeAreaIsolated for a Windows portrait canvas.</summary>
    [DefaultExecutionOrder(-1)]
    public sealed class WorldSafeArea : MonoBehaviour
    {
        public float PaddingTop, PaddingBottom;
        private int width, height;
        private void OnEnable() => Apply();
        private void LateUpdate() { if (width != Screen.width || height != Screen.height) Apply(); }

        private void Apply()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform root = canvas.rootCanvas.transform as RectTransform;
            RectTransform self = transform as RectTransform;
            if (root == null || self == null) return;
            Canvas.ForceUpdateCanvases();
            width = Screen.width;
            height = Screen.height;
            Rect safe = Screen.safeArea;
            float factor = canvas.rootCanvas.scaleFactor;
            var area = new Rect(safe.x / factor, safe.y / factor + PaddingBottom,
                safe.width / factor, safe.height / factor + PaddingTop - PaddingBottom);
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.pivot = Vector2.zero;
            self.sizeDelta = Vector2.zero;
            self.anchoredPosition = Vector2.zero;
            self.sizeDelta += area.size - self.rect.size;
            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            Vector3 origin = self.worldToLocalMatrix.MultiplyPoint3x4(corners[0]);
            self.anchoredPosition += new Vector2(origin.x + area.x, origin.y + area.y);
        }
    }
}
