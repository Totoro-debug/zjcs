using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public interface IWorldWindowBlurManager
    {
        RenderTexture GetRenderTexture();
        void TakeAShot(RenderTexture target);
        void ReleaseRenderTexture(RenderTexture target);
    }

    // Original WindowBlurMask. The Windows host supplies the frame capture service.
    public sealed class WorldWindowBlurMask : WorldWindowMask
    {
        [SerializeField] private RawImage mImage;
        private readonly Dictionary<WorldPopupWindow, RenderTexture> mWindow2RTDLookup
            = new Dictionary<WorldPopupWindow, RenderTexture>();
        private readonly List<WorldPopupWindow> mExpiredWindowList = new List<WorldPopupWindow>();
        public IWorldWindowBlurManager BlurManager { private get; set; }
        public RawImage Image => mImage;

        public IEnumerator Prepare(WorldPopupWindow window, bool reopenWindowInTop)
        {
            if (!mWindow2RTDLookup.TryGetValue(window, out var value))
            {
                value = BlurManager.GetRenderTexture();
                mWindow2RTDLookup.Add(window, value);
            }
            if (!reopenWindowInTop)
            {
                BlurManager.TakeAShot(value);
                yield return null;
            }
            else Graphics.Blit(mImage.texture, value);
        }

        protected override void OnAttachToWindow(WorldPopupWindow target)
        {
            RemoveExpiredRenderTexture();
            if (!mWindow2RTDLookup.TryGetValue(target, out var value)) return;
            mImage.texture = value;
            ExpandImageToFullCanvas();
            var rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                Rect rect = rectTransform.rect;
                float numerator = rect.width * value.height;
                float denominator = value.width * rect.height;
                if (!Mathf.Approximately(denominator, numerator))
                {
                    Rect uv = mImage.uvRect;
                    uv.width = numerator / denominator;
                    uv.x = .5f - .5f * uv.width;
                    mImage.uvRect = uv;
                }
            }
        }

        private void ExpandImageToFullCanvas()
        {
            if (Canvas == null || Canvas.rootCanvas == null)
            {
                Debug.LogError("WindowBlurMask ExpandToFullScreen failed! Canvas or rootCanvas is null! Canvas == null:" + (Canvas == null));
                return;
            }
            var rect = Canvas.rootCanvas.transform as RectTransform;
            if (rect != null && mImage != null)
            {
                RectTransform image = mImage.rectTransform;
                image.anchorMin = image.anchorMax = new Vector2(.5f, .5f);
                image.sizeDelta = rect.rect.size;
                image.anchoredPosition = Vector2.zero;
            }
        }

        protected override void OnUnattachToWindow(WorldPopupWindow target) => RemoveExpiredRenderTexture();

        private void RemoveExpiredRenderTexture()
        {
            foreach (var window in mWindow2RTDLookup.Keys)
                if (window.State != WorldPopupWindow.EState.Show && window.State != WorldPopupWindow.EState.Showing)
                    mExpiredWindowList.Add(window);
            foreach (var window in mExpiredWindowList)
            {
                if (mWindow2RTDLookup.TryGetValue(window, out var texture))
                {
                    mWindow2RTDLookup.Remove(window);
                    BlurManager.ReleaseRenderTexture(texture);
                }
            }
            mExpiredWindowList.Clear();
        }
    }
}
