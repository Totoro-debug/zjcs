using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.BattleRoundUI
{
    // Official GameRoundScene, with the Windows overlay/camera binding kept here.
    [DefaultExecutionOrder(100)]
    public sealed class GameRoundScene : MonoBehaviour
    {
        [SerializeField] private float mSize = 5f;
        [SerializeField] public Transform mIconRoot;
        [SerializeField] public SpriteRenderer mLine;
        [SerializeField] private float mZValue = -16f;

        private Transform mTransformCache;
        private RectTransform mAttachUITrans;
        private Canvas mCanvas;
        private CanvasScaler mCanvasScaler;
        private Camera mSceneCamera;
        private Vector3 mLastPosition = Vector3.positiveInfinity;
        private float mLastScale = -1f;
        private static readonly Vector3 sFarPos = new Vector3(0f, 1000f, 0f);

        public Transform IconRoot => mIconRoot;
        public SpriteRenderer Line => mLine;
        public Transform Transform => mTransformCache;

        private void Awake()
        {
            mTransformCache = transform;
            SetBarFarAway();
        }

        private void OnEnable() => Canvas.willRenderCanvases += FixedPosAndScale;
        private void OnDisable() => Canvas.willRenderCanvases -= FixedPosAndScale;

        private void OnDestroy()
        {
            mAttachUITrans = null;
            mCanvas = null;
            mCanvasScaler = null;
            mSceneCamera = null;
        }

        public void SetVisible(bool visible)
        {
            enabled = visible;
            if (!visible) SetBarFarAway();
        }

        public void SetAttachUI(RectTransform uiTrans, Camera sceneCamera = null)
        {
            mAttachUITrans = uiTrans;
            mCanvasScaler = uiTrans.GetComponentInParent<CanvasScaler>();
            mCanvas = mCanvasScaler.GetComponent<Canvas>();
            mSceneCamera = sceneCamera;
            FixedPosAndScale();
        }

        private void LateUpdate() => FixedPosAndScale();

        private void FixedPosAndScale()
        {
            Camera camera = mSceneCamera != null ? mSceneCamera : Camera.main;
            if (!enabled || mTransformCache == null || mAttachUITrans == null || mCanvas == null || camera == null)
                return;
            Camera uiCamera = mCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mCanvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, mAttachUITrans.position);
            Vector3 position = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(camera.transform.position.z)));
            position.z = mZValue;
            if (position != mLastPosition)
            {
                mLastPosition = position;
                mTransformCache.position = position;
            }
            // Overlay canvas dimensions can change when the Windows viewport resizes.
            RectTransform canvasRect = mCanvas.transform as RectTransform;
            float height = canvasRect == null ? 0f : canvasRect.rect.height;
            if (height <= Mathf.Epsilon || mSize <= Mathf.Epsilon || camera.orthographicSize <= Mathf.Epsilon)
                return;
            float scale = camera.orthographicSize / mSize * (mCanvasScaler.referenceResolution.y / height);
            if (float.IsNaN(scale) || float.IsInfinity(scale)) return;
            if (!Mathf.Approximately(scale, mLastScale))
            {
                mLastScale = scale;
                mTransformCache.localScale = new Vector3(scale, scale, scale);
            }
        }

        private void SetBarFarAway()
        {
            mTransformCache.position = sFarPos;
            mLastPosition = sFarPos;
        }
    }
}
