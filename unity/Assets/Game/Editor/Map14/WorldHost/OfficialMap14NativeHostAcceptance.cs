using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Presentation.World.NativeMap14;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map14
{
    // This fixture exercises the production native host. It supplies region
    // states directly; it does not claim map pointer, goddess or save coverage.
    public static class OfficialMap14NativeHostAcceptance
    {
        private const string Key = "Zjcs.Map14.NativeHostAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_NATIVE_MAP14_HOST") == "1")
                Debug.Log("Explicit native Map14 host assembly isolation; full ProjectTools.Validate is not executed.");
            else Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            if (!SortingLayer.IsValid(540003499) || !SortingLayer.IsValid(1182434123) ||
                !SortingLayer.IsValid(unchecked((int)3372647923)))
                throw new InvalidDataException("The native map check requires the original Ground/Object/Cloud sorting layers from ProjectSettings/TagManager.asset.");
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 native host acceptance timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Map14 native host verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 production native host Play Mode passed: original terrain/environment/labels and original shared nine-component root mounted together, initial position loading, live region state refresh, render captures, interrupted loading, disposal, binding restoration and remount. Map interactions, progression saves, world entity spawning and Player release are not covered.");
            }
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline, quality = QualitySettings.renderPipeline;
            var cameraObject = new GameObject("Map14 original world camera fixture"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .12f, 1);
            camera.farClipPlane = 1000; camera.allowHDR = false; camera.allowMSAA = false;
            var target = new RenderTexture(432, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create(); camera.targetTexture = target;
            var state = new State();
            var originalBindings = SharedBindings();
            int originalManagers = UnityEngine.Object.FindObjectsByType<GConfig.ConfigManager>(FindObjectsSortMode.None).Length;
            // Official default entry placement 101: grid (247,115), origin (-134.5,-134.5).
            state.Position = new Vector3(112.5f, -19.5f, 0);
            camera.transform.position = state.Position + new Vector3(0, 0, -100);
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ??
                throw new InvalidOperationException("Set the owned validation directory before running the native host check.");
            Directory.CreateDirectory(output);
            OfficialMap14NativeWorldView view = OfficialMap14NativeWorldView.Create(null, camera, state);
            yield return WaitForHost(view, state.Position);
            var context = view.GroundRoot.PartitionController.Context;
            if (context.GetCurrentMapStatus().Count != 111)
                throw new InvalidDataException("Map14 production host lost original region states.");
            Capture(camera, target, Path.Combine(output, "entry-private.png"));
            state.Public = true;
            view.UnlockRegion(state.Position);
            yield return new WaitForSecondsRealtime(5.25f);
            if (context.GetCurrentMapStatus().Any(status => status != MapRegionStatus.Public))
                throw new InvalidDataException("Map14 host did not refresh public region states.");
            Capture(camera, target, Path.Combine(output, "entry-public.png"));
            state.Position = new Vector3(0, -40, 0);
            camera.transform.position = state.Position + new Vector3(0, 0, -100);
            yield return WaitForHost(view, state.Position);
            Capture(camera, target, Path.Combine(output, "central-public.png"));
            var originalRoot = view.SharedRoot.Root;
            view.Dispose(); UnityEngine.Object.Destroy(view.gameObject);
            yield return null; yield return null;
            RequireSharedCleanup(view, originalRoot, originalBindings, originalManagers);
            if (GraphicsSettings.defaultRenderPipeline != pipeline || QualitySettings.renderPipeline != quality)
                throw new InvalidDataException("Map14 host did not restore previous pipelines.");
            if (camera.GetComponent<UniversalAdditionalCameraData>() != null)
                throw new InvalidDataException("Map14 host leaked added camera data after disposal.");
            view = OfficialMap14NativeWorldView.Create(null, camera, state);
            yield return WaitForHost(view, state.Position);
            Capture(camera, target, Path.Combine(output, "central-remount.png"));
            originalRoot = view.SharedRoot.Root;
            view.Dispose(); UnityEngine.Object.Destroy(view.gameObject);
            yield return null; yield return null;
            RequireSharedCleanup(view, originalRoot, originalBindings, originalManagers);
            view = OfficialMap14NativeWorldView.Create(null, camera, state);
            yield return null;
            if (view.IsReady || view.SharedRoot != null)
                throw new InvalidDataException("Interrupted-loading check did not interrupt shared map loading.");
            view.Dispose(); UnityEngine.Object.Destroy(view.gameObject);
            yield return null; yield return null;
            RequireSharedCleanup(view, null, originalBindings, originalManagers);
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
            if (errors != 0) throw new InvalidDataException("Map14 original host logged errors during its lifecycle.");
        }
        private static IEnumerator WaitForHost(OfficialMap14NativeWorldView view, Vector3 position)
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while ((!view.IsReady || !view.GroundRoot.PartitionController.Loader.IsPosReady(position)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!view.IsReady || !view.GroundRoot.PartitionController.Loader.IsPosReady(position))
                throw new InvalidDataException("Map14 production host did not load its current position.");
            if (view.SharedRoot == null || !(bool)view.SharedRoot.Root.GetType().GetField("Started").GetValue(view.SharedRoot.Root) ||
                ((IList)view.SharedRoot.Root.GetType().GetField("ComponentList").GetValue(view.SharedRoot.Root)).Count != 9)
                throw new InvalidDataException("Map14 production host did not start its complete original shared root.");
            for (int i = 0; i < 12; i++) yield return null;
            if (!view.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled))
                throw new InvalidDataException("Map14 host has no active original renderers.");
        }
        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            if (texture.GetPixels32().Select(color => color.r + 256 * color.g + 65536 * color.b).Distinct().Take(32).Count() < 32)
                throw new InvalidDataException("Map14 world capture contains no rendered scene detail.");
            UnityEngine.Object.Destroy(texture);
        }
        private static object[] SharedBindings()
        {
            var console = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == "Console");
            var serializer = Type.GetType("MessagePack.MessagePackSerializer, MessagePack", true);
            return new[] { console?.GetType("Parsers", true).GetField("Instance").GetValue(null),
                GConfig.FileLoader.sFileLoader, serializer.GetProperty("DefaultResolver").GetValue(null) };
        }
        private static void RequireSharedCleanup(OfficialMap14NativeWorldView view, object root, object[] expected, int managers)
        {
            var actual = SharedBindings();
            if (!actual.Zip(expected, ReferenceEquals).All(value => value) ||
                UnityEngine.Object.FindObjectsByType<GConfig.ConfigManager>(FindObjectsSortMode.None).Length != managers)
                throw new InvalidDataException("Map14 host did not restore its original shared bindings and configuration lifetime.");
            if (root != null && !(bool)root.GetType().GetField("Ended").GetValue(root))
                throw new InvalidDataException("Map14 host did not end its original shared root.");
            if (view != null && (view.SharedRoot != null || view.IsReady))
                throw new InvalidDataException("Map14 disposed host still publishes a ready shared root.");
        }
        private sealed class State : IMap14NativeWorldState
        {
            public Vector3 Position; public bool Public;
            public Vector3 PlayerWorldPosition => Position;
            public int PlayerLevel => 130;
            public int PlayerSubRank => 12;
            public long PlayerCombatRating => 6000000;
            public bool IsPublicRegion(int region) => Public;
            public int GetRegionProgress(int region) => 0;
            public int GetRegionGoddessId(int region) => 0;
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
