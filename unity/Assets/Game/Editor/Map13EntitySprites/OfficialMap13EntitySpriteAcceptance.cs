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

namespace Zjcs.Editor.Map13EntitySprites
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13EntitySpriteAcceptance
    {
        private const string Key = "Map13.NativeEntitySprites";
        private const string SourcePath = "assets/reference/official-map/map13-native-entity-sprites-source-manifest.json";
        private static bool started, error;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13EntitySpriteAcceptance()
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
            { Debug.LogError("Native Map13 Sprite verification timed out."); EditorApplication.Exit(1); return; }
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
            Require(Hash(bytes) == SessionState.GetString(Key + ".source", ""), "Sprite source changed during Play Mode entry.");
            var source = JsonUtility.FromJson<Source>(System.Text.Encoding.UTF8.GetString(bytes));
            foreach (var output in source.outputs)
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository, output.path))) == output.sha256, "Source delivery changed: " + output.path);
            var catalog = Resources.Load<NativeMap13EntitySpriteCatalog>("Map13/Native/entity-sprite-catalog");
            Require(catalog && catalog.sprites.Length == 230 && catalog.renderers.Length == 808 && catalog.packages.Length == 62, "Native Sprite catalog differs.");
            var illumination = new GameObject("Sprite verification illumination").AddComponent<Light2D>();
            illumination.lightType = Light2D.LightType.Global; illumination.intensity = 1;
            var camera = new GameObject("Original native Sprite gallery camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = .6f; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -50); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f, .07f, .07f, 0); camera.farClipPlane = 100;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(160, 160, 24); target.Create(); camera.targetTexture = target;
            var readback = new Texture2D(160, 160, TextureFormat.RGBA32, false);
            var galleries = new List<Texture2D>(); int tiles = 0, textures = 0, mips = 0, bindings = 0, nulls = 0, pixels = 0;
            foreach (var package in source.packages)
            {
                var objects = new List<GameObject>();
                using (var library = new NativeMap13EntitySpriteLibrary(catalog))
                {
                    var bundle = library.Package(package.key);
                    foreach (var expected in package.textures)
                    {
                        var texture = bundle.LoadAsset<Texture2D>(expected.assetName);
                        Require(texture && texture.width == expected.width && texture.height == expected.height && texture.mipmapCount == expected.mips.Length && (int)texture.format == expected.native_format, "Native image dimensions/format/mips differ: " + expected.identity);
                        foreach (var mip in expected.mips)
                        {
                            var request = AsyncGPUReadback.Request(texture, mip.level);
                            while (!request.done) yield return null;
                            Require(!request.hasError && Hash(request.GetData<byte>().ToArray()) == mip.rgba_sha256, "Native image GPU bytes differ: " + expected.identity + "/" + mip.level); mips++;
                        }
                        textures++;
                    }
                    foreach (var expected in source.sprites.Where(s => s.package == package.key))
                    {
                        var sprite = library.Require(expected.key);
                        Require(sprite.vertices.SequenceEqual(expected.vertices) && sprite.triangles.Select(i => (int)i).SequenceEqual(expected.triangles) && sprite.pixelsPerUnit == expected.ppu && sprite.rect == new Rect(expected.rect.x, expected.rect.y, expected.rect.width, expected.rect.height) && sprite.pivot == new Vector2(expected.pivot.x * expected.rect.width, expected.pivot.y * expected.rect.height), "Original native Sprite geometry differs: " + expected.key);
                        Require(sprite.uv.Length == sprite.vertices.Length && sprite.uv.All(v => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y)), "Native Sprite UVs are invalid.");
                        foreach (var entry in catalog.renderers.Where(r => r.spriteKey == expected.key))
                        {
                            var go = new GameObject("Original Sprite source binding " + entry.sourceKey); objects.Add(go);
                            var binding = go.AddComponent<NativeMap13EntitySpriteBinding>(); binding.SourceKey = entry.sourceKey;
                            if (entry.kind == "SpriteMask") binding.Mask = go.AddComponent<SpriteMask>();
                            else binding.Renderer = go.AddComponent<SpriteRenderer>();
                            binding.Bind(library); Require((binding.Renderer ? binding.Renderer.sprite : binding.Mask.sprite) == sprite, "Explicit source Sprite binding differs.");
                            go.SetActive(false); bindings++;
                        }
                        var display = new GameObject("Original native Sprite " + sprite.name); objects.Add(display);
                        var renderer = display.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(expected.material.Substring(6)); Require(renderer.sharedMaterial, "Original Sprite material is missing.");
                        float extent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y); Require(extent > 0, "Original Sprite bounds are empty.");
                        float scale = 1 / extent; display.transform.localScale = Vector3.one * scale; display.transform.position = -sprite.bounds.center * scale;
                        yield return null; camera.Render();
                        var previous = RenderTexture.active; RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 160, 160), 0, 0); readback.Apply(); RenderTexture.active = previous;
                        var colors = readback.GetPixels32(); pixels += colors.Count(c => c.a > 0);
                        int page = tiles / 64, tile = tiles % 64;
                        if (page == galleries.Count) { var gallery = new Texture2D(1280, 1280, TextureFormat.RGBA32, false); gallery.SetPixels32(new Color32[1280 * 1280]); galleries.Add(gallery); }
                        galleries[page].SetPixels32((tile % 8) * 160, (7 - tile / 8) * 160, 160, 160, colors); tiles++;
                        display.SetActive(false);
                    }
                    foreach (var go in objects) UnityEngine.Object.Destroy(go);
                    yield return null;
                }
                Debug.Log("Original native Sprite package verified: " + package.key);
                yield return Resources.UnloadUnusedAssets();
            }
            using (var library = new NativeMap13EntitySpriteLibrary(catalog))
                foreach (var entry in catalog.renderers.Where(r => string.IsNullOrEmpty(r.spriteKey)))
                {
                    var go = new GameObject("Original null Sprite binding"); var binding = go.AddComponent<NativeMap13EntitySpriteBinding>(); binding.SourceKey = entry.sourceKey;
                    if (entry.kind == "SpriteMask") binding.Mask = go.AddComponent<SpriteMask>(); else binding.Renderer = go.AddComponent<SpriteRenderer>();
                    binding.Bind(library); Require(!(binding.Renderer ? binding.Renderer.sprite : binding.Mask.sprite), "Original null Sprite field changed.");
                    UnityEngine.Object.Destroy(go); nulls++; bindings++;
                }
            Require(tiles == 230 && bindings == 808 && nulls == 4 && pixels > 10000, "Original Sprite coverage/rendered pixels differ.");
            for (int page = 0; page < galleries.Count; page++) { galleries[page].Apply(); File.WriteAllBytes(Path.Combine(Output, "native-entity-sprites-" + page + ".png"), galleries[page].EncodeToPNG()); UnityEngine.Object.Destroy(galleries[page]); }
            Require(!error, "Native Sprite execution logged an error.");
            var proof = new Proof { source_sha256 = Hash(bytes), packages = source.packages.Length, sprites = tiles, renderer_bindings = bindings, original_null_bindings = nulls, textures = textures, gpu_mips = mips, rendered_pixels = pixels, backend = SystemInfo.graphicsDeviceType.ToString(), editor = Application.unityVersion, play_mode = Application.isPlaying, coverage = "Original native Sprite resources, GPU mip bytes, geometry, material gallery and explicit component binding only. Full entities, map inputs/save and Windows Player remain pending." };
            File.WriteAllText(Path.Combine(Output, "native-entity-sprites-execution.json"), JsonUtility.ToJson(proof, true));
            Debug.Log("Map13 original native Sprite verification passed: " + tiles + " sprites, " + textures + " textures, " + mips + " GPU mips, " + bindings + " bindings.");
        }
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private sealed class Runner : MonoBehaviour { }
        [Serializable] private sealed class Source { public Package[] packages; public SpriteSpec[] sprites; public OutputSpec[] outputs; }
        [Serializable] private sealed class Package { public string key; public TextureSpec[] textures; }
        [Serializable] private sealed class TextureSpec { public string identity, assetName; public int width, height, native_format; public Mip[] mips; }
        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class SpriteSpec { public string key, package, material; public Vector2[] vertices; public int[] triangles; public float ppu; public RectSpec rect; public Vector2 pivot; }
        [Serializable] private sealed class RectSpec { public float x, y, width, height; }
        [Serializable] private sealed class OutputSpec { public string path, sha256; }
        [Serializable] private sealed class Proof { public string source_sha256, backend, editor, coverage; public int packages, sprites, renderer_bindings, original_null_bindings, textures, gpu_mips, rendered_pixels; public bool play_mode; }
    }
}
