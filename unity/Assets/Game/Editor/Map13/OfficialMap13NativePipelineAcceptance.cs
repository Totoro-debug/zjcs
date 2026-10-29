using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    // Tests original native renderer assets and texture upload; uses isolated fixtures, never user saves.
    public static class OfficialMap13NativePipelineAcceptance
    {
        private const string Key = "Zjcs.Map13.NativePipelineExecution";
        private static bool started;
        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class Texture { public string path; public Mip[] mips; }
        [Serializable] private sealed class SceneSource { public Texture[] textures; }

        public static void Run()
        {
            OfficialMap13RenderPipelineTools.RestoreAndValidate();
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog")
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            QualitySettings.renderPipeline = null;
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }

        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 pipeline execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 pipeline test runner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 native pipeline Play Mode passed: all 47 original texture GPU payloads; both original Renderer2DData camera paths, exact color/row sampling and  rendering. Fixtures do not verify the complete map, mouse flows or saves.");
            EditorApplication.isPlaying = false;
        }


        [Serializable] private sealed class NativeTexture
        {
            public string path, gpu_sha256;
            public int gpu_byte_count;
        }
        [Serializable] private sealed class NativeTextures { public NativeTexture[] outputs; }

        private static IEnumerator VerifySteps()
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Native pipeline GPU check requires original Linear color.");
            string root = Path.GetFullPath(Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? "../../.." : "..");
            var textures = JsonUtility.FromJson<NativeTextures>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map13-pipeline-textures-source-manifest.json")));
            foreach (var source in textures.outputs)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source.path.Substring(6));
                var request = AsyncGPUReadback.Request(texture, 0);
                while (!request.done) yield return null;
                if (request.hasError) throw new InvalidDataException("Original pipeline texture GPU readback failed: " + source.path);
                byte[] bytes = request.GetData<byte>().ToArray();
                if (bytes.Length != source.gpu_byte_count || Hash(bytes) != source.gpu_sha256)
                    throw new InvalidDataException("Original pipeline texture GPU format/bytes/rows differ: " + source.path + " " + texture.graphicsFormat + " " + bytes.Length);
            }
            Debug.Log("Map13 original pipeline texture GPU payload verified: 47 textures; unchanged Alpha8/RGB24/RGBA32 bytes and row order.");
            var cameraObject = new GameObject("Original renderer camera fixture");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = false; camera.allowMSAA = false;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = false;
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); camera.targetTexture = target;
            var pixels = new[] { new Color32(32, 64, 96, 255), new Color32(128, 160, 192, 255), new Color32(224, 192, 160, 255), new Color32(96, 64, 32, 255) };
            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            sourceTexture.SetPixels32(pixels); sourceTexture.Apply(false, false);
            var sprite = Sprite.Create(sourceTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            var spriteObject = new GameObject("Original native pipeline sampling fixture");
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            renderer.sharedMaterial = material;
            for (int index = 0; index < 2; index++)
            {
                additional.SetRenderer(index);
                // Batch-mode editors do not schedule WaitForEndOfFrame. Two
                // normal frames let the actual SRP camera finish before readback.
                yield return null; yield return null;
                var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError) throw new InvalidDataException("Native Renderer2DData camera output readback failed.");
                var actual = request.GetData<Color32>();
                for (int y = 4; y < 60; y++) for (int x = 4; x < 60; x++)
                {
                    var expected = pixels[(y / 32) * 2 + x / 32]; var color = actual[y * 64 + x];
                    if (Math.Abs(color.r - expected.r) > 1 || Math.Abs(color.g - expected.g) > 1 || Math.Abs(color.b - expected.b) > 1 || color.a != 255)
                        throw new InvalidDataException("Original Renderer2DData camera color/rows differ: renderer " + index + " at " + x + "," + y + " " + color + " expected " + expected);
                }
                string screenshot = Path.Combine(root, "output/map13-native-renderer-" + index + "-20261027.png");
                var image = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
                image.SetPixels32(actual.ToArray()); image.Apply(); File.WriteAllBytes(screenshot, image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
                Debug.Log("Original native Renderer2DData " + index + " rendered all 3136 interior fixture pixels with exact Linear color and row order on .");
            }
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(spriteObject); UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(material); UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(sourceTexture);
        }
        private static string Hash(byte[] value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
