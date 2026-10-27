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
using Zjcs.Offline.World.NativeLabelResources;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13LanguageAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeLabelLanguageExecution";
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
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-language-code-source-manifest.json")));
            foreach (var record in source.outputs)
            {
                if (OfficialMap13TextTools.Hash(File.ReadAllBytes(Path.Combine(root, record.path))) != record.sha256 ||
                    OfficialMap13TextTools.Hash(File.ReadAllBytes(record.path.Substring(6))) != record.sha256)
                    throw new InvalidDataException("Original label language source differs: " + record.path);
                if (!string.IsNullOrEmpty(record.meta_sha256) && OfficialMap13TextTools.Hash(File.ReadAllBytes(record.path.Substring(6) + ".meta")) != record.meta_sha256)
                    throw new InvalidDataException("Original label language MonoScript identity differs: " + record.path);
            }
            Debug.Log("Original label language pinned C#/metadata/native evidence and MonoScript identities verified.");
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
            { Debug.LogError("Original label language execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original label language fixture").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Original binary language Play Mode passed: both byte-identical current rawfiles, original provider/reload/disposal order, original key/hash/UTF8/buffer lookup, replacement and missing-key rules, all ten culture constructions, Chinese numeric/time arguments and actual bound label rendering. Original 88 world label prefabs and global settings remain separate.");
            EditorApplication.isPlaying = false;
        }

        [Serializable] private sealed class LanguageReference { public RawFile[] files; public TextValue[] texts; public HashValue[] hashes; public BinaryValue[] samples; }
        [Serializable] private sealed class RawFile { public string path, sha256; }
        [Serializable] private sealed class TextValue { public string key, value; }
        [Serializable] private sealed class HashValue { public string text, seed, value; }
        [Serializable] private sealed class BinaryValue { public string hash, value; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private static IEnumerator VerifySteps()
        {
            Require(QualitySettings.activeColorSpace == ColorSpace.Linear, "Language fixture requires Linear color space.");
            var reference = JsonUtility.FromJson<LanguageReference>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-label-language.json"));
            foreach (var vector in reference.hashes)
                Require(XXHash64.ComputeHash(vector.text, ulong.Parse(vector.seed)).ToString() == vector.value, "Original XXHash64 differs from independent vectors.");
            foreach (var file in reference.files)
                Require(OfficialMap13TextTools.Hash(File.ReadAllBytes(file.path.Substring(6))) == file.sha256, "Packaged original language rawfile changed.");
            // The actual original reader must grow its 256-byte buffer and seek on every read.
            foreach (var sample in reference.samples)
            {
                string found = null;
                foreach (var file in reference.files)
                {
                    using (var reader = new LocalizationBinaryFileReader(file.path.Substring(6)))
                    { found = reader.GetTextByHash(ulong.Parse(sample.hash)); if (found != null) break; }
                }
                Require(found == sample.value && System.Text.Encoding.UTF8.GetByteCount(found) > 256, "Original language reader long UTF8/seek/buffer path differs.");
            }
            var language = new FixtureLanguage(); var loader = new CountingLoader(new LocalLanguageFiles(Application.streamingAssetsPath));
            var launch = new LaunchLanguageBinaryTextDataProvider("Assets/Launch/"); var game = new GameLanguageBinaryTextDataProvider("Assets/Config/");
            var lookup = new LocalizationBinaryTextService(language, null, new[] { launch }, loader);
            Require(loader.Loads == 1 && loader.Disposals == 1, "Original launch provider lease lifetime differs.");
            lookup.AddDataProvider(game);
            Require(loader.Loads == 2 && loader.Disposals == 2, "Original added game provider lease lifetime differs.");
            foreach (var item in reference.texts)
            {
                Require(lookup.ContainsKey(item.key), "Original language key missing: " + item.key);
                Require(lookup.GetText(item.key, LanguageType.zh_CN) == item.value, "Original binary language text differs: " + item.key);
            }
            Require(reference.texts.Count(t => t.key.StartsWith("region_13_")) >= 105, "Original Map13 region language coverage differs.");
            Require(!lookup.TryGetOriginalText("", LanguageType.zh_CN, out string empty) && empty == "", "Original empty-key result differs.");
            Require(!lookup.TryGetText("__missing_map13_fixture__", out string missing) && missing == null && lookup.GetText("__missing_map13_fixture__", LanguageType.zh_CN) == "__missing_map13_fixture__", "Original missing-key behavior differs.");
            string regionKey = reference.texts.First(t => t.key.StartsWith("region_13_")).key;
            language.IsShowLanguageKey = true; Require(lookup.GetText(regionKey, LanguageType.zh_CN) == regionKey, "Original show-key flag differs."); language.IsShowLanguageKey = false;
            var replacer = new FixtureReplacer(); lookup.SetReplacer(replacer);
            string regionName = reference.texts.First(t => t.key == regionKey).value;
            Require(lookup.GetText(regionKey, LanguageType.zh_CN, 65) == regionName + "#1" && lookup.GetText(regionKey, LanguageType.zh_CN, 65) == regionName + "#1" && replacer.Calls == 2, "Original binary replacement unexpectedly cached or lost arguments.");
            lookup.SetReplacer(null); language.CurrentLanguage = LanguageType.zh_CN;
            Require(loader.Loads == 4 && loader.Disposals == 4 && lookup.GetText(regionKey, LanguageType.zh_CN) == regionName, "Original language change did not reload/dispose both readers.");
            LanguageManager.Initialize(lookup);
            foreach (var type in new[] { LanguageType.zh_CN, LanguageType.zh_TW, LanguageType.en_US, LanguageType.ja, LanguageType.ko, LanguageType.de, LanguageType.fr, LanguageType.ru, LanguageType.pt, LanguageType.es })
                Require(LanguageCultureResolver.Resolve(type).LanguageType == type, "Original culture constructor language guard differs.");
            var compact = new Dictionary<long, string> { { 0, "0" }, { 9999, "9999" }, { 10000, "1万" }, { 123456, "12.3万" }, { 123456789, "1.23亿" } };
            foreach (var item in compact) Require(CultureNumberArg.Compact(item.Key).ToString() == item.Value, "Original compact number differs: " + item.Key);
            Require(CultureNumberArg.RawFormat(7).ToString() == "07" && CultureNumberArg.Decimal(123456, 1).ToString() == "12.3万", "Original raw/decimal number argument differs.");
            Require(new CultureTimeSpanArg(TimeSpan.FromSeconds(3661)).ToString() == "01:01:01" && new CultureTimeSpanArg(TimeSpan.FromSeconds(59)).ToString() == "00:59", "Original normal countdown format differs.");
            string FormatKey(string key, params object[] args) => string.Format(reference.texts.First(t => t.key == key).value, args);
            Require(new CultureTimeSpanArg(TimeSpan.FromHours(49)).ToString() == FormatKey("TwoDayCountdown2", 2, 1), "Original day/hour countdown differs.");
            Require(new CultureTimeSpanArg(TimeSpan.FromSeconds(59.1), ETimeSpanFormatType.Brief).ToString() == FormatKey("time_second", 60), "Original brief seconds ceiling differs.");
            Require(new CultureTimeSpanArg(TimeSpan.FromMinutes(61.5), ETimeSpanFormatType.Short).ToString() == FormatKey("time_hour", 1) && new CultureTimeSpanArg(TimeSpan.FromMinutes(61.5), ETimeSpanFormatType.CeilShort).ToString() == FormatKey("time_hour", 2), "Original short floor/ceiling differs.");
            Require(new CultureTimeSpanArg(TimeSpan.Zero, ETimeSpanFormatType.Brief).ToString() == FormatKey("time_end"), "Original ended countdown text differs.");
            Require(new CultureTimeSpanArg(new TimeSpan(1, 2, 3, 4), ETimeSpanFormatType.Detail).ToString() == "1天02小时03分04秒", "Original detail countdown format differs.");
            Require(lookup.GetText(regionKey, LanguageType.en_US) == regionName, "Original binary reader incorrectly switched files from a GetText language argument.");
            var elapsed = new TimeSpan(1, 2, 3, 4);
            Require(new CultureTimeSpanArg(elapsed, ETimeSpanFormatType.Adapted).ToString() == FormatKey("time_day", 1) + FormatKey("time_hour", 2) + FormatKey("time_minute_abbreviation", 3) + FormatKey("time_second", 4), "Original adapted countdown component order differs.");
            Require(new CultureTextArg(regionKey).ToString() == regionName && new CultureTextArg("Literal", null, false).ToString() == "Literal", "Original localized/literal culture argument differs.");
            var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OfficialMap13TextTools.FontPath);
            var font = UnityEngine.Object.Instantiate(original); font.material = UnityEngine.Object.Instantiate(original.material);
            var atlas = UnityEngine.Object.Instantiate(original.atlasTexture); font.atlasTextures = new[] { atlas }; font.material.mainTexture = atlas; font.ReadFontAssetDefinition();
            using (var uiService = new MultiLanguageUIService(language))
            {
                var go = new GameObject("Original binary language label", typeof(RectTransform)); go.SetActive(false);
                var text = go.AddComponent<TextMeshPro>();
                var serialized = new SerializedObject(text); serialized.FindProperty("m_fontAsset").objectReferenceValue = font;
                serialized.FindProperty("m_sharedMaterial").objectReferenceValue = font.material; serialized.ApplyModifiedPropertiesWithoutUndo();
                text.fontSize = 40; text.enableWordWrapping = false; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(700, 200);
                var label = go.AddComponent<MultiLanguageText>(); label.BindLanguageServices(uiService, language); label.LocalizationTextService = lookup; label.MultiLanguageFontService = new FixtureFont { Font = font }; label.myKey = regionKey;
                go.SetActive(true); yield return null; yield return null;
                Require(label.Text == regionName, "Original binary service did not feed the actual multilingual component.");
                label.SetKey("time_hour", 2); Require(label.Text == FormatKey("time_hour", 2), "Original bound language formatting differs.");
                label.SetKey(regionKey); language.CurrentLanguage = LanguageType.zh_CN;
                Require(label.Text == regionName && loader.Loads == 6 && loader.Disposals == 6, "Original reader reload and UI refresh order differs.");
                var cameraObject = new GameObject("Original language camera"); var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 4; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = camera.allowMSAA = false;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                var target = new RenderTexture(1024, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create(); camera.targetTexture = target;
                yield return null; yield return null;
                var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); while (!request.done) yield return null;
                Require(!request.hasError, "Original language label GPU readback failed.");
                var pixels = request.GetData<Color32>().ToArray(); Require(pixels.Count(p => p.r > 10 || p.g > 10 || p.b > 10) > 500, "Original language label rendered no visible text.");
                var image = new Texture2D(1024, 256, TextureFormat.RGBA32, false, true); image.SetPixels32(pixels); image.Apply(); File.WriteAllBytes(Path.Combine(OfficialMap13FlagTools.Repository, ".tools/map13-language.png"), image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
                UnityEngine.Object.Destroy(go); yield return null; yield return null;
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
            }
            lookup.Dispose();
            // A reader left open would prevent this exclusive open on Windows.
            foreach (var file in reference.files) using (File.Open(file.path.Substring(6), FileMode.Open, FileAccess.Read, FileShare.None)) { }
            UnityEngine.Object.Destroy(font.material); UnityEngine.Object.Destroy(atlas); UnityEngine.Object.Destroy(font);
        }
        private sealed class FixtureLanguage : ILanguageTypeService
        {
            private LanguageType language;
            public LanguageType CurrentLanguage { get => language; set { language = value; OnLanguageChanged?.Invoke(value); } }
            public bool IsShowLanguageKey { get; set; }
            public bool IsShowMissingKeyText { get; set; }
            public event Action<LanguageType> OnLanguageChanged;
        }
        private sealed class FixtureFont : IMultiLanguageFontService
        { public TMP_FontAsset Font; public MultiLanguageFontAsset GetMultiLanguageFontAsset() => new MultiLanguageFontAsset(Font); }
        private sealed class FixtureReplacer : ITextReplacer
        {
            public int Calls;
            public string ReplaceText(string text, params object[] args) { Calls++; return text + "#" + (args == null ? -1 : args.Length); }
            public bool TryMatchReplaceKey(string text, out HashSet<string> keys) { throw new NotSupportedException("Fixture only"); }
            public void Register(string name, Func<object> method) { throw new NotSupportedException("Fixture only"); }
            public void Unregister(string name) { throw new NotSupportedException("Fixture only"); }
        }
        private sealed class CountingLoader : ILocalRawFileLoader
        {
            private readonly ILocalRawFileLoader source; public int Loads, Disposals;
            public CountingLoader(ILocalRawFileLoader source) { this.source = source; }
            public ILocalRawFileLease LoadRawFile(string location) { Loads++; return new CountedLease(source.LoadRawFile(location), this); }
            private sealed class CountedLease : ILocalRawFileLease
            {
                private readonly ILocalRawFileLease inner; private readonly CountingLoader owner;
                public CountedLease(ILocalRawFileLease inner, CountingLoader owner) { this.inner = inner; this.owner = owner; }
                public string GetRawFilePath() => inner.GetRawFilePath();
                public void Dispose() { inner.Dispose(); owner.Disposals++; }
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
