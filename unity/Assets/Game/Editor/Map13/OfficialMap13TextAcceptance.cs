using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    // Isolated font/shader fixture; never substitutes for the original region UI.
    public static class OfficialMap13TextAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeTextExecution";
        private static bool started;
        public static void Run()
        {
            OfficialMap13TextTools.RestoreAndCompile();
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog")
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            QualitySettings.renderPipeline = null;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
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
            { Debug.LogError("Map13 native text execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Map13 native text fixture").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Map13 native text Play Mode passed: original Alpha8 GPU bytes, dynamic Chinese/ASCII glyphs, source-font identity, native mesh/atlas generation and original shader face/outline/dual-outline/underlay/glow/gray/clipping paths on Linear. Original region UI and effect components remain unverified.");
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class Atlas { public string sha256; public int byte_count; }
        [Serializable] private sealed class Source { public Atlas atlas; }

        private static IEnumerator VerifySteps()
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Original text fixture requires Linear color space.");
            string root = OfficialMap13FlagTools.Repository;
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-native-text-font-source-manifest.json")));
            var original = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OfficialMap13TextTools.FontPath);
            var originalAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(OfficialMap13TextTools.AtlasPath);
            var request = AsyncGPUReadback.Request(originalAtlas, 0);
            while (!request.done) yield return null;
            if (request.hasError || request.GetData<byte>().Length != source.atlas.byte_count || OfficialMap13TextTools.Hash(request.GetData<byte>().ToArray()) != source.atlas.sha256)
                throw new InvalidDataException("Original Alpha8 atlas GPU bytes/rows differ.");
            // Dynamic writes must target independent runtime clones of every mutable asset.
            var font = UnityEngine.Object.Instantiate(original);
            font.material = UnityEngine.Object.Instantiate(original.material);
            var atlas = UnityEngine.Object.Instantiate(originalAtlas); font.atlasTextures = new[] { atlas }; font.material.mainTexture = atlas;
            font.ReadFontAssetDefinition();
            const string characters = "泽之国海之宫推荐战力水路神社Lv.65 123,456";
            if (!font.TryAddCharacters(characters, out string missing) || !string.IsNullOrEmpty(missing))
                throw new InvalidDataException("Original dynamic font could not generate characters: " + missing);
            foreach (char c in characters.Where(c => c != ' '))
                if (!font.characterLookupTable.TryGetValue(c, out TMP_Character character) || character.textAsset != font || character.glyph.index == 0)
                    throw new InvalidDataException("Original dynamic font substituted a character: " + c);
            if (font.glyphTable.Count <= 2 || original.glyphTable.Count != 2 || original.characterTable.Count != 2)
                throw new InvalidDataException("Dynamic glyph generation modified the original asset.");
            var cameraObject = new GameObject("Original text shader camera"); var camera = cameraObject.AddComponent<Camera>();
            // Original world TextMeshPro uses m_isOrthographic=0, hence its
            // native 0.1 world scale. Zoom the fixture camera, keep that setting.
            camera.orthographic = true; camera.orthographicSize = 7.5f; camera.aspect = 4;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black; camera.allowHDR = false; camera.allowMSAA = false;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>(); additional.renderPostProcessing = false;
            var target = new RenderTexture(1024, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); camera.targetTexture = target;
            var label = new GameObject("Native text fixture label", typeof(RectTransform)); label.SetActive(false);
            var text = label.AddComponent<TextMeshPro>();
            var serialized = new SerializedObject(text);
            serialized.FindProperty("m_fontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            label.SetActive(true);
            text.fontSize = 40; text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(600, 150);
            text.color = Color.white; text.text = "泽之国　海之宫\nLv.65　123,456";
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount < 20 || text.textInfo.meshInfo[0].vertexCount < 40)
                throw new InvalidDataException("Original native text did not generate the glyph mesh.");
            string[][] cases = { Array.Empty<string>(), new[] { "OUTLINE_ON" }, new[] { "OUTLINE_ON", "OUTLINE_ON_2" },
                new[] { "UNDERLAY_ON" }, new[] { "GLOW_ON" }, new[] { "GRAY_ON" }, new[] { "UNITY_UI_CLIP_RECT", "UNITY_UI_ALPHACLIP" } };
            string previous = null;
            for (int i = 0; i < cases.Length; i++)
            {
                var material = font.material; material.shaderKeywords = cases[i];
                material.SetFloat("_OutlineWidth", .15f); material.SetColor("_OutlineColor", Color.red);
                material.SetFloat("_OutlineWidth2", .3f); material.SetColor("_OutlineColor2", Color.blue);
                material.SetFloat("_UnderlayOffsetX", .5f); material.SetFloat("_UnderlayOffsetY", -.5f); material.SetColor("_UnderlayColor", Color.green);
                material.SetFloat("_GlowOffset", 0); material.SetFloat("_GlowInner", .1f); material.SetFloat("_GlowOuter", .3f); material.SetFloat("_GlowPower", .75f);
                material.SetColor("_GlowColor", Color.magenta);
                material.SetVector("_ClipRect", i == 6 ? new Vector4(0, -75, 300, 75) : new Vector4(-32767, -32767, 32767, 32767));
                // A colored face makes the original gray path observably different.
                material.SetColor("_FaceColor", new Color(1, .6f, .2f, 1));
                yield return null; yield return null;
                request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError) throw new InvalidDataException("Original text camera readback failed.");
                Color32[] pixels = request.GetData<Color32>().ToArray();
                var image = new Texture2D(1024, 256, TextureFormat.RGBA32, false, true); image.SetPixels32(pixels); image.Apply();
                File.WriteAllBytes(Path.Combine(root, ".tools/map13-text-" + i + ".png"), image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
                int lit = pixels.Count(c => c.r > 5 || c.g > 5 || c.b > 5);
                if (lit < 500) throw new InvalidDataException("Original text shader produced no visible glyphs: " + i + " " + lit);
                string hash = OfficialMap13TextTools.Hash(request.GetData<byte>().ToArray());
                if (hash == previous) throw new InvalidDataException("Original text shader feature did not affect rendering: " + i);
                if (i == 5 && pixels.Any(c => Math.Abs(c.r - c.g) > 2 || Math.Abs(c.g - c.b) > 2))
                    throw new InvalidDataException("Original gray shader did not produce neutral RGB.");
                if (i == 6 && pixels.Where((c, n) => n % 1024 < 500).Any(c => c.r > 2 || c.g > 2 || c.b > 2))
                    throw new InvalidDataException("Original text UI clipping leaked outside its rectangle.");
                previous = hash;
                Debug.Log("Original text shader rendered fixture " + i + ": " + lit + " visible pixels, " + hash);
            }
            request = AsyncGPUReadback.Request(originalAtlas, 0);
            while (!request.done) yield return null;
            if (request.hasError || OfficialMap13TextTools.Hash(request.GetData<byte>().ToArray()) != source.atlas.sha256)
                throw new InvalidDataException("Dynamic font writes modified the original atlas.");
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(label); UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(font.material); UnityEngine.Object.Destroy(atlas); UnityEngine.Object.Destroy(font);
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
