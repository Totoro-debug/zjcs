using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map13
{
    // Actual Play Mode / GPU probe of the manually restored follower view.
    // Passing this checks texture binding and live meshes, not physics parity.
    [InitializeOnLoad]
    public static class OfficialMap13NpcFollowerAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeNpcFollowers";
        private static bool started, failed;
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")
            ?? throw new InvalidOperationException("An explicit validation workspace is required.");
        static OfficialMap13NpcFollowerAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe;
                SessionState.SetBool(Key, false);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
                PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
                int result = SessionState.GetInt(Key + ".result", 1);
                Debug.Log("Native NPC follower validation exit code: " + result);
                EditorApplication.Exit(result);
            };
        }
        public static void Run()
        {
            if (!Directory.Exists(Output)) throw new DirectoryNotFoundException(Output);
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0)) { EditorApplication.Exit(1); return; }
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Native NPC follower verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            if (!failed) SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(value => value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            Application.runInBackground = true;
            var illumination = new GameObject("Original global 2D illumination").AddComponent<Light2D>();
            illumination.lightType = Light2D.LightType.Global;
            var camera = new GameObject("NPC follower camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .08f, .12f, 0); camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            var target = new RenderTexture(512, 512, 24); target.Create(); camera.targetTexture = target;
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            foreach (int id in new[] { 8402, 8440, 8441, 8442 })
            {
                var follower = new GameObject("Follower " + id);
                var view = follower.AddComponent<OfficialMap13NpcFollowerView>();
                view.Initialize(id, SortingLayer.NameToID("Object"), 7);
                Require(view.ModelOffset == new Vector3(0, -.5f, -.5f), "Original follower model position differs.");
                float scale = id == 8402 ? .23f : .3f;
                Require(follower.transform.GetChild(0).localScale == new Vector3(scale, scale, 1), "Original follower model scale differs: " + id);
                yield return null;
                MeshRenderer[] renderers = follower.GetComponentsInChildren<MeshRenderer>();
                Require(renderers.Length > 0, "No native NPC draw batches: " + id);
                foreach (var renderer in renderers)
                {
                    Require(renderer.sharedMaterial && renderer.sharedMaterial.mainTexture && renderer.sharedMaterial.mainTexture != Texture2D.whiteTexture,
                        "NPC atlas is absent or replaced with white: " + id);
                    Require(renderer.sharedMaterial.shader.isSupported, "Original NPC shader is unsupported: " + id);
                    Require(renderer.sortingLayerID == SortingLayer.NameToID("Object") && renderer.sortingOrder >= 7, "NPC sorting differs: " + id);
                }
                var filters = follower.GetComponentsInChildren<MeshFilter>();
                Vector3[][] before = filters.Select(value => value.sharedMesh.vertices).ToArray();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                camera.transform.position = bounds.center + Vector3.back * 10;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f;
                for (int frame = 0; frame < 30; frame++) yield return null;
                Require(filters.Where((value, index) => !value.sharedMesh.vertices.SequenceEqual(before[index])).Any(), "NPC idle meshes did not animate: " + id);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(Output, "npc-" + id + ".png"), image.EncodeToPNG());
                Color32[] pixels = image.GetPixels32();
                Color32 background = pixels[0];
                bool Painted(Color32 value) => value.a > 40 && Math.Abs(value.r - background.r) + Math.Abs(value.g - background.g) + Math.Abs(value.b - background.b) > 24;
                int visible = pixels.Count(Painted);
                int colored = pixels.Count(value => Painted(value) && Mathf.Max(value.r, value.g, value.b) - Mathf.Min(value.r, value.g, value.b) > 15);
                int pink = pixels.Count(value => value.r > 245 && value.g < 15 && value.b > 245);
                Require(visible > 1000 && colored > 300, "Original NPC texture did not produce a visible colored image: " + id + ", " + visible + "/" + colored);
                Require(pink < visible / 100, "NPC frame contains shader-error magenta: " + id);
                Debug.Log("Native NPC follower " + id + ": meshes=" + renderers.Length + ", visiblePixels=" + visible + ", coloredPixels=" + colored);
                UnityEngine.Object.Destroy(follower);
                yield return null;
            }
            camera.targetTexture = null;
            target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
            Debug.Log("Four native NPC followers passed atlas, sorting, animated mesh and GPU image checks. Full constraint/physics parity requires additional verification.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private sealed class Runner : MonoBehaviour { }
    }
}
