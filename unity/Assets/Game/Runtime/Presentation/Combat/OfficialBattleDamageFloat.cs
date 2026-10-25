using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// One official battle damage prefab with a simulation-ticked lifetime and animation.
    /// </summary>
    public sealed class OfficialBattleDamageFloat : MonoBehaviour
    {
        public const float Duration = 0.9f;
        private const float Rise = 1.2f;

        private TextMeshPro label;
        private TMP_Text primaryLabel;
        private CanvasGroup canvasGroup;
        private OfficialBattleHudAnimationPlayer animationPlayer;
        private SortingGroup sortingGroup;
        private RectTransform rectTransform;
        private Vector3 start;
        private Vector3 worldAnchor;
        private Vector2 anchoredStart;
        private RectTransform screenSpaceParent;
        private Camera projectionCamera;
        private Camera eventCamera;
        private Color color;
        private float duration = Duration;
        private float elapsed;
        private float cameraScale;
        private int sortingOrderStart;
        private int lastSiblingIndex = -1;
        private bool screenSpace;
        private bool manualTick;
        private bool hasOfficialAnimation;

        public int EffectType { get; private set; }
        public int TargetId { get; private set; }
        public float StackHeight { get; private set; }
        public Vector3 StackOffset { get; private set; }

        public static OfficialBattleDamageFloat Spawn(Transform parent, TMP_FontAsset font,
            string text, Color tint, Vector3 position)
        {
            var gameObject = new GameObject("BattleDamageFloat");
            if (parent != null) gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            var label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            label.color = tint;
            label.enableWordWrapping = false;
            label.sortingOrder = 50;
            gameObject.transform.localScale = Vector3.one * 0.02f;
            Camera camera = Camera.main;
            if (camera != null) gameObject.transform.rotation = camera.transform.rotation;
            var result = gameObject.AddComponent<OfficialBattleDamageFloat>();
            result.label = label;
            result.start = position;
            result.color = tint;
            result.elapsed = 0f;
            result.EffectType = 0;
            return result;
        }

        public void InitializeOfficial(int effectType, TMP_Text text, CanvasGroup group,
            OfficialBattleHudAnimationPlayer animations, float showTime, bool isScreenSpace,
            bool usesOfficialAnimation, int targetId, float stackHeight, Vector3 anchorWorld,
            RectTransform screenParent, Camera worldCamera, Camera canvasCamera, float worldCameraScale = 0f,
            int worldSortingOrderStart = 0)
        {
            EffectType = effectType;
            primaryLabel = text;
            canvasGroup = group;
            animationPlayer = animations;
            rectTransform = transform as RectTransform;
            duration = showTime > 0f ? showTime : Duration;
            cameraScale = worldCameraScale;
            sortingOrderStart = worldSortingOrderStart;
            sortingGroup = GetComponent<SortingGroup>();
            screenSpace = isScreenSpace;
            manualTick = true;
            hasOfficialAnimation = usesOfficialAnimation;
            TargetId = targetId;
            StackHeight = stackHeight;
            worldAnchor = anchorWorld;
            screenSpaceParent = screenParent;
            projectionCamera = worldCamera;
            eventCamera = canvasCamera;
            StackOffset = Vector3.zero;
            start = transform.position;
            anchoredStart = rectTransform == null ? Vector2.zero : rectTransform.anchoredPosition;
            color = primaryLabel == null ? Color.white : primaryLabel.color;
            RefreshSortingOrder();
        }

        public void SetStackOffset(Vector3 offset)
        {
            StackOffset = offset;
            Vector3 anchor = worldAnchor + offset;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (screenSpace && rectTransform != null && screenSpaceParent != null
                && projectionCamera != null)
            {
                Vector3 screen = projectionCamera.WorldToScreenPoint(anchor);
                if (screen.z >= 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    screenSpaceParent, screen, eventCamera, out Vector2 local))
                {
                    anchoredStart = local;
                    rectTransform.anchoredPosition = local + (!hasOfficialAnimation
                        ? Vector2.up * (80f * progress) : Vector2.zero);
                    return;
                }
            }

            start = anchor;
            transform.position = start + (!hasOfficialAnimation
                ? Vector3.up * (Rise * progress) : Vector3.zero);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            elapsed += deltaTime;
            animationPlayer?.Tick(deltaTime);
            RefreshSortingOrder();
            if (cameraScale > 0f && Camera.main != null && Camera.main.orthographic)
            {
                float scale = Camera.main.orthographicSize / cameraScale;
                transform.localScale = new Vector3(scale, scale, scale);
            }
            if (!hasOfficialAnimation)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                if (screenSpace && rectTransform != null)
                    rectTransform.anchoredPosition = anchoredStart + new Vector2(0f, 80f * progress);
                else
                    transform.position = start + new Vector3(0f, Rise * progress, 0f);
                if (canvasGroup != null) canvasGroup.alpha = 1f - progress;
                if (label != null)
                {
                    color.a = 1f - progress;
                    label.color = color;
                }
                if (primaryLabel != null)
                {
                    Color tint = color;
                    tint.a = 1f - progress;
                    primaryLabel.color = tint;
                }
            }
            if (elapsed >= duration) Destroy(gameObject);
        }

        private void Update()
        {
            if (!manualTick) Tick(Time.deltaTime);
        }

        private void RefreshSortingOrder()
        {
            if (sortingGroup == null) return;
            int siblingIndex = transform.GetSiblingIndex();
            if (siblingIndex == lastSiblingIndex) return;
            lastSiblingIndex = siblingIndex;
            sortingGroup.sortingOrder = sortingOrderStart + siblingIndex;
        }
    }
}
