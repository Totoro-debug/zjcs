using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Official.World.Map13EntityRendering;

namespace Zjcs.Editor.Map13EntityRendering
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13SlicedRendererAcceptance
    {
        private const string Key = "Map13.NativeSlicedRenderers";
        private const string SourcePath = "assets/reference/official-map/map13-entity-rendering-source-manifest.json";
        private static bool started, error;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13SlicedRendererAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
                PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
                EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetString(Key + ".source", Hash(File.ReadAllBytes(Path.Combine(Repository, SourcePath))));
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Native Map13 sliced-renderer verification timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original Map13 native Sprite resource verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe;
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count != 0)
            {
                object next;
                try
                {
                    var step = stack.Peek(); if (!step.MoveNext()) { stack.Pop(); continue; }
                    next = step.Current;
                    if (next is IEnumerator nested && !(next is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (!error) SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            byte[] bytes = File.ReadAllBytes(Path.Combine(Repository, SourcePath));
            Require(Hash(bytes) == SessionState.GetString(Key + ".source", ""), "Sliced renderer source changed.");
            var source = JsonUtility.FromJson<Source>(System.Text.Encoding.UTF8.GetString(bytes));
            foreach (var output in source.outputs)
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository, output.path))) == output.sha256, "Sliced source delivery changed: " + output.path);
            var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-entity-sliced-renderers.json"));
            Require(recipe.renderers.Length == 84, "Original sliced renderer inventory differs.");
            var catalog = Resources.Load<NativeMap13EntitySpriteCatalog>("Map13/Native/entity-sprite-catalog");
            var service = new GameObject("Offline original sliced renderer service owner").AddComponent<ServiceOwner>();
            var light = new GameObject("Sliced renderer verification illumination").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
            var camera = new GameObject("Native sliced renderer diagnostic camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = .6f; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -50); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f, .07f, .07f, 0); camera.farClipPlane = 100;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(128, 128, 24); target.Create(); camera.targetTexture = target;
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var full = new Texture2D(1536, 1024, TextureFormat.RGBA32, false); var half = new Texture2D(1536, 1024, TextureFormat.RGBA32, false);
            full.SetPixels32(new Color32[1536 * 1024]); half.SetPixels32(new Color32[1536 * 1024]);
            int poses = 0, meshFields = 0, pixels = 0, index = 0;
            using (var library = new NativeMap13EntitySpriteLibrary(catalog))
            foreach (var record in recipe.renderers)
            {
                var go = new GameObject("Original sliced renderer " + record.key); go.SetActive(false);
                var renderer = go.AddComponent<MeshRenderer>(); var filter = go.AddComponent<MeshFilter>();
                var component = go.AddComponent<SlicedFilledRender>(); var fields = record.fields;
                var serialized = new SerializedObject(component);
                serialized.FindProperty("mMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(record.material.Substring(6));
                serialized.FindProperty("mSize").vector2Value = fields.mSize;
                serialized.FindProperty("mColor").colorValue = fields.mColor;
                serialized.FindProperty("mFillMode").intValue = fields.mFillMode;
                serialized.FindProperty("mFillOrigin").intValue = fields.mFillOrigin;
                serialized.FindProperty("mAnticlockwise").boolValue = fields.mAnticlockwise != 0;
                serialized.FindProperty("mFillAmount").floatValue = fields.mFillAmount;
                serialized.ApplyModifiedPropertiesWithoutUndo(); component.OnAfterDeserialize();
                var sprite = library.Package(record.package).LoadAsset<Sprite>(record.assetName); Require(sprite, "Original sliced image is absent.");
                component.BindOriginalSprite(sprite); component.enabled = fields.m_Enabled != 0;
                go.SetActive(true); yield return WaitForRefresh(component);
                Require(filter.sharedMesh && renderer.sharedMaterial && !Dirty(component), "Original Awake/late refresh did not execute: " + record.key + "/mesh=" + (bool)filter.sharedMesh + "/material=" + (bool)renderer.sharedMaterial + "/dirty=" + Dirty(component));
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); Require(block.GetTexture("_MainTex") == sprite.texture, "Original property-block atlas binding differs.");
                component.FillAmount = 1; yield return WaitForRefresh(component); float area = Area(filter.sharedMesh); Require(area > 0, "Original full sliced mesh is empty.");
                var fullBounds = renderer.bounds;
                float last = area;
                foreach (float fill in new[] { 1f, .75f, .5f, .25f, .01f, 0f })
                {
                    component.FillAmount = fill; yield return WaitForRefresh(component);
                    Require(!Dirty(component), "Original service did not consume dirty refresh.");
                    var mesh = filter.sharedMesh; float current = Area(mesh);
                    Require(current >= 0 && current <= last + 1e-6f, "Sliced mesh area is not monotonic.");
                    if (fill == 0) Require(mesh.vertexCount == 0, "Original zero fill did not clear mesh.");
                    else
                    {
                        Require(mesh.vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z)) && mesh.uv.All(v => Finite(v.x) && Finite(v.y)), "Original sliced mesh has nonfinite data.");
                        Require(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), "Original sliced mesh triangle index differs.");
                        Require(mesh.colors32.All(c => c.Equals((Color32)fields.mColor.linear)), "Original sliced linear color differs.");
                        if (fields.mFillMode != 4) Require(Mathf.Abs(current / area - fill) < 1e-4f, "Original linear fill area differs.");
                    }
                    last = current; poses++; meshFields += mesh.vertexCount * 6 + mesh.triangles.Length;
                    if (fill == 1 || fill == .5f)
                    {
                        camera.transform.position = fullBounds.center + new Vector3(0, 0, -50); camera.orthographicSize = Mathf.Max(fullBounds.extents.x, fullBounds.extents.y) * 1.15f;
                        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); RenderTexture.active = previous;
                        var colors = image.GetPixels32(); pixels += colors.Count(c => c.a > 0);
                        (fill == 1 ? full : half).SetPixels32((index % 12) * 128, (7 - index / 12) * 128, 128, 128, colors);
                    }
                }
                // Property setters preserve clamping and dirty scheduling.
                component.FillAmount = -1; Require(component.FillAmount == 0, "Negative fill was not clamped.");
                component.FillAmount = 2; Require(component.FillAmount == 1, "Excess fill was not clamped.");
                yield return WaitForRefresh(component); Require(Mathf.Abs(Area(filter.sharedMesh) - area) < 1e-6f, "Dirty refresh failed after clamping.");
                go.SetActive(false); go.SetActive(true); yield return null; Require(filter.sharedMesh && Mathf.Abs(Area(filter.sharedMesh) - area) < 1e-6f, "Sliced mesh was lost on reactivation.");
                UnityEngine.Object.Destroy(go); yield return null; index++;
            }
            Require(pixels > 10000 && !error, "Original sliced renderer execution produced insufficient pixels/errors.");
            full.Apply(); half.Apply(); File.WriteAllBytes(Path.Combine(Output, "native-sliced-full.png"), full.EncodeToPNG()); File.WriteAllBytes(Path.Combine(Output, "native-sliced-half.png"), half.EncodeToPNG());
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(full); UnityEngine.Object.Destroy(half); UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(service.gameObject);
            var proof = new Proof { source_sha256 = Hash(bytes), components = index, sampled_meshes = poses, mesh_fields = meshFields, rendered_pixels = pixels, backend = SystemInfo.graphicsDeviceType.ToString(), editor = Application.unityVersion, play_mode = Application.isPlaying, coverage = "Original sliced renderer lifecycle, original 84 configurations, native Sprite/atlas material binding, linear/radial fill meshes, dirty service LateUpdate and enable/disable. Diagnostic source-size objects only; full native prefab, entity EC/input/save and Windows Player remain pending." };
            File.WriteAllText(Path.Combine(Output, "native-sliced-execution.json"), JsonUtility.ToJson(proof, true));
            Debug.Log("Map13 original sliced renderer passed: " + index + " components, " + poses + " meshes, " + meshFields + " finite fields.");
        }
        private static bool Dirty(SlicedFilledRender target) => (bool)typeof(SlicedFilledRender).GetField("mDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(target);
        private static IEnumerator WaitForRefresh(SlicedFilledRender target)
        {
            // The coroutine resumes in Update; the original service consumes
            // its dirty queue in LateUpdate later in that frame.
            for (int frames = 0; Dirty(target); frames++)
            { Require(frames < 8, "Original sliced service did not run LateUpdate."); yield return null; }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Area(Mesh mesh)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles; float area = 0;
            for (int i = 0; i < triangles.Length; i += 3) area += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * .5f;
            return area;
        }
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private sealed class Runner : MonoBehaviour { }
        private sealed class ServiceOwner : MonoBehaviour { private void LateUpdate() { SlicedFilledRenderService.Current.LateUpdate(); } }
        [Serializable] private sealed class Source { public OutputSpec[] outputs; }
        [Serializable] private sealed class OutputSpec { public string path, sha256; }
        [Serializable] private sealed class Recipe { public Record[] renderers; }
        [Serializable] private sealed class Record { public string key, material, package, assetName; public Fields fields; }
        [Serializable] private sealed class Fields { public int m_Enabled, mFillMode, mFillOrigin, mAnticlockwise; public Vector2 mSize; public Color mColor; public float mFillAmount; }
        [Serializable] private sealed class Proof { public string source_sha256, backend, editor, coverage; public int components, sampled_meshes, mesh_fields, rendered_pixels; public bool play_mode; }
    }
}
