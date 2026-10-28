using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    // WindowBlurMask.Prepare / WindowMaskBase.AttachToWindow and ScreenBlurPass.
    internal sealed class OfficialSkillPopupMask : MonoBehaviour
    {
        private GameObject mask;
        private RenderTexture snapshot;
        private Material blurMaterial;
        private Canvas windowCanvas;
        private GraphicRaycaster windowRaycaster;
        private bool previousCanvas, previousRaycaster;
        private Action close;
        private bool allowClose;
        private float shownAt;
        internal bool IsReady { get; private set; }
        internal float ShownAtUnscaled { get; private set; }

        internal void Show(Dictionary<string, object> templates, int maskType, bool closeByMask, Action onClose, Action onReady)
        {
            close = onClose; allowClose = closeByMask;
            windowCanvas = GetComponent<Canvas>();
            if (windowCanvas == null) windowCanvas = gameObject.AddComponent<Canvas>();
            windowRaycaster = GetComponent<GraphicRaycaster>();
            if (windowRaycaster == null) windowRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            previousCanvas = windowCanvas.enabled; previousRaycaster = windowRaycaster.enabled;
            Canvas parent = transform.parent.GetComponentInParent<Canvas>().rootCanvas;
            int order = parent.sortingOrder;
            foreach (Canvas child in parent.GetComponentsInChildren<Canvas>())
                if (child != windowCanvas && child.overrideSorting && child.name != "OfficialSkillSystemMessage") order = Math.Max(order, child.sortingOrder);
            windowCanvas.overrideSorting = true; windowCanvas.sortingOrder = order + 10;
            windowCanvas.enabled = false; windowRaycaster.enabled = false;
            StartCoroutine(Prepare(templates, parent, maskType, onReady));
        }

        private IEnumerator Prepare(Dictionary<string, object> templates, Canvas parent, int maskType, Action onReady)
        {
            if (maskType == 3) yield return new WaitForEndOfFrame();
            var masks = CharacterModelPackage.AsObject(templates["popup_masks"], "original window masks");
            var binding = CharacterModelPackage.AsObject(masks[maskType.ToString(CultureInfo.InvariantCulture)], "original window mask");
            var fields = CharacterModelPackage.AsObject(binding["fields"], "WindowBlurMask fields");
            string Text(object value) => Convert.ToString(value, CultureInfo.InvariantCulture);
            GameObject source = Resources.Load<GameObject>(Text(binding["prefab_resource"]));
            Transform original = source == null ? null : source.transform.Find(Text(binding["prefab_path"]));
            if (original == null) throw new InvalidOperationException("The original shared blur mask prefab is missing.");
            if (maskType == 3)
            {
                Material material = Resources.Load<Material>(Text(binding["blur_material"]));
                if (material == null || material.shader.name != "Zjcs/Official/UI/ScreenBlur")
                    throw new InvalidOperationException("The converted original ScreenBlur shader is missing.");
                var capture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
                try
                {
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
                    snapshot = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width / 13), Mathf.Max(1, Screen.height / 13), 0);
                    blurMaterial = new Material(material);
                    Blur(capture, snapshot);
                }
                finally { RenderTexture.ReleaseTemporary(capture); }
            }
            mask = Instantiate(original.gameObject, parent.transform, false);
            mask.name = original.name;
            OfficialCharacterPanelView.StripUnavailableOfficialBehaviours(mask.transform);
            Canvas canvas = mask.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Original WindowBlurMask canvas is missing.");
            canvas.enabled = false;
            mask.SetActive(true);
            var rect = (RectTransform)mask.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            canvas.sortingLayerID = windowCanvas.sortingLayerID;
            canvas.overrideSorting = true; canvas.sortingOrder = windowCanvas.sortingOrder - 1;
            string prefix = "/WorldCanvas/" + Text(binding["prefab_path"]);
            Transform Target(string field)
            {
                string path = Text(fields[field]);
                return path == prefix ? mask.transform : mask.transform.Find(path.Substring(prefix.Length + 1));
            }
            Button button = Target("mBtn").GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Original window mask button is missing.");
            if (maskType == 3)
            {
                RawImage image = Target("mImage").GetComponent<RawImage>();
                if (image == null) throw new InvalidOperationException("Original blur mask RawImage is missing.");
                image.texture = snapshot;
                var rawImage = CharacterModelPackage.AsObject(binding["raw_image"], "original blur RawImage");
                var uvSource = (List<object>)rawImage["m_UVRect"];
                float Uv(int index) => Convert.ToSingle(uvSource[index], CultureInfo.InvariantCulture);
                image.uvRect = new Rect(Uv(0), Uv(1), Uv(2), Uv(3));
                Canvas.ForceUpdateCanvases();
                RectTransform imageRect = image.rectTransform;
                imageRect.anchorMin = imageRect.anchorMax = new Vector2(.5f, .5f);
                imageRect.sizeDelta = ((RectTransform)parent.transform).rect.size;
                imageRect.anchoredPosition = Vector2.zero;
                float numerator = imageRect.rect.width * snapshot.height;
                float denominator = snapshot.width * imageRect.rect.height;
                if (!Mathf.Approximately(denominator, numerator))
                {
                    Rect uv = image.uvRect;
                    uv.width = numerator / denominator;
                    uv.x = .5f - .5f * uv.width;
                    image.uvRect = uv;
                }
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Rect uv = image.uvRect;
                    uv.y += uv.height;
                    uv.height = -uv.height;
                    image.uvRect = uv;
                }
            }
            button.onClick.AddListener(() => { if (allowClose && IsReady && Time.time - shownAt > .5f) close?.Invoke(); });
            if (mask.GetComponent<GraphicRaycaster>() == null) mask.AddComponent<GraphicRaycaster>();
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = windowCanvas.sortingOrder - 1;
            windowCanvas.enabled = previousCanvas; windowRaycaster.enabled = previousRaycaster;
            shownAt = Time.time; ShownAtUnscaled = Time.unscaledTime; IsReady = true;
            onReady?.Invoke();
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

        private void OnDestroy()
        {
            if (mask != null) { mask.SetActive(false); Destroy(mask); }
            if (snapshot != null) { RenderTexture.ReleaseTemporary(snapshot); snapshot = null; }
            if (blurMaterial != null) Destroy(blurMaterial);
            close = null;
        }
    }
}
