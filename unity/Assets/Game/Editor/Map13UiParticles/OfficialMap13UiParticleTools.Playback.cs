using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Coffee.UIExtensions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Map13UiParticles
{
    public static partial class OfficialMap13UiParticleTools
    {
        private const string PlaybackKey = "Zjcs.Map13.OriginalUiParticlePlayback";
        private static bool playbackStarted, playbackError;
        public static void RunPlayback() => RunPlaybackCore(false);
        public static void RunTextures() => RunPlaybackCore(true);
        private static void RunPlaybackCore(bool texturesOnly)
        {
            try
            {
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
                ShaderUtil.allowAsyncCompilation = false;
                BindAndValidate();
                SessionState.SetString(PlaybackKey + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
                SessionState.SetString(PlaybackKey + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                SessionState.SetInt(PlaybackKey + ".color", (int)PlayerSettings.colorSpace);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
                if (!GraphicsSettings.defaultRenderPipeline) throw new InvalidDataException("Map13 original UI effect pipeline is unbound.");
                QualitySettings.renderPipeline = null;
                PlayerSettings.colorSpace = ColorSpace.Linear;
                SessionState.SetBool(PlaybackKey + ".texturesOnly", texturesOnly);
                SessionState.SetBool(PlaybackKey, true);
                SessionState.SetInt(PlaybackKey + ".result", 1);
                SessionState.SetFloat(PlaybackKey + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                HookPlayback();
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        [InitializeOnLoadMethod]
        private static void RestorePlayback() { if (SessionState.GetBool(PlaybackKey, false)) HookPlayback(); }
        private static void HookPlayback()
        {
            playbackStarted = false;
            EditorApplication.update -= TickPlayback;
            EditorApplication.update += TickPlayback;
            EditorApplication.playModeStateChanged -= PlaybackModeChanged;
            EditorApplication.playModeStateChanged += PlaybackModeChanged;
        }
        private static void PlaybackModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PlaybackKey, false)) return;
            SessionState.SetBool(PlaybackKey, false);
            EditorApplication.update -= TickPlayback;
            EditorApplication.playModeStateChanged -= PlaybackModeChanged;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(PlaybackKey + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(PlaybackKey + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(PlaybackKey + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(PlaybackKey + ".result", 1));
        }
        private static void TickPlayback()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(PlaybackKey + ".deadline", 0))
            {
                Debug.LogError("Map13 original UI particle playback timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (playbackStarted || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            playbackStarted = true;
            new GameObject("Map13OriginalParticlePlayback").AddComponent<PlaybackRunner>().StartCoroutine(Playback());
        }
        public sealed class PlaybackRunner : MonoBehaviour { }
        private static void ObservePlaybackError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) playbackError = true;
        }
        private static IEnumerator Playback()
        {
            IEnumerator steps = SessionState.GetBool(PlaybackKey + ".texturesOnly", false) ? TextureSteps() : PlaybackSteps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(PlaybackKey + ".result", 0);
            Debug.Log(SessionState.GetBool(PlaybackKey + ".texturesOnly", false) ? "Map13 original UI particle texture GPU checks passed." : "Map13 original UI particle Play Mode passed: 8 authored hierarchies, 80 emitting systems, 32 Coffee mesh outputs at two portrait scales. Component playback only; business triggers remain separate.");
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class TextureMip { public int level, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class TextureCheck { public string path, profile; public int native_format; public TextureMip[] mips; }
        [Serializable] private sealed class TextureChecks { public TextureCheck[] textures; }
        private static IEnumerator TextureSteps()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json"))) directory = directory.Parent;
            if (directory == null) throw new InvalidDataException("Official source root is missing.");
            var data = JsonUtility.FromJson<TextureChecks>(File.ReadAllText(Path.Combine(directory.FullName,
                "assets/reference/official-map/map13-native-ui-particle-assets-source-manifest.json")));
            if (data.textures.Length != 17) throw new InvalidDataException("Original UI particle texture closure differs.");
            int count = 0;
            foreach (var source in data.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source.path.Substring(6));
                if (!texture || texture.format != (TextureFormat)source.native_format || texture.mipmapCount != source.mips.Length)
                    throw new InvalidDataException("Original particle native texture format/mips differ: " + source.path);
                foreach (var mip in source.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    while (!request.done) yield return null;
                    if (request.hasError) throw new InvalidDataException("Particle texture GPU readback failed: " + source.path);
                    var bytes = request.GetData<byte>().ToArray();
                    string hash;
                    using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    if (bytes.Length != mip.rgba_size || hash != mip.rgba_sha256)
                        throw new InvalidDataException("Original particle mip GPU bytes/rows differ: " + source.path + " mip " + mip.level);
                    count++;
                }
            }
            Debug.Log("Map13 original UI particle textures: 17 textures, " + count + " original mip GPU payloads and row order verified.");
        }
        private static IEnumerator PlaybackSteps()
        {
            playbackError = false;
            Application.logMessageReceived += ObservePlaybackError;
            Application.runInBackground = true;
            foreach (var startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(c => c.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (var boot in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas)) UnityEngine.Object.Destroy(boot.gameObject);
            yield return null;
            var common = Resources.Load<TMP_SpriteAsset>("Map13/UI/Common");
            MaterialReferenceManager.AddSpriteAsset(common.hashCode, common);
            MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetSimpleHashCode(common.name), common);
            var data = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
            string output = Path.GetFullPath("../.tools/map13-ui-particle-playback");
            Directory.CreateDirectory(output);
            var camera = new GameObject("Map13ParticleCaptureCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 5;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(1);
            foreach (int width in new[] { 450, 720 })
            {
                int height = width == 450 ? 800 : 1280;
                SetPlaybackSize(width, height);
                float deadline = Time.realtimeSinceStartup + 10;
                while (Screen.width != width || Screen.height != height)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidDataException("Particle GameView failed to resize.");
                    yield return null;
                }
                var frame = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                frame.Create(); camera.targetTexture = frame; camera.aspect = (float)width / height;
                var host = new GameObject("Map13ParticleFixtureCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(900, 1600); scaler.matchWidthOrHeight = 1;
                foreach (string panel in data.carriers.Select(c => c.panel).Distinct())
                {
                    var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map13/UI/" + panel), host.transform, false);
                    var rootCanvas = root.GetComponent<Canvas>();
                    if (rootCanvas)
                    {
                        root.transform.SetParent(null, false);
                        rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                        rootCanvas.worldCamera = camera; rootCanvas.planeDistance = 1;
                        var rootScaler = root.GetComponent<CanvasScaler>();
                        if (!rootScaler) rootScaler = root.AddComponent<CanvasScaler>();
                        rootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        rootScaler.referenceResolution = scaler.referenceResolution; rootScaler.matchWidthOrHeight = scaler.matchWidthOrHeight;
                    }
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
                    foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true)) c.worldCamera = camera;
                    var world = root.GetComponent<WorldPanel>();
                    if (world) world.Initialize(false);
                    // AwardListPanelItemShow holds its optional particle inactive.
                    // UI animation/input has its own fixture; this component probe
                    // explicitly opens each effect state without animation overwrites.
                    foreach (var animation in root.GetComponentsInChildren<Animation>(true))
                    { animation.Stop(); animation.enabled = false; }
                    // Probe each authored effect state. State activation belongs to the
                    // fixture; transforms, particle modules and original renderer data stay intact.
                    var effects = data.coffee.Where(c => c.panel == panel).Select(c => NodeAt(root, c.indices).GetComponent<UIParticle>()).ToArray();
                    foreach (var effect in effects)
                    {
                        for (Transform t = effect.transform; t && t != host.transform; t = t.parent) t.gameObject.SetActive(true);
                        foreach (var ps in effect.particles)
                            for (Transform t = ps.transform; t && t != host.transform; t = t.parent) t.gameObject.SetActive(true);
                        effect.Play();
                    }
                    var systems = data.carriers.Where(c => c.panel == panel).Select(c => NodeAt(root, c.indices).GetComponent<ParticleSystem>()).ToArray();
                    var emitted = new HashSet<ParticleSystem>();
                    var meshed = new HashSet<UIParticle>();
                    float start = Time.time;
                    bool capturedEarly = false;
                    while (Time.time - start < 4.0f)
                    {
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        foreach (var ps in systems) if (ps.particleCount > 0) emitted.Add(ps);
                        foreach (var effect in effects)
                            if (effect.GetComponentsInChildren<CanvasRenderer>(true).Any(c => c.GetMesh() && c.GetMesh().vertexCount > 0)) meshed.Add(effect);
                        if (!capturedEarly && Time.time - start >= .2f)
                        {
                            yield return new WaitForEndOfFrame();
                            CapturePlayback(camera, frame, Path.Combine(output, width + "-" + panel + "-early.png"));
                            capturedEarly = true;
                        }
                    }
                    yield return new WaitForEndOfFrame();
                    CapturePlayback(camera, frame, Path.Combine(output, width + "-" + panel + "-late.png"));
                    if (emitted.Count != systems.Length || meshed.Count != effects.Length)
                        throw new InvalidDataException("Original particle playback incomplete: " + panel + " emitted=" + emitted.Count + "/" + systems.Length + " meshes=" + meshed.Count + "/" + effects.Length + " missing=" + string.Join(";", systems.Where(p => !emitted.Contains(p)).Select(p => p.name + ":active=" + p.gameObject.activeInHierarchy + ":time=" + p.time + ":delay=" + p.main.startDelay.constant + ":bursts=" + p.emission.burstCount + ":rate=" + p.emission.rateOverTime.constant + ":alpha=" + p.GetComponentInParent<UIParticle>().canvasRenderer.GetInheritedAlpha())));
                    Debug.Log("Map13 particle playback " + width + " " + panel + ": " + emitted.Count + " systems, " + meshed.Count + " Coffee meshes.");
                    UnityEngine.Object.Destroy(root);
                    yield return null;
                    if (playbackError) throw new InvalidDataException("Original particle playback emitted a runtime error.");
                }
                UnityEngine.Object.Destroy(host); camera.targetTexture = null; frame.Release(); UnityEngine.Object.Destroy(frame);
                yield return null;
            }
            Application.logMessageReceived -= ObservePlaybackError;
        }
        private static void CapturePlayback(Camera camera, RenderTexture frame, string path)
        {
            Canvas.ForceUpdateCanvases();
            var previous = RenderTexture.active; RenderTexture.active = frame;
            var image = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); image.Apply();
            if (image.GetPixels32().Count(p => p.r > 220 && p.b > 220 && p.g < 20) > 50)
                throw new InvalidDataException("Map13 particle output contains the magenta error shader: " + path);
            File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.Destroy(image); RenderTexture.active = previous;
        }
        private static void SetPlaybackSize(int width, int height)
        {
            var editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, "Map13 particle fixture" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type type = editor.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(type, false, "Game", false);
            type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
            view.Repaint();
        }
    }
}
