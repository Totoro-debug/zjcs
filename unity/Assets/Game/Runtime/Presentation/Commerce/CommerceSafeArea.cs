using UnityEngine;

namespace Zjcs.Presentation.Commerce
{
    /// <summary>
    /// Restores the official SafeArea behaviour for commerce fragments. The pinned UI
    /// extraction serializes the runtime-adjusted anchors as zero for full-screen slots, so
    /// stretch those slots to the parent while keeping authored stretch anchoring intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CommerceSafeArea : MonoBehaviour
    {
        private RectTransform rect;

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (rect == null) return;
            bool full = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
            if (!full && rect.anchorMin == rect.anchorMax && rect.sizeDelta == Vector2.zero)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }
    }
}

