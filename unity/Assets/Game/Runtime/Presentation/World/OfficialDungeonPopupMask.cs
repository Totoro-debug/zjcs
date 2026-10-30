using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    // WindowMaskBase/WindowBlurMask and ScreenBlurPass with the local popup host.
    public sealed class OfficialDungeonPopupMask : MonoBehaviour
    {
        private WorldPopupWindow popup;
        private Canvas windowCanvas;
        private GraphicRaycaster windowRaycaster;
        private GameObject maskPrefab, mask;
        private Material sourceMaterial, blurMaterial;
        private string imagePath, buttonPath;
        private Action<GameObject> restoreBindings;
        private Action closed;
        private RenderTexture snapshot;
        private Coroutine preparation;
        private bool canvasEnabled, raycasterEnabled, closing;
        public bool IsReady { get; private set; }

        public void Initialize(WorldPopupWindow window, GameObject nativeMask, Material blur,
            string image, string button, Action<GameObject> restore)
        {
            if (window == null || nativeMask == null || blur == null || blur.shader.name != "Zjcs/Official/UI/ScreenBlur")
                throw new InvalidOperationException("Original private dungeon blur mask resources are missing.");
            popup = window;
            popup.OnShow -= KeepWindowAboveMask;
            popup.OnShow += KeepWindowAboveMask;
            maskPrefab = nativeMask;
            sourceMaterial = blur;
            imagePath = image;
            buttonPath = button;
            restoreBindings = restore;
            windowCanvas = GetComponent<Canvas>();
            windowRaycaster = GetComponent<GraphicRaycaster>();
            if (windowCanvas == null || windowRaycaster == null)
                throw new InvalidOperationException("Original private dungeon popup canvas is missing.");
            canvasEnabled = windowCanvas.enabled;
            raycasterEnabled = windowRaycaster.enabled;
            popup.SetMaskClickAction(Close);
        }

        public void Show(Action onClosed)
        {
            if (popup == null || !isActiveAndEnabled)
                throw new InvalidOperationException("Original private dungeon mask has not been initialized.");
            if (preparation != null) StopCoroutine(preparation);
            ReleaseMask();
            closed = onClosed;
            closing = false;
            IsReady = false;
            // WindowBlurMask.Prepare captures the preceding frame before the popup is visible.
            windowCanvas.enabled = false;
            windowRaycaster.enabled = false;
            preparation = StartCoroutine(PrepareAndShow());
        }

        private IEnumerator PrepareAndShow()
        {
            yield return new WaitForEndOfFrame();
            preparation = null;
            try
            {
                var capture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
                try
                {
                    snapshot = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width / 13), Mathf.Max(1, Screen.height / 13), 0);
                    blurMaterial = new Material(sourceMaterial);
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
                    Blur(capture, snapshot);
                }
                finally { RenderTexture.ReleaseTemporary(capture); }
                mask = Instantiate(maskPrefab, transform.parent, false);
                mask.name = maskPrefab.name;
                restoreBindings?.Invoke(mask);
                var maskCanvas = mask.GetComponent<Canvas>();
                var image = Require(mask.transform, imagePath).GetComponent<RawImage>();
                var button = Require(mask.transform, buttonPath).GetComponent<Button>();
                if (maskCanvas == null || image == null || button == null || mask.GetComponent<GraphicRaycaster>() == null)
                    throw new InvalidOperationException("Original private dungeon mask component binding is incomplete.");
                Canvas parentCanvas = transform.parent.GetComponentInParent<Canvas>();
                if (parentCanvas == null) throw new InvalidOperationException("The private popup container canvas is missing.");
                int order = parentCanvas.sortingOrder;
                foreach (Canvas existing in parentCanvas.GetComponentsInChildren<Canvas>(true))
                    if (existing.isActiveAndEnabled && existing.overrideSorting && existing != maskCanvas
                        && !existing.transform.IsChildOf(transform)) order = Math.Max(order, existing.sortingOrder);
                windowCanvas.overrideSorting = true;
                windowCanvas.sortingLayerID = parentCanvas.sortingLayerID;
                windowCanvas.sortingOrder = order + 10; // WindowContainer.OnWindowShowing interval.
                maskCanvas.sortingLayerID = windowCanvas.sortingLayerID;
                maskCanvas.sortingOrder = windowCanvas.sortingOrder - 1;
                maskCanvas.enabled = true;
                mask.SetActive(true);
                maskCanvas.overrideSorting = true;
                image.texture = snapshot;
                Canvas.ForceUpdateCanvases();
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = ((RectTransform)maskCanvas.rootCanvas.transform).rect.size;
                rect.anchoredPosition = Vector2.zero;
                float numerator = rect.rect.width * snapshot.height;
                float denominator = snapshot.width * rect.rect.height;
                if (!Mathf.Approximately(denominator, numerator))
                {
                    Rect uv = image.uvRect;
                    uv.width = numerator / denominator;
                    uv.x = .5f - .5f * uv.width;
                    image.uvRect = uv;
                }
                button.onClick.AddListener(popup.OnMaskClick);
                windowCanvas.enabled = canvasEnabled;
                windowRaycaster.enabled = raycasterEnabled;
                popup.Show();
                IsReady = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                ReleaseMask();
                CompleteClose();
            }
        }

        private void KeepWindowAboveMask(WorldPopupWindow shown)
        {
            if (shown != popup || mask == null) return;
            Canvas maskCanvas = mask.GetComponent<Canvas>();
            if (maskCanvas != null) popup.SetCanvasSortingOrder(maskCanvas.sortingOrder + 1);
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            IsReady = false;
            if (preparation != null) { StopCoroutine(preparation); preparation = null; }
            popup.Hide(CompleteClose);
        }

        private void CompleteClose()
        {
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }

        private void Blur(RenderTexture capture, RenderTexture target)
        {
            var up = new RenderTexture[4];
            var down = new RenderTexture[4];
            RenderTexture source = capture;
            void Blit(RenderTexture from, RenderTexture to, Vector2 direction)
            {
                blurMaterial.SetVector("_BlurDir", direction);
                blurMaterial.SetVector("_GlobalMipBias", Vector2.zero);
                blurMaterial.SetTexture("_BlitTexture", from);
                blurMaterial.SetVector("_BlitTexture_TexelSize", new Vector4(1f / from.width, 1f / from.height, from.width, from.height));
                Graphics.Blit(from, to, blurMaterial, 0);
            }
            try
            {
                int width = capture.width, height = capture.height;
                for (int i = 0; i < 4; i++)
                {
                    width = Mathf.Max(1, width >> 1); height = Mathf.Max(1, height >> 1);
                    up[i] = RenderTexture.GetTemporary(width, height, 0, capture.format);
                    down[i] = RenderTexture.GetTemporary(width, height, 0, capture.format);
                    up[i].filterMode = down[i].filterMode = FilterMode.Bilinear;
                    up[i].wrapMode = down[i].wrapMode = TextureWrapMode.Repeat;
                    Blit(source, up[i], Vector2.right);
                    Blit(up[i], down[i], Vector2.up);
                    source = down[i];
                }
                for (int i = 2; i >= 0; i--)
                {
                    Blit(source, down[i], Vector2.right);
                    Blit(down[i], i == 0 ? target : up[i], Vector2.up);
                    source = up[i];
                }
            }
            finally
            {
                foreach (RenderTexture texture in up) if (texture != null) RenderTexture.ReleaseTemporary(texture);
                foreach (RenderTexture texture in down) if (texture != null) RenderTexture.ReleaseTemporary(texture);
            }
        }

        private static Transform Require(Transform root, string path)
        {
            Transform node = string.IsNullOrEmpty(path) ? root : root.Find(path);
            if (node == null) throw new InvalidOperationException("Original private mask hierarchy changed: " + path);
            return node;
        }

        private void ReleaseMask()
        {
            if (mask != null)
            {
                Transform imageNode = string.IsNullOrEmpty(imagePath) ? mask.transform : mask.transform.Find(imagePath);
                RawImage image = imageNode == null ? null : imageNode.GetComponent<RawImage>();
                if (image != null) image.texture = null;
                mask.SetActive(false);
                Destroy(mask);
                mask = null;
            }
            if (snapshot != null) { RenderTexture.ReleaseTemporary(snapshot); snapshot = null; }
            if (blurMaterial != null) { Destroy(blurMaterial); blurMaterial = null; }
        }

        private void OnDisable()
        {
            if (preparation != null) { StopCoroutine(preparation); preparation = null; }
            IsReady = false;
            ReleaseMask();
            if (windowCanvas != null) windowCanvas.enabled = canvasEnabled;
            if (windowRaycaster != null) windowRaycaster.enabled = raycasterEnabled;
            if (closing) CompleteClose();
        }

        private void OnDestroy()
        {
            if (popup != null) popup.OnShow -= KeepWindowAboveMask;
            ReleaseMask();
            closed = null;
        }
    }
}
