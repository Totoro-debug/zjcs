using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.CharacterCreation
{
    internal sealed class CharacterCreationNativeModelPreview : IDisposable
    {
        private readonly GameObject worldRoot;
        private readonly GameObject modelRoot;
        private readonly Camera camera;
        private readonly RawImage image;
        private readonly RenderTexture renderTexture;
        private readonly CharacterCreationMeshFramePlayer framePlayer;

        public CharacterCreationNativeModelPreview(Transform owner, RectTransform viewport, string label, int layer)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (viewport == null) throw new ArgumentNullException(nameof(viewport));

            foreach (Component component in viewport.GetComponentsInChildren<Component>(true))
                if (component != null && component.GetType().Name == "SkeletonGraphic" && component is Behaviour behaviour)
                    behaviour.enabled = false;

            var imageObject = new GameObject("NativeCharacterPreview", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(viewport, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            AspectRatioFitter aspect = imageObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 2f / 3f;

            worldRoot = new GameObject("NativeCharacterPreviewWorld_" + label);
            worldRoot.transform.SetParent(owner, false);
            worldRoot.transform.localPosition = new Vector3(layer * 4, 0, 0);
            modelRoot = new GameObject(label + "NativeModel");
            modelRoot.transform.SetParent(worldRoot.transform, false);
            modelRoot.name = label + "NativeModel";
            SetLayerRecursively(modelRoot, layer);

            framePlayer = modelRoot.AddComponent<CharacterCreationMeshFramePlayer>();
            framePlayer.Initialize(label.ToLowerInvariant());

            const int width = 512;
            const int height = 768;
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "CharacterCreationPreview_" + label,
                hideFlags = HideFlags.DontSave
            };
            renderTexture.Create();
            image.texture = renderTexture;

            var cameraObject = new GameObject("NativeCharacterPreviewCamera_" + label);
            cameraObject.transform.SetParent(worldRoot.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.orthographic = true;
            camera.aspect = (float)width / height;
            camera.cullingMask = 1 << layer;
            camera.targetTexture = renderTexture;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.depth = -10;
            FitCamera(camera, modelRoot);
            SetVisible(false);
            ApplySkins(Array.Empty<string>());
        }

        public void SetVisible(bool visible)
        {
            if (worldRoot != null) worldRoot.SetActive(visible);
        }

        public void ApplySkins(IEnumerable<string> skinNames)
        {
            framePlayer.ApplySkins(skinNames ?? Array.Empty<string>());
            FitCamera(camera, modelRoot);
        }

        public void ApplyDyeChannels(IReadOnlyDictionary<int, Vector4[]> changesBySkinType)
        {
            framePlayer.ApplyDye(changesBySkinType);
        }

        public void Play(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return;
            framePlayer.Play(animationName);
            FitCamera(camera, modelRoot);
        }

        public void Dispose()
        {
            if (image != null) image.texture = null;
            if (camera != null) camera.targetTexture = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
            if (worldRoot != null) UnityEngine.Object.Destroy(worldRoot);
            if (image != null) UnityEngine.Object.Destroy(image.gameObject);
        }

        private static void FitCamera(Camera previewCamera, GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) bounds = new Bounds(root.transform.position, new Vector3(2, 4, 0.1f));
            previewCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / previewCamera.aspect) * 1.08f;
            previewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y,
                bounds.center.z - Mathf.Max(bounds.extents.z + 10, 20));
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

    }
}
