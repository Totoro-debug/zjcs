using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map14EntityTextures
{
    [InitializeOnLoad]
    public static class OfficialMap14EntityTextureAcceptance
    {
        private const string Key = "Map14.EntityTextureGpu";
        private static bool started, loggedError;
        static OfficialMap14EntityTextureAcceptance()
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
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetString(Key + ".source", OfficialMap14EntityTextureTools.Hash(File.ReadAllBytes(Path.Combine(OfficialMap14EntityTextureTools.Repository, OfficialMap14EntityTextureTools.SourcePath))));
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) loggedError = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original Map14 material texture GPU validation timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map14 original texture GPU fixture").AddComponent<Runner>().StartCoroutine(Verify());
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
            string sourcePath = Path.Combine(OfficialMap14EntityTextureTools.Repository, OfficialMap14EntityTextureTools.SourcePath);
            if (OfficialMap14EntityTextureTools.Hash(File.ReadAllBytes(sourcePath)) != SessionState.GetString(Key + ".source", string.Empty))
                throw new InvalidDataException("Original material texture source changed during Play Mode transition.");
            var source = OfficialMap14EntityTextureTools.ReadSource();
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-entity-textures-catalog");
            if (!catalog || catalog.Bindings.Length != 1644) throw new InvalidDataException("Native texture catalog is unavailable in Play Mode.");
            int mips = 0;
            foreach (var record in source.textures)
            {
                var texture = catalog.Load<Texture2D>(OfficialMap14EntityTextureTools.Identity(record));
                OfficialMap14EntityTextureTools.ValidateTexture(record, texture);
                foreach (var mip in record.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    while (!request.done) yield return null;
                    if (request.hasError || OfficialMap14EntityTextureTools.Hash(request.GetData<byte>().ToArray()) != mip.rgba_sha256)
                        throw new InvalidDataException("Original native material texture GPU bytes differ: " + OfficialMap14EntityTextureTools.Identity(record) + " mip " + mip.level);
                    mips++;
                }
            }
            if (mips != source.counts.mips) throw new InvalidDataException("Original material texture mip inventory differs.");
            Debug.Log("Map14 original material textures Play Mode passed: 1644 explicit CAB/PathID bindings, " + mips +
                " exact native GPU mip readbacks and original texture/sampler settings on . Six original empty dynamic font atlases, dynamic loader variants, entity rendering, input and saves remain unverified.");
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
