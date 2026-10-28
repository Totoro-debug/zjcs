using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.EntityIndex;
using Zjcs.Offline.World.NativeMap14EntityLoading;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityIndexAcceptance
    {
        private const string Key = "Map14.EntityIndexs";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS") != "1")
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            else Debug.Log("Explicit entity container assembly isolation; full ProjectTools.Validate is not executed.");
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            Application.logMessageReceived -= Observe;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original entity container check timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original entity container check").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count > 0)
            {
                object value;
                try
                {
                    var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; }
                    value = top.Current;
                    if (value is IEnumerator nested && !(value is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original client entity index Play Mode passed: all pinned directories/identifiers/native paths, unknown lookup, static cache, disposal and reinitialization; native shared state and original client container/loading/four-statue regression passed. Full native prefab catalog, component injection, formal world host/input/UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }
        private static IEnumerator Steps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            {
                var context = new NativeMap14EntityIndexContext(shared);
                var original = context.Decode(context.LoadText("Assets/Config/EC/index.g.txt"));
                Require(original.Count == context.DirectoryCount && original.Sum(pair => pair.Value.Length) == context.IdentifierCount,
                    "unchanged original index counts differ");
                using (var index = new MyEntityIndexHandler { Context = context })
                {
                    index.Initialize();
                    int count = 0;
                    foreach (var pair in original)
                    {
                        Require(index.GetIdsByDir(pair.Key).SequenceEqual(pair.Value), "directory identifier order differs");
                        foreach (int id in pair.Value)
                        {
                            Require(index.GetDir(id) == pair.Key && index.GetEntityPath(id) ==
                                "Assets/Prefabs/EcEntity/" + pair.Key + "/" + id + ".prefab", "original entity native address differs: " + id);
                            count++;
                        }
                    }
                    Require(count == context.IdentifierCount && index.GetDir(int.MinValue) == null && index.GetIdsByDir("absent-directory-fixture") == null,
                        "original absent lookup differs");
                    var cached = index.GetEntityPath(6570); var directory = index.GetDir(6570);
                    Require(directory != null, "official hero statue is absent from the original index");
                    index.Dispose();
                    Require(index.GetDir(6570) == null && index.GetIdsByDir(directory) == null && index.GetEntityPath(6570) == cached,
                        "original disposal or static cache differs");
                    index.Initialize(); Require(index.GetDir(6570) == directory && index.GetEntityPath(6570) == cached, "original reinitialization differs");
                }
                bool rejected = false; try { context.LoadText("Assets/Config/EC/wrong-fixture.txt"); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "unbound index path was accepted");
                Debug.Log("Original selected entity index verified: " + context.DirectoryCount + " directories / " + context.IdentifierCount + " identifiers.");
            }
            yield return (IEnumerator)typeof(OfficialMap14ClientEntityStateAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
