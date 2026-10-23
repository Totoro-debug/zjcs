using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor.Home
{
    public static class HomeSpriteBindingValidation
    {
        public static void RunAndRender()
        {
            Run();
            HomeAssetCatalog catalog = HomeAssetCatalog.LoadDefault();
            var canvasObject = new GameObject("HomeSpriteRender", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("HomeSpriteCamera", typeof(Camera));
            var target = new RenderTexture(400, 600, 24);
            var capture = new Texture2D(400, 600, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasObject.layer = 31;
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 600;
                camera.aspect = 2f / 3f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.06f, 0.16f, 0.31f, 1f);
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                var renderer = new HomeUiRenderer(catalog);
                HomeUiNode source = Find(catalog.Panel("bed").root, "/FurnitureBedDialog/BgDialog3/BgImg");
                GameObject node = renderer.CreateNode(source, canvasObject.transform);
                foreach (Transform child in node.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                Mask[] masks = node.GetComponentsInChildren<Mask>(true);
                if (masks.Length != 1 || masks[0].showMaskGraphic)
                    throw new InvalidOperationException("The bed panel draws its official invisible stencil mask.");
                RectTransform rect = (RectTransform)node.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(700, 1100);
                rect.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, 400, 600), 0, 0);
                capture.Apply();
                string path = Environment.GetEnvironmentVariable("ZJCS_HOME_SPRITE_CAPTURE");
                if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, capture.EncodeToPNG());
                if (capture.GetPixel(200, 300).r < 0.7f || capture.GetPixel(40, 60).r < 0.7f)
                    throw new InvalidOperationException("The official panel corner is stretched instead of sliced.");
                Debug.Log("Home official nine-slice corner rendered correctly on the graphics device.");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(capture);
                UnityEngine.Object.DestroyImmediate(target);
                catalog.Release();
            }
        }

        [MenuItem("Zjcs/Validate/Home Sprite Borders")]
        public static void Run()
        {
            HomeAssetCatalog catalog = HomeAssetCatalog.LoadDefault();
            GameObject root = new GameObject("HomeSpriteBindingValidation", typeof(RectTransform));
            try
            {
                // A scene or dynamic icon can request the same texture before a
                // panel requests its authored border and pivot.
                const string file = "ImgBg_TipInfo-00747eeea6.png";
                catalog.FindSprite(file);
                HomeUiRenderer renderer = new HomeUiRenderer(catalog);
                HomeUiNode source = Find(catalog.Panel("bed").root, "/FurnitureBedDialog/BgDialog3/BgImg");
                if (source == null) throw new InvalidOperationException("The official bed panel image is missing.");
                source.children = Array.Empty<HomeUiNode>();
                renderer.CreateNode(source, root.transform);
                Image image = renderer.Find<Image>("/FurnitureBedDialog/BgDialog3/BgImg");
                Vector4 expected = new Vector4(101f, 100f, 101f, 99f);
                if (image == null || image.sprite == null || image.type != Image.Type.Sliced
                    || image.sprite.border != expected)
                    throw new InvalidOperationException("The bed panel lost its official nine-slice border: "
                        + (image?.sprite == null ? "missing sprite" : image.sprite.border.ToString()));
                if (image.sprite.pivot != new Vector2(101f, 99.5f))
                    throw new InvalidOperationException("The bed panel lost its official sprite pivot.");
                Debug.Log("Home sprite border and pivot binding validated after an earlier texture-only request.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                catalog.Release();
            }
        }

        private static HomeUiNode Find(HomeUiNode node, string path)
        {
            if (node.path == path) return node;
            foreach (HomeUiNode child in node.children ?? Array.Empty<HomeUiNode>())
            {
                HomeUiNode found = Find(child, path);
                if (found != null) return found;
            }
            return null;
        }
    }
}
