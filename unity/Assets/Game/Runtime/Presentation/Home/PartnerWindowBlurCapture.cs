using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    // Windows overlay-canvas capture for the original BlurManager service port.
    // Resolution, shader expressions and down/up pass order remain official.
    public sealed class PartnerWindowBlurCapture : MonoBehaviour, IWorldWindowBlurManager
    {
        private readonly List<RenderTexture> textures = new List<RenderTexture>();
        private Material material;
        private int width, height;

        public void Initialize(Material source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            material = new Material(source);
            width = Screen.width / 13;
            height = Screen.height / 13;
        }

        public RenderTexture GetRenderTexture()
        {
            var texture = RenderTexture.GetTemporary(width, height);
            textures.Add(texture);
            return texture;
        }

        public void TakeAShot(RenderTexture target) => StartCoroutine(Capture(target));

        private IEnumerator Capture(RenderTexture target)
        {
            yield return new WaitForEndOfFrame();
            if (!textures.Contains(target)) yield break;
            var capture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
            var previous = RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(capture);
                OfficialDungeonPopupMask.Blur(capture, target, material);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(capture);
            }
        }

        public void ReleaseRenderTexture(RenderTexture target)
        {
            if (target == null) return;
            textures.Remove(target);
            RenderTexture.ReleaseTemporary(target);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var texture in textures) RenderTexture.ReleaseTemporary(texture);
            textures.Clear();
            if (material != null) Destroy(material);
        }
    }
}
