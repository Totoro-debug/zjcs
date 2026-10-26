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

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13TextEffectAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeTextEffectExecution";
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
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-text-effects-code-source-manifest.json")));
            foreach (var record in source.outputs.Concat(new[] { source.evidence }))
            {
                if (OfficialMap13TextTools.Hash(File.ReadAllBytes(Path.Combine(root, record.path))) != record.sha256 ||
                    OfficialMap13TextTools.Hash(File.ReadAllBytes(record.path.Substring(6))) != record.sha256)
                    throw new InvalidDataException("Original TMP effects source differs: " + record.path);
                if (!string.IsNullOrEmpty(record.meta_sha256) && OfficialMap13TextTools.Hash(File.ReadAllBytes(record.path.Substring(6) + ".meta")) != record.meta_sha256)
                    throw new InvalidDataException("Original TMP effects MonoScript identity differs: " + record.path);
            }
            if (typeof(TMP_Text).GetField("m_isMaterialDirty", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType != typeof(bool))
                throw new InvalidDataException("Original TMP material-dirty field adapter is incompatible.");
            Debug.Log("Original TMP effects pinned C#/metadata/native evidence and MonoScript identities verified.");
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
            { Debug.LogError("Original TMP effects execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original TMP effects fixture").AddComponent<Runner>().StartCoroutine(Verify());
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
            if (errors.Count != 0) { Debug.LogError("TMP effect lifecycle logged errors: " + string.Join("\n", errors)); EditorApplication.isPlaying = false; yield break; }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Original TMP effects Play Mode passed: official label fields, original colors/normalization, pooled CRC identity and ownership, last-reference release, real OnEnable/OnDisable/Update, gray mutation, autosize TEXT_CHANGED_EVENT and Linear DX12 rendering. Full original world label UI remains separate.");
            EditorApplication.isPlaying = false;
        }

        private static void Near(float actual, double expected, string reason)
        { if (Math.Abs(actual - expected) > 0.000002) throw new InvalidDataException(reason + ": " + actual + " expected " + expected); }
        private static void OfficialFields(TMPEffect effect)
        {
            effect.dilate = 0; effect.linearDodge = false; effect.useVertexColor = false; effect.RealtimeUpdate = false;
            effect.outline = true; effect.outlineColor = new Color(.3176470697f, .1372549087f, .1294117719f, .6980392337f); effect.outlineWidth = 2;
            effect.outline2 = false; effect.outlineColor2 = Color.black; effect.outlineWidth2 = 0;
            effect.underlay = false; effect.underlayColor = Color.black; effect.shadowAngle = 90; effect.shadowDis = effect.shadowBlur = 0;
            effect.glow = false; effect.glowColor = Color.black; effect.glowOffset = effect.glowInner = effect.glowOuter = effect.glowPower = 0; effect.gray = false;
        }
        private static TMPEffect Create(string name, TMP_FontAsset font, Vector3 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.SetActive(false);
            var text = go.AddComponent<TextMeshPro>();
            var serialized = new SerializedObject(text); serialized.FindProperty("m_fontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_sharedMaterial").objectReferenceValue = font.material; serialized.ApplyModifiedPropertiesWithoutUndo();
            go.transform.position = position; go.SetActive(true);
            text.fontSize = 40; text.text = "泽之国 海之宫 Lv.65"; text.color = Color.white;
            text.enableWordWrapping = false; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(600, 100);
            text.ForceMeshUpdate();
            var effect = go.AddComponent<TMPEffect>(); OfficialFields(effect); effect.SetupEffect(true); return effect;
        }
        private static IEnumerator VerifySteps()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 || QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Original TMP effect fixture requires Linear DX12.");
            var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OfficialMap13TextTools.FontPath);
            var font = UnityEngine.Object.Instantiate(original); font.material = UnityEngine.Object.Instantiate(original.material);
            var atlas = UnityEngine.Object.Instantiate(original.atlasTexture); font.atlasTextures = new[] { atlas }; font.material.mainTexture = atlas;
            font.ReadFontAssetDefinition();
            var first = Create("Original TMP effect first owner", font, new Vector3(0, 3, 0));
            var second = Create("Original TMP effect second owner", font, new Vector3(0, -3, 0));
            var shared = first.text.fontSharedMaterial;
            if (shared == font.material || second.text.fontSharedMaterial != shared || !shared.IsKeywordEnabled("OUTLINE_ON"))
                throw new InvalidDataException("Original material CRC pool did not share matching effects.");
            Near(shared.GetFloat("_OutlineWidth"), .5, "Original label outline normalization");
            if (shared.GetColor("_OutlineColor") != first.outlineColor) throw new InvalidDataException("Original effect applied an extra color-space conversion.");
            second.SetGray(true);
            if (second.text.fontSharedMaterial == shared || !second.text.fontSharedMaterial.IsKeywordEnabled("GRAY_ON") || shared.IsKeywordEnabled("GRAY_ON"))
                throw new InvalidDataException("Original gray mutation modified the other pool owner.");
            second.SetGray(false);
            if (second.text.fontSharedMaterial != shared) throw new InvalidDataException("Original CRC pool did not reuse the existing effect.");
            second.enabled = false; yield return null;
            if (second.text.fontSharedMaterial != font.material || first.text.fontSharedMaterial != shared || shared == null)
                throw new InvalidDataException("Original disable released a remaining material owner.");
            second.enabled = true; yield return null;
            if (second.text.fontSharedMaterial != shared) throw new InvalidDataException("Original enable failed to reuse the remaining effect.");
            first.enabled = false; second.enabled = false; yield return null; yield return null;
            if (shared != null) throw new InvalidDataException("Original last-reference material was not destroyed.");
            first.enabled = true;
            first.outline = false; first.outline2 = true; first.SetupEffect(true);
            if (first.outline2 || first.text.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON_2"))
                throw new InvalidDataException("Original outline-off mutation did not clear outline2.");
            first.outline = true; first.underlay = true; first.shadowDis = 1.5f; first.shadowBlur = .75f;
            foreach (float angle in new[] { 0f, 45f, 90f, 180f, 270f })
            {
                first.shadowAngle = angle; first.SetupEffect(true);
                var material = first.text.fontSharedMaterial; double radians = angle * (double)0.01745329238474369f;
                Near(material.GetFloat("_UnderlayOffsetX"), -Math.Cos(radians) * .375, "Original underlay X");
                Near(material.GetFloat("_UnderlayOffsetY"), -Math.Sin(radians) * .375, "Original underlay Y");
                Near(material.GetFloat("_UnderlaySoftness"), .1875, "Original underlay blur");
            }
            OfficialFields(first); first.RealtimeUpdate = true; first.outlineWidth = 3;
            yield return null; yield return null;
            Near(first.text.fontSharedMaterial.GetFloat("_OutlineWidth"), .75, "Original real Update effect refresh");
            first.RealtimeUpdate = false; first.outlineWidth = 2; first.SetupEffect(true);
            first.text.enableAutoSizing = true; first.text.fontSizeMin = 10; first.text.fontSizeMax = 40;
            first.text.rectTransform.sizeDelta = new Vector2(5, 100); first.text.ForceMeshUpdate(); yield return null; yield return null;
            if (first.text.fontSize >= 40) throw new InvalidDataException("Native text autosizing fixture did not resize.");
            Near(first.text.fontSharedMaterial.GetFloat("_OutlineWidth"), Math.Min(1, 40d / first.text.fontSize / 4 * 2), "Original autosize event effect rebuild");
            first.text.rectTransform.sizeDelta = new Vector2(600, 100); first.text.ForceMeshUpdate(); yield return null; yield return null;
            Near(first.text.fontSharedMaterial.GetFloat("_OutlineWidth"), .5, "Original autosize event recovery");
            second.enabled = true;
            var cameraObject = new GameObject("Original TMP effects camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 8; camera.aspect = 4; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = camera.allowMSAA = false;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var target = new RenderTexture(1024, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create(); camera.targetTexture = target;
            yield return null; yield return null;
            var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); while (!request.done) yield return null;
            if (request.hasError) throw new InvalidDataException("Original TMP effects rendering readback failed.");
            var pixels = request.GetData<Color32>().ToArray(); if (pixels.Count(p => p.r > 10 || p.g > 10 || p.b > 10) < 500)
                throw new InvalidDataException("Original TMP effects did not render visible glyphs.");
            var image = new Texture2D(1024, 256, TextureFormat.RGBA32, false, true); image.SetPixels32(pixels); image.Apply();
            File.WriteAllBytes(Path.Combine(OfficialMap13FlagTools.Repository, ".tools/map13-text-effects.png"), image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(first.gameObject); UnityEngine.Object.Destroy(second.gameObject); yield return null; yield return null;
            var pool = (System.Collections.IDictionary)typeof(TMPEffectMaterial).GetField("pool", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var references = (System.Collections.IDictionary)typeof(TMPEffectMaterial).GetField("m_refMatDic", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (pool.Count != 0 || references.Count != 0) throw new InvalidDataException("Original TMP effects left pooled materials after teardown.");
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
            UnityEngine.Object.Destroy(font.material); UnityEngine.Object.Destroy(atlas); UnityEngine.Object.Destroy(font);
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
