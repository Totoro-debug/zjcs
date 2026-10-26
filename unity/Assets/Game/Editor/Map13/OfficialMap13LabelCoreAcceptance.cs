using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeLabelUI.Common;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13LabelCoreAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeLabelCoreExecution";
        private static bool started;
        private static readonly List<string> errors = new List<string>();
        public static void Run()
        {
            ValidateSources();
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog")
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            QualitySettings.renderPipeline = null;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [Serializable] private sealed class FileRecord { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Source { public FileRecord[] outputs; public FileRecord evidence; }
        public static void ValidateSources()
        {
            string root = OfficialMap13FlagTools.Repository;
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-label-core-code-source-manifest.json")));
            foreach (var record in source.outputs.Concat(new[] { source.evidence }))
            {
                if (OfficialMap13TextTools.Hash(File.ReadAllBytes(Path.Combine(root, record.path))) != record.sha256 ||
                    OfficialMap13TextTools.Hash(File.ReadAllBytes(record.path.Substring(6))) != record.sha256)
                    throw new InvalidDataException("Original label core source differs: " + record.path);
                if (!string.IsNullOrEmpty(record.meta_sha256) && OfficialMap13TextTools.Hash(File.ReadAllBytes(record.path.Substring(6) + ".meta")) != record.meta_sha256)
                    throw new InvalidDataException("Original label core MonoScript identity differs: " + record.path);
            }
            Debug.Log("Original label core pinned C#/metadata/native evidence and MonoScript identities verified.");
        }
        public static void ValidateProject()
        {
            ValidateSources();
            var tools = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Zjcs.Editor.ProjectTools", false)).FirstOrDefault(t => t != null);
            if (tools == null) throw new InvalidDataException("Global project validator is unavailable.");
            tools.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false; EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original label core execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original label core fixture").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + trace); }
        private static IEnumerator Verify()
        {
            errors.Clear(); Application.logMessageReceived += Log;
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error) { Application.logMessageReceived -= Log; Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            Application.logMessageReceived -= Log;
            if (errors.Count != 0) { Debug.LogError("Label core lifecycle logged errors: " + string.Join("\n", errors)); EditorApplication.isPlaying = false; yield break; }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Original label core Play Mode passed: shared state nodes and callback order, multilingual registration/language changes/unregistration, raw/localized processors and formatting, original gradient vertex colors, linear color proxy and real font/effect refresh. Original 88 label prefabs and formal world services remain separate.");
            EditorApplication.isPlaying = false;
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException(message); }
        private static void Field(object target, string name, object value)
        { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
        private static TMP_FontAsset CloneFont(TMP_FontAsset original)
        {
            var font = UnityEngine.Object.Instantiate(original); font.material = UnityEngine.Object.Instantiate(original.material);
            font.atlasTextures = new[] { UnityEngine.Object.Instantiate(original.atlasTexture) }; font.material.mainTexture = font.atlasTexture;
            font.ReadFontAssetDefinition(); return font;
        }
        private static void DestroyFont(TMP_FontAsset font)
        { UnityEngine.Object.Destroy(font.material); UnityEngine.Object.Destroy(font.atlasTexture); UnityEngine.Object.Destroy(font); }
        private static IEnumerator VerifySteps()
        {
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 && QualitySettings.activeColorSpace == ColorSpace.Linear, "Label fixture requires Linear DX12.");
            VerifyStates();
            var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OfficialMap13TextTools.FontPath);
            var font = CloneFont(original); var replacementFont = CloneFont(original);
            var language = new FixtureLanguage(); var fonts = new FixtureFonts { Font = font };
            using (var service = new MultiLanguageUIService(language))
            {
                var go = new GameObject("Original multilingual label fixture", typeof(RectTransform)); go.SetActive(false);
                var text = go.AddComponent<TextMeshPro>();
                var serialized = new SerializedObject(text); serialized.FindProperty("m_fontAsset").objectReferenceValue = font;
                serialized.FindProperty("m_sharedMaterial").objectReferenceValue = font.material; serialized.ApplyModifiedPropertiesWithoutUndo();
                text.text = ""; text.fontSize = 40; text.enableWordWrapping = false; text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(600, 150);
                var label = go.AddComponent<MultiLanguageText>();
                label.BindLanguageServices(service, language); label.LocalizationTextService = new FixtureLocalization(); label.MultiLanguageFontService = fonts;
                label.myKey = "fixture-name";
                go.SetActive(true); yield return null; yield return null;
                Require(label.Text == "泽之国 海之宫", "Original multilingual Start did not refresh the bound language.");
                language.CurrentLanguage = LanguageType.en_US;
                Require(label.Text == "Fixture Marsh", "Original registered language callback did not refresh.");
                int refreshes = 0; label.OnTextRefresh += () => refreshes++;
                var processors = new List<string>();
                label.SetKey("fixture-format", new FixtureProcessor(processors, "raw", s => "[" + s + "]"), new FixtureProcessor(processors, "localized", s => s + "!"), 65);
                Require(label.Text == "[Level 65]!" && string.Join(",", processors) == "raw:Level {0},localized:[Level 65]" && refreshes == 1, "Original processor/format/event order differs.");
                label.Format(66); Require(label.Text == "Level 66" && refreshes == 2, "Original Format did not replace processors/arguments.");
                label.myKey = ""; label.Refresh(LanguageType.zh_CN); Require(refreshes == 2, "Empty original key emitted a refresh event.");
                Require(MultiLanguageText.GetKeyBySplit(0, "/") == null && MultiLanguageText.GetKeyBySplit(3, "/") == "{0}/{1}/{2}", "Original split format differs.");
                label.SetKey("fixture-name"); language.CurrentLanguage = LanguageType.zh_CN;
                var effect = go.AddComponent<TMPEffect>(); effect.outline = true; effect.outlineWidth = 2; effect.SetupEffect(true);
                int changes = 0;
                label.OnFontChanged += () => { Require(text.font == replacementFont, "Original font event ran before assignment."); changes++; };
                fonts.Font = replacementFont; label.Refresh(language.CurrentLanguage);
                Require(changes == 1 && text.font == replacementFont && text.fontSharedMaterial != replacementFont.material && text.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON"), "Original font replacement did not rebuild the effect.");
                label.Refresh(language.CurrentLanguage); Require(changes == 1, "Unchanged original font emitted an event.");
                var gradient = go.AddComponent<Zjcs.Official.World.NativeLabelUI.TMPColorGradient>(); gradient.Angle = 0;
                gradient.SetColors(new List<Color> { Color.red, Color.green, Color.blue });
                Require(gradient.Gradient.colorKeys.Length == 3 && Math.Abs(gradient.Gradient.colorKeys[1].time - .5f) < .00002f && gradient.Gradient.alphaKeys.All(k => k.alpha == 1), "Original gradient keys/alpha spacing differ.");
                label.SetColor(new Color(.5f, .25f, .75f, 1));
                Require(!gradient.enabled && text.color == new Color(.5f, .25f, .75f, 1), "Original solid color did not disable gradient or added conversion.");
                var requested = new Gradient { mode = GradientMode.Fixed };
                requested.SetKeys(new[] { new GradientColorKey(Color.red, 0), new GradientColorKey(Color.blue, 1) }, new[] { new GradientAlphaKey(.3f, 0), new GradientAlphaKey(.9f, 1) });
                label.SetColor(requested);
                Require(gradient.enabled && gradient.Gradient.mode == GradientMode.Blend, "Original gradient setter unexpectedly copied mode.");
                text.text = "泽之国\n海之宫"; text.ForceMeshUpdate(); yield return null;
                Require(text.textInfo.lineCount == 2, "Gradient fixture did not span two lines.");
                int checkedVertices = 0;
                foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
                {
                    if (!character.isVisible) continue;
                    var line = text.textInfo.lineInfo[character.lineNumber]; var mesh = text.textInfo.meshInfo[character.materialReferenceIndex];
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int index = character.vertexIndex + corner; float position = mesh.vertices[index].x;
                        float t = line.lineExtents.min.x == line.lineExtents.max.x ? 0 : Math.Max(0, Math.Min(1, (position - line.lineExtents.min.x) / (line.lineExtents.max.x - line.lineExtents.min.x)));
                        Color32 expected = gradient.Gradient.Evaluate(t);
                        Require(mesh.colors32[index].Equals(expected), "Original gradient callback vertex colors differ."); checkedVertices++;
                    }
                }
                Require(checkedVertices == 24, "Original gradient fixture vertex coverage differs.");
                label.SetColor(Color.white);
                var proxy = go.AddComponent<TextMeshProColorProxy>(); Field(proxy, "mTextMeshPro", text);
                var color = new Color(.5f, .25f, .75f, .6f); proxy.Color = color;
                Require(proxy.Color == color && text.color == color.linear, "Original color proxy linear conversion/storage differs.");
                proxy.SetColor(Color.red); Require(proxy.Color == color && text.color == Color.red.linear, "Original direct SetColor unexpectedly changed stored color.");
                var corners = new ColorGradientData { TopLeft = new Color(.2f, .4f, .6f), TopRight = Color.red, BottomLeft = Color.green, BottomRight = Color.blue };
                proxy.SetColorGradientData(corners);
                Require(text.colorGradient.topLeft == corners.TopLeft.linear && text.colorGradient.topRight == corners.TopRight.linear && text.colorGradient.bottomLeft == corners.BottomLeft.linear && text.colorGradient.bottomRight == corners.BottomRight.linear, "Original four-corner proxy colors differ.");
                text.color = Color.white; text.enableVertexGradient = false; label.SetKey("fixture-name");
                // Disabled labels remain registered in the original service until OnDestroy.
                go.SetActive(false); language.CurrentLanguage = LanguageType.en_US;
                Require(label.Text == "Fixture Marsh", "Original inactive label lost its language subscription.");
                go.SetActive(true); language.CurrentLanguage = LanguageType.zh_CN;
                yield return null; yield return null;
                var cameraObject = new GameObject("Original label core camera"); var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 4; camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = camera.allowMSAA = false;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                var target = new RenderTexture(1024, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create(); camera.targetTexture = target;
                yield return null; yield return null;
                var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); while (!request.done) yield return null;
                Require(!request.hasError, "Original label rendering GPU readback failed.");
                var pixels = request.GetData<Color32>().ToArray(); Require(pixels.Count(p => p.r > 10 || p.g > 10 || p.b > 10) > 500, "Original label core did not render glyphs.");
                var image = new Texture2D(1024, 256, TextureFormat.RGBA32, false, true); image.SetPixels32(pixels); image.Apply();
                File.WriteAllBytes(Path.Combine(OfficialMap13FlagTools.Repository, ".tools/map13-label-core.png"), image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
                UnityEngine.Object.Destroy(go); yield return null; yield return null;
                var registered = (System.Collections.ICollection)typeof(MultiLanguageUIService).GetField("mTempMultiLanguageUIs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
                language.CurrentLanguage = LanguageType.en_US;
                Require(registered.Count == 0, "Original label OnDestroy did not unregister from the language service.");
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
            }
            DestroyFont(font); DestroyFont(replacementFont); yield return null;
        }
        private enum FixtureState { Unknown = -1, First = 1, Second = 2 }
        private static readonly List<string> transitions = new List<string>();
        private static void VerifyStates()
        {
            var root = new GameObject("Original state fixture"); root.SetActive(false);
            var shared = new GameObject("Shared"); var first = new GameObject("First"); var second = new GameObject("Second");
            foreach (var child in new[] { shared, first, second }) { child.transform.SetParent(root.transform, false); child.AddComponent<StateProbe>(); }
            second.SetActive(false); root.SetActive(true); transitions.Clear();
            var config = new StateGameObjectConfig();
            Field(config, "mStateGameObjectData", new StateGameObjectData { GoConfig = new List<StateGameObjectData.StateGameObjectKV> {
                new StateGameObjectData.StateGameObjectKV { State = 1, GameObjects = new List<GameObject> { shared, first } },
                new StateGameObjectData.StateGameObjectKV { State = 2, GameObjects = new List<GameObject> { shared, second } } } });
            var state = new StateSwitcher<FixtureState>(); Field(state, "mStateConfigs", new List<IStateConfig> { config }); Field(state, "mOwner", root);
            state.SetState(FixtureState.Second);
            Require(shared.activeSelf && !first.activeSelf && second.activeSelf && string.Join(",", transitions) == "-First,+Second" && state.State == FixtureState.Second, "Original shared-node state transition/lifecycle order differs.");
            transitions.Clear(); state.SetState(FixtureState.First);
            Require(shared.activeSelf && first.activeSelf && !second.activeSelf && string.Join(",", transitions) == "-Second,+First", "Original reverse state transition differs.");
            state.Clear(); Require(shared.activeSelf && first.activeSelf && !second.activeSelf, "Original no-op GameObject Clear mutated state.");
            transitions.Clear(); state.SetState(FixtureState.Unknown);
            Require(!shared.activeSelf && !first.activeSelf && !second.activeSelf && string.Join(",", transitions) == "-Shared,-First", "Original missing-state deactivation order differs.");
            var boolean = new StateSwitcher<bool>(); Field(boolean, "mStateConfigs", new List<IStateConfig> { config }); boolean.SetState(true);
            Require(shared.activeSelf && first.activeSelf && !second.activeSelf && boolean.State, "Original Boolean state conversion differs.");
            var integer = new StateSwitcher<int>(); Field(integer, "mStateConfigs", new List<IStateConfig> { config }); integer.SetState(2);
            Require(shared.activeSelf && !first.activeSelf && second.activeSelf && integer.State == 2, "Original integer state conversion differs.");
            UnityEngine.Object.Destroy(root);
        }
        private sealed class StateProbe : MonoBehaviour
        {
            private void OnEnable() { transitions.Add("+" + gameObject.name); }
            private void OnDisable() { transitions.Add("-" + gameObject.name); }
        }
        private sealed class FixtureProcessor : ITextProcessor
        {
            private readonly List<string> trace; private readonly string name; private readonly Func<string, string> process;
            public FixtureProcessor(List<string> trace, string name, Func<string, string> process) { this.trace = trace; this.name = name; this.process = process; }
            public string Process(string text) { trace.Add(name + ":" + text); return process(text); }
        }
        private sealed class FixtureLanguage : ILanguageTypeService
        {
            private LanguageType language;
            public LanguageType CurrentLanguage { get => language; set { language = value; OnLanguageChanged?.Invoke(value); } }
            public bool IsShowLanguageKey { get; set; }
            public bool IsShowMissingKeyText { get; set; }
            public event Action<LanguageType> OnLanguageChanged;
        }
        private sealed class FixtureFonts : IMultiLanguageFontService
        { public TMP_FontAsset Font; public MultiLanguageFontAsset GetMultiLanguageFontAsset() => new MultiLanguageFontAsset(Font); }
        private sealed class FixtureLocalization : ILocalizationTextService
        {
            public LanguageType CurrentLanguage => LanguageType.zh_CN;
            public void ReloadData() { throw new NotSupportedException("Fixture only"); }
            public bool ContainsKey(string key) => key == "fixture-name" || key == "fixture-format";
            public string GetText(string key, LanguageType language, params object[] args)
            { if (key == "fixture-name") return language == LanguageType.zh_CN ? "泽之国 海之宫" : "Fixture Marsh"; if (key == "fixture-format") return "Level {0}"; throw new KeyNotFoundException(key); }
            public bool TryGetOriginalText(string key, LanguageType language, out string result) { result = GetText(key, language); return true; }
            public bool TryGetText(string key, LanguageType language, out string result) => TryGetOriginalText(key, language, out result);
            public bool TryGetText(string key, out string text) => TryGetText(key, CurrentLanguage, out text);
            public string GetTextFormated(string key, LanguageType language, params object[] args) => string.Format(GetText(key, language), args);
            public void SetReplacer(ITextReplacer replacer) { throw new NotSupportedException("Fixture only"); }
            public void AddDataProvider(ILanguageTextDataProvider provider) { throw new NotSupportedException("Fixture only"); }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
