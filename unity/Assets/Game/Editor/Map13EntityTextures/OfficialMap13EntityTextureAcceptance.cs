using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13EntityTextures
{
    [InitializeOnLoad]
    public static class OfficialMap13EntityTextureAcceptance
    {
        private const string Key = "Map13.EntityTextureGpu";
        private static bool started, loggedError;
        static OfficialMap13EntityTextureAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe;
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetString(Key + ".source", OfficialMap13EntityTextureTools.Hash(File.ReadAllBytes(Path.Combine(OfficialMap13EntityTextureTools.Repository, OfficialMap13EntityTextureTools.SourcePath))));
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) loggedError = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original Map13 material texture GPU validation timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 original texture GPU fixture").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe;
            var steps = VerifySteps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (loggedError) { EditorApplication.isPlaying = false; yield break; }
            SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator VerifySteps()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            string sourcePath = Path.Combine(OfficialMap13EntityTextureTools.Repository, OfficialMap13EntityTextureTools.SourcePath);
            if (OfficialMap13EntityTextureTools.Hash(File.ReadAllBytes(sourcePath)) != SessionState.GetString(Key + ".source", string.Empty))
                throw new InvalidDataException("Original material texture source changed during Play Mode transition.");
            var source = OfficialMap13EntityTextureTools.ReadSource();
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-entity-textures-catalog");
            if (!catalog || catalog.Bindings.Length != 1485) throw new InvalidDataException("Native texture catalog is unavailable in Play Mode.");
            int mips = 0, textures = 0;
            foreach (var record in source.textures)
            {
                var texture = catalog.Load<Texture2D>(OfficialMap13EntityTextureTools.Identity(record));
                OfficialMap13EntityTextureTools.ValidateTexture(record, texture);
                int completed = 0;
                var hashes = new string[record.mips.Length];
                for (int index = 0; index < record.mips.Length; index++)
                {
                    int captured = index;
                    AsyncGPUReadback.Request(texture, record.mips[index].level, request =>
                    {
                        if (!request.hasError) hashes[captured] = OfficialMap13EntityTextureTools.Hash(request.GetData<byte>().ToArray());
                        completed++;
                    });
                }
                while (completed != record.mips.Length) yield return null;
                for (int index = 0; index < record.mips.Length; index++)
                {
                    if (hashes[index] != record.mips[index].rgba_sha256)
                        throw new InvalidDataException("Original native material texture GPU bytes differ: " + OfficialMap13EntityTextureTools.Identity(record) + " mip " + record.mips[index].level);
                    mips++;
                }
                textures++;
                if (textures % 128 == 0) Debug.Log("Map13 original texture GPU readbacks: " + textures + " textures, " + mips + " exact mips.");
            }
            if (mips != source.counts.mips) throw new InvalidDataException("Original material texture mip inventory differs.");
            CaptureTexturePreview(source, catalog);
            Debug.Log("Map13 original material textures Play Mode passed: 1485 explicit CAB/PathID bindings, " + mips +
                " exact native GPU mip readbacks and original texture/sampler settings. Six original empty dynamic font atlases, dynamic loader variants, entity rendering, input and saves remain unverified.");
        }
        private static void CaptureTexturePreview(OfficialMap13EntityTextureTools.Source source, NativeMap13AssetCatalog catalog)
        {
            // A diagnostic GPU preview of original textures, not a substitute for entity rendering.
            string[] names = { "map_fangJianBei_A_1", "map_fangJianBei_A_2", "map_fangJianBei_C_2", "map_huangQuanGe_C" };
            var target = RenderTexture.GetTemporary(640, 640, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(640, 640, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, new Color(.08f, .08f, .08f, 1));
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, 640, 640, 0);
                    for (int index = 0; index < names.Length; index++)
                    {
                        var record = source.textures.Single(item => item.fields.m_Name == names[index]);
                        var texture = catalog.Load<Texture2D>(OfficialMap13EntityTextureTools.Identity(record));
                        float fit = Mathf.Min(304f / texture.width, 304f / texture.height);
                        float width = texture.width * fit, height = texture.height * fit;
                        Graphics.DrawTexture(new Rect(index % 2 * 320 + (320 - width) / 2,
                            index / 2 * 320 + (320 - height) / 2, width, height), texture);
                    }
                }
                finally { GL.PopMatrix(); }
                pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); pixels.Apply();
                string folder = Path.GetFullPath(".tools/map13-entity-textures");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "native-texture-gpu-preview.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(pixels);
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
