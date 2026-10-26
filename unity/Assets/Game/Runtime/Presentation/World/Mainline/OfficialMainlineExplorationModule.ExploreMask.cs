using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineExplorationModule
        {
            public readonly Dictionary<OfficialMap11WitchWindow, GameObject> exploreMasks = new Dictionary<OfficialMap11WitchWindow, GameObject>();
            public readonly Dictionary<OfficialMap11WitchWindow, RenderTexture> exploreSnapshots = new Dictionary<OfficialMap11WitchWindow, RenderTexture>();
            private IEnumerator PrepareExploreMask(OfficialMap11WitchWindow window, Action show)
            {
                // WindowBlurMask.Prepare captures before the popup is shown.
                yield return new WaitForEndOfFrame();
                if (window.Root == null || !exploreWindows.Contains(window)) yield break;
                var capture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
                var snapshot = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width / 13), Mathf.Max(1, Screen.height / 13), 0);
                try
                {
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
                    BlurExploreCapture(capture, snapshot);
                }
                catch
                {
                    RenderTexture.ReleaseTemporary(snapshot);
                    throw;
                }
                finally { RenderTexture.ReleaseTemporary(capture); }
                exploreSnapshots.Add(window, snapshot);
                GameObject native = Resources.Load<GameObject>(host.NativeUiResource("WorldCanvas"));
                var mask = Instantiate(native.transform.Find("Content/Popup/BlurMask").gameObject, exploreCanvas.transform, false);
                mask.name = "BlurMask";
                exploreMasks.Add(window, mask);
                var canvas = mask.GetComponent<Canvas>() ?? mask.AddComponent<Canvas>();
                var windowCanvas = window.Root.GetComponent<Canvas>() ?? window.Root.AddComponent<Canvas>();
                show();
                Canvas.ForceUpdateCanvases();
                windowCanvas.overrideSorting = true;
                windowCanvas.sortingOrder = 120 + exploreWindows.IndexOf(window) * 2 + 2;
                canvas.sortingOrder = windowCanvas.sortingOrder - 1;
                // WindowMaskBase.AttachToWindow enables the native canvas after
                // assigning its order, then restores overrideSorting on activation.
                canvas.enabled = true;
                mask.SetActive(true);
                canvas.overrideSorting = true;
                if (mask.GetComponent<GraphicRaycaster>() == null) mask.AddComponent<GraphicRaycaster>();
                var image = mask.GetComponent<RawImage>();
                if (image == null) throw new InvalidOperationException("Original WorldCanvas BlurMask RawImage is not bound.");
                image.texture = snapshot;
                // WindowBlurMask.ExpandImageToFullCanvas and original aspect correction.
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = ((RectTransform)canvas.rootCanvas.transform).rect.size;
                rect.anchoredPosition = Vector2.zero;
                float width = rect.rect.width * snapshot.height / (snapshot.width * rect.rect.height);
                if (!Mathf.Approximately(width, 1)) image.uvRect = new Rect(.5f - .5f * width, 0, width, 1);
                float shown = Time.time;
                mask.GetComponent<Button>().onClick.AddListener(() =>
                {
                    // PopupWindow.CanCloseByMask uses the original half-second gate.
                    if (Time.time - shown > .5f && exploreWindows.Count > 0 && exploreWindows[exploreWindows.Count - 1] == window)
                        CloseExploreWindow(window);
                });
            }
            private static void BlurExploreCapture(RenderTexture capture, RenderTexture target)
            {
                Material material = Resources.Load<Material>("World/Mainline/ScreenBlur");
                if (material == null) throw new InvalidOperationException("Original ScreenBlur material is not bound.");
                var up = new RenderTexture[4];
                var down = new RenderTexture[4];
                RenderTexture source = capture;
                void Blit(RenderTexture from, RenderTexture to, Vector2 direction)
                {
                    material.SetVector("_BlurDir", direction);
                    material.SetVector("_GlobalMipBias", Vector2.zero);
                    material.SetTexture("_BlitTexture", from);
                    material.SetVector("_BlitTexture_TexelSize", new Vector4(1f / from.width, 1f / from.height, from.width, from.height));
                    Graphics.Blit(from, to, material, 0);
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
                    foreach (var value in up) if (value != null) RenderTexture.ReleaseTemporary(value);
                    foreach (var value in down) if (value != null) RenderTexture.ReleaseTemporary(value);
                }
            }
            public void ReleaseExploreMask(OfficialMap11WitchWindow window)
            {
                if (exploreMasks.TryGetValue(window, out var mask)) Destroy(mask);
                exploreMasks.Remove(window);
                if (exploreSnapshots.TryGetValue(window, out var texture)) RenderTexture.ReleaseTemporary(texture);
                exploreSnapshots.Remove(window);
            }
        }
    }
}
