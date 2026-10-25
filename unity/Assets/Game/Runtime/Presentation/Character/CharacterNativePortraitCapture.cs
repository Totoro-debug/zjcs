using System;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.CharacterCreation
{
    [Serializable]
    public sealed class CharacterPortraitPhotography
    {
        public string manifest, animation;
        public int width, height, depth;
        public float size, near, far;
        public float[] offset;
        public int[] excludes;
        public bool captureParticles;

        [Serializable] private sealed class Receipt { public string manifest, output_sha256; }

        public static CharacterPortraitPhotography LoadBattleHead()
        {
            TextAsset asset = Resources.Load<TextAsset>("Combat/battle-portraits");
            TextAsset provenance = Resources.Load<TextAsset>("Combat/battle-portraits-source-manifest");
            if (asset == null || provenance == null) throw new InvalidOperationException("Official battle portrait photography is missing.");
            Receipt receipt = JsonUtility.FromJson<Receipt>(provenance.text);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(asset.bytes)).Replace("-", "").ToLowerInvariant();
            if (receipt == null || receipt.manifest != "854_156383" || receipt.output_sha256 != hash)
                throw new InvalidOperationException("Battle portrait photography differs from its original source.");
            return JsonUtility.FromJson<CharacterPortraitPhotography>(asset.text);
        }
    }

    /// <summary>Official HeadCharacterPortraitBuilder and CharacterPortraitPhotographer capture order,
    /// adapted to the independently recovered native attachment geometry.</summary>
    public static class CharacterNativePortraitCapture
    {
        public static Texture2D Capture(GameObject model, CharacterPortraitPhotography settings, string animationState)
        {
            if (model == null || settings == null || string.IsNullOrEmpty(animationState))
                throw new ArgumentException("A native model, original photography and converted portrait pose are required.");
            if (settings.captureParticles) throw new NotSupportedException("Particle portrait capture requires the original particle bindings.");
            Animator animator = model.GetComponent<Animator>();
            var geometry = model.GetComponent<CharacterNativeAttachmentGeometry>();
            if (animator == null || geometry == null || !animator.HasState(0, Animator.StringToHash(animationState)))
                throw new InvalidOperationException("The official native portrait pose or geometry is missing.");
            animator.Play(animationState, 0, 0);
            animator.Update(0);
            geometry.RefreshForCapture();
            var cameraObject = new GameObject("OfficialCharacterPortraitCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = settings.size;
            camera.aspect = (float)settings.width / settings.height;
            camera.nearClipPlane = settings.near;
            camera.farClipPlane = settings.far;
            camera.transform.SetPositionAndRotation(model.transform.position
                + new Vector3(settings.offset[0], settings.offset[1], settings.offset[2] - 5), Quaternion.identity);
            RenderTexture target = RenderTexture.GetTemporary(settings.width, settings.height, settings.depth,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
            var commands = new CommandBuffer { name = "Official native head photography" };
            Texture2D result = null;
            try
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                // The source uses renderer Z ordering. The native conversion preserves the
                // original slot order in sortingOrder while retaining renderer Z as a tie break.
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()
                    .Where(value => value.enabled && !value.forceRenderingOff && value is MeshRenderer)
                    .OrderBy(value => value.sortingOrder).ThenByDescending(value => value.transform.position.z))
                    for (int index = 0; index < renderer.sharedMaterials.Length; index++)
                        commands.DrawRenderer(renderer, renderer.sharedMaterials[index], index);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    result = new Texture2D(settings.width, settings.height, TextureFormat.RGBA32, false)
                        { name = "Official native character head" };
                    result.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    result.Apply();
                }
                finally { RenderTexture.active = previous; }
                return result;
            }
            catch
            {
                if (result != null) UnityEngine.Object.Destroy(result);
                throw;
            }
            finally
            {
                commands.Dispose();
                RenderTexture.ReleaseTemporary(target);
                if (Application.isPlaying) UnityEngine.Object.Destroy(cameraObject);
                else UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
