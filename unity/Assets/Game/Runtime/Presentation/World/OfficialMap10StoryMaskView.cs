using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10StoryMaskView : MonoBehaviour
    {
        private OfficialMap10StoryMaskGraphic graphic;

        public void Initialize(Transform canvasRoot)
        {
            if (canvasRoot == null) throw new ArgumentNullException(nameof(canvasRoot));
            if (graphic != null) throw new InvalidOperationException("Map10 story mask is already initialized.");
            if (canvasRoot.GetComponent<Canvas>() == null || canvasRoot.GetComponent<GraphicRaycaster>() == null)
                throw new InvalidOperationException("Map10 story mask requires the official UI canvas and raycaster.");

            var maskObject = new GameObject("Map10StoryMask", typeof(RectTransform),
                typeof(OfficialMap10StoryMaskGraphic));
            maskObject.transform.SetParent(canvasRoot, false);
            maskObject.transform.SetAsLastSibling();
            RectTransform rect = maskObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            graphic = maskObject.GetComponent<OfficialMap10StoryMaskGraphic>();
            graphic.Hide();
        }

        public void Show(Camera worldCamera, Vector3 worldPosition, Vector2 size)
        {
            if (graphic == null) throw new InvalidOperationException("Map10 story mask is not initialized.");
            graphic.Show(worldCamera, worldPosition, size);
        }

        public void Refresh() => graphic?.RefreshTarget();

        public bool ContainsScreenPoint(Vector2 screenPoint) =>
            graphic != null && graphic.ContainsScreenPoint(screenPoint);

        public void Hide() => graphic?.Hide();

        private void OnDestroy()
        {
            if (graphic == null) return;
            if (Application.isPlaying) Destroy(graphic.gameObject);
            else DestroyImmediate(graphic.gameObject);
            graphic = null;
        }
    }

    public sealed class OfficialMap10StoryMaskGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        private const float DefaultRadius = 10f;
        private const int CornerSegments = 6;
        private static readonly Color DefaultMaskColor = new Color(0f, 0f, 0f, 199f / 255f);

        private Camera worldCamera;
        private Vector3 worldPosition;
        private Vector2 targetSize;
        private Vector2 targetMin;
        private Vector2 targetMax;
        private bool targetVisible;
        private bool targetProjected;

        public void Show(Camera camera, Vector3 position, Vector2 size)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (size.x <= 0f || size.y <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            worldCamera = camera;
            worldPosition = position;
            targetSize = size;
            targetVisible = true;
            targetProjected = false;
            color = DefaultMaskColor;
            raycastTarget = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            RefreshTarget();
        }

        public void RefreshTarget()
        {
            if (!targetVisible || worldCamera == null) return;
            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition, null, out Vector2 localPosition))
            {
                targetProjected = false;
                raycastTarget = false;
                SetVerticesDirty();
                return;
            }

            Vector2 halfSize = targetSize * 0.5f;
            Vector2 nextMin = localPosition - halfSize;
            Vector2 nextMax = localPosition + halfSize;
            bool wasProjected = targetProjected;
            targetProjected = true;
            raycastTarget = true;
            if (!wasProjected || (nextMin - targetMin).sqrMagnitude > 0.01f
                || (nextMax - targetMax).sqrMagnitude > 0.01f)
            {
                targetMin = nextMin;
                targetMax = nextMax;
                SetVerticesDirty();
            }
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return targetVisible && targetProjected && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, null, out Vector2 localPosition) && IsInsideTarget(localPosition);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!targetVisible || !targetProjected || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 localPosition))
                return true;
            return !IsInsideTarget(localPosition);
        }

        public void Hide()
        {
            targetVisible = false;
            targetProjected = false;
            worldCamera = null;
            raycastTarget = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!targetVisible || !targetProjected) return;

            Rect outer = rectTransform.rect;
            Vector2 outerMin = outer.min;
            Vector2 outerMax = outer.max;
            Vector2 innerBottomLeft = targetMin;
            Vector2 innerTopRight = targetMax;
            Vector2 innerTopLeft = new Vector2(innerBottomLeft.x, innerTopRight.y);
            Vector2 innerBottomRight = new Vector2(innerTopRight.x, innerBottomLeft.y);

            AddTriangle(helper, outerMin, new Vector2(outerMin.x, outerMax.y), innerBottomLeft);
            AddTriangle(helper, new Vector2(outerMin.x, outerMax.y), innerBottomLeft, innerTopLeft);
            AddTriangle(helper, new Vector2(outerMin.x, outerMax.y), innerTopLeft, new Vector2(outerMax.x, outerMax.y));
            AddTriangle(helper, new Vector2(outerMax.x, outerMax.y), innerTopLeft, innerTopRight);
            AddTriangle(helper, new Vector2(outerMax.x, outerMax.y), innerTopRight, outerMax);
            AddTriangle(helper, innerTopRight, outerMax, innerBottomRight);
            AddTriangle(helper, innerBottomLeft, innerBottomRight, outerMax);
            AddTriangle(helper, outerMin, innerBottomLeft, outerMax);

            float radius = Mathf.Min(DefaultRadius, Mathf.Min(targetSize.x, targetSize.y) * 0.5f);
            Vector2[] corners =
            {
                innerBottomLeft,
                innerTopLeft,
                innerTopRight,
                innerBottomRight
            };
            Vector2[] centers =
            {
                innerBottomLeft + new Vector2(radius, radius),
                innerTopLeft + new Vector2(radius, -radius),
                innerTopRight + new Vector2(-radius, -radius),
                innerBottomRight + new Vector2(-radius, radius)
            };
            Vector2[] arcStarts =
            {
                innerBottomLeft + new Vector2(0f, radius),
                innerTopLeft + new Vector2(radius, 0f),
                innerTopRight + new Vector2(0f, -radius),
                innerBottomRight + new Vector2(-radius, 0f)
            };
            float[] startAngles = { Mathf.PI, Mathf.PI * 0.5f, 0f, Mathf.PI * 1.5f };

            for (int corner = 0; corner < corners.Length; corner++)
            {
                Vector2 previous = arcStarts[corner];
                for (int segment = 1; segment <= CornerSegments; segment++)
                {
                    float angle = startAngles[corner] + Mathf.PI * 0.5f * segment / CornerSegments;
                    Vector2 next = centers[corner] + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    AddTriangle(helper, corners[corner], previous, next);
                    previous = next;
                }
            }
        }

        private bool IsInsideTarget(Vector2 point)
        {
            return point.x >= targetMin.x && point.x <= targetMax.x
                && point.y >= targetMin.y && point.y <= targetMax.y;
        }

        private void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c)
        {
            int first = helper.currentVertCount;
            AddVertex(helper, a);
            AddVertex(helper, b);
            AddVertex(helper, c);
            helper.AddTriangle(first, first + 1, first + 2);
        }

        private void AddVertex(VertexHelper helper, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            helper.AddVert(vertex);
        }
    }
}
