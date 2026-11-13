using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.StoryBubbles;

namespace Zjcs.Official.StoryMasks
{
    public interface IStoryMaskHost
    {
        GameObject LoadPrefab(string originalGuid);
        GameObject LastNonTipWindow { get; }
        event Action<GameObject> OnWindowClosing;
        event Action<GameObject> OnWindowClosed;
        event Action<bool> OnOpeningWindowStateChanged;
    }

    public interface IStoryMaskBloomRegistry
    {
        void RegisterBuiltInGroup(UIBloomGroup group, UIBloomBuiltGroupPriority priority);
        void UnregisterBuiltInGroup(UIBloomGroup group, UIBloomBuiltGroupPriority priority);
    }

    // Keep the native UnsafeAssetPrefabProxy field structure. The host owns the
    // selected local prefab lease, while the original view owns its instance.
    [Serializable]
    public sealed class StoryMaskPrefabProxy
    {
        [Serializable] public sealed class Reference { public string mAssetGuid; }
        [SerializeField] private Reference mProxyRes;
        [SerializeField] private Transform mTrans;
        [NonSerialized] public Func<string, GameObject> LoadPrefab;
        [NonSerialized] public Action<GameObject> Initialize;
        private GameObject loaded;
        public GameObject Instantiate()
        {
            if (!loaded) loaded = LoadPrefab(mProxyRes.mAssetGuid);
            if (!loaded) throw new InvalidOperationException("Original story mask prefab is missing: " + mProxyRes.mAssetGuid);
            GameObject instance = UnityEngine.Object.Instantiate(loaded, mTrans, false);
            instance.name = loaded.name;
            Initialize(instance);
            return instance;
        }
        public void Unload() => loaded = null;
    }

    internal static class StoryMaskProjection
    {
        // UI camera resolution is supplied by the actual host canvas. This also
        // supports the original screen overlay path (null event camera).
        public static Camera GetUiCamera(Component owner)
        {
            Canvas canvas = owner.GetComponentInParent<Canvas>();
            return canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
        }
        public static float GetCanvasScaleFactor(Canvas canvas, CanvasScaler scaler)
        {
            Rect pixelRect = canvas.pixelRect;
            float width = pixelRect.width / scaler.referenceResolution.x;
            pixelRect = canvas.pixelRect;
            return Mathf.Min(width, pixelRect.height / scaler.referenceResolution.y);
        }
        public static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);
            return instance;
        }
        // Original UIUtils.CopyRectTransformData / CopyRectTransform.
        public static void CopyRectTransformData(RectTransform self, RectTransform source, RectTransform target)
        {
            if (!target || !source) return;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchorMax = source.anchorMax;
            target.anchorMin = source.anchorMin;
            target.anchoredPosition = source.anchoredPosition;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
            target.localPosition = source.localPosition;
        }
        // Original UIUtils.CalculateRelativeRect: transform all four world corners.
        public static Rect CalculateRelativeRect(Transform root, Transform child)
        {
            if (!(child is RectTransform rect)) return new Rect(Vector2.zero, Vector2.zero);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Matrix4x4 inverse = root.worldToLocalMatrix;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = inverse.MultiplyPoint3x4(corners[i]);
                min = Vector3.Min(local, min);
                max = Vector3.Max(local, max);
            }
            return new Rect((Vector2)min, (Vector2)(max - min));
        }
    }
}
