using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>SafeArea.SetDefaultSize / ApplySafeAreaIsolated for a Windows portrait canvas.</summary>
    [DefaultExecutionOrder(-1)]
    public sealed class WorldSafeArea : MonoBehaviour
    {
        public float PaddingTop, PaddingBottom;
        public bool DisableTopAdapt, DisableBottomAdapt;
        private int width, height;
        // Official SafeArea.Awake resets even a disabled component's rectangle.
        private void Awake() => SetDefaultSize();
        private void OnEnable() => Apply();
        private void Start() => Apply();
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
            float bottom = DisableBottomAdapt ? 0f : safe.yMin / factor + PaddingBottom;
            float top = DisableTopAdapt ? root.rect.height : safe.yMax / factor + PaddingTop;
            var area = new Rect(safe.x / factor, bottom, safe.width / factor, Mathf.Max(0f, top - bottom));
            SetDefaultSize();
            self.sizeDelta += area.size - self.rect.size;
            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            Vector3 origin = self.worldToLocalMatrix.MultiplyPoint3x4(corners[0]);
            self.anchoredPosition += new Vector2(origin.x + area.x, origin.y + area.y);
        }

        private void SetDefaultSize()
        {
            var self = transform as RectTransform;
            if (self == null) return;
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.pivot = Vector2.zero;
            self.sizeDelta = Vector2.zero;
            self.anchoredPosition = Vector2.zero;
        }
    }
}
