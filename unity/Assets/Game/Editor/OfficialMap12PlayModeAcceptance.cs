using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    // Payload rendering check. Complete map interactions have a separate scope.
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private const string Key = "Zjcs.Map12.SourceRendering";
        private static bool started;

        public static void Run() => RunCore(false);
        public static void RunUi() => RunCore(true);
        public static void RunUiImported() => RunCore(true, false);
        public static void RunRuntime()
        {
            SessionState.SetBool(Key + ".runtimeTerminals", false);
            SessionState.SetBool(Key + ".runtimeSpatial", false);
            SessionState.SetBool(Key + ".runtimeDialogue", false);
            RunCore(false, false, 12, true);
        }
        public static void RunRuntimeDialogue()
        {
            SessionState.SetBool(Key + ".runtimeTerminals", false);
            SessionState.SetBool(Key + ".runtimeSpatial", false);
            SessionState.SetBool(Key + ".runtimeDialogue", true);
            RunCore(false, false, 12, true);
        }
        public static void RunRuntimeTerminals()
        {
            try
            {
                OfficialMap12PuzzleValidation.Validate();
                SessionState.SetBool(Key + ".runtimeTerminals", true);
                SessionState.SetBool(Key + ".runtimeSpatial", false);
                RunCore(false, false, 12, true);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void RunRuntimeSpatial()
        {
            SessionState.SetBool(Key + ".runtimeTerminals", false);
            SessionState.SetBool(Key + ".runtimeSpatial", true);
            RunCore(false, false, 12, true);
        }
        public static void RunDialogueInput() => RunCore(false, false, 12, false, true);
        public static void RunDialogueInputMap11() => RunCore(false, false, 11, false, true);
        public static void ImportUiAndRunRuntime()
        {
            try { OfficialMap11UiTools.ImportForMap(12); RunRuntime(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void RunUiMap11Regression()
        {
            try { OfficialMap11UiTools.ImportForMap(11); RunCore(true, false, 11); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void RunCore(bool verifyUi, bool import = true, int uiMapClassId = 12, bool verifyRuntime = false,
            bool verifyDialogueInput = false)
        {
            try
            {
                if (import) OfficialMap12Tools.ImportAndValidate();
                if (verifyUi && import) OfficialMap11UiTools.ImportForMap(12);
                ProjectTools.Validate();
                SessionState.SetBool(Key + ".ui", verifyUi);
                SessionState.SetBool(Key + ".runtime", verifyRuntime);
                SessionState.SetBool(Key + ".dialogueInput", verifyDialogueInput);
                SessionState.SetInt(Key + ".uiMap", uiMapClassId);
                SessionState.SetBool(Key, true);
                SessionState.SetInt(Key + ".result", 1);
                SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Hook();
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }

        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map12 source rendering timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map12SourceRenderingRunner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log(SessionState.GetBool(Key + ".dialogueInput", false) ? "Map" + SessionState.GetInt(Key + ".uiMap", 12) + " original dialogue raycast and simulated pointer input passed."
                : SessionState.GetBool(Key + ".runtime", false) ? "Map12 runtime input and save checks passed. Complete map verification remains separate."
                : SessionState.GetBool(Key + ".ui", false)
                ? "Map" + SessionState.GetInt(Key + ".uiMap", 12) + " native UI Play Mode passed: two portrait scales, goddess icon states and simulated button input. Full map and persistence remain separate."
                : "Map12 source Play Mode rendering passed. Gameplay, UI, puzzles and save recovery are not covered.");
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator VerifySteps()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                throw new InvalidOperationException("Map12 graphics must run with Direct3D12.");
            if (SessionState.GetBool(Key + ".dialogueInput", false))
            {
                IEnumerator dialogueSteps = VerifyDialogueInputSteps();
                while (dialogueSteps.MoveNext()) yield return dialogueSteps.Current;
                yield break;
            }
            if (SessionState.GetBool(Key + ".runtime", false))
            {
                IEnumerator runtimeSteps = VerifyRuntimeSteps();
                while (runtimeSteps.MoveNext()) yield return runtimeSteps.Current;
                yield break;
            }
            if (SessionState.GetBool(Key + ".ui", false))
            {
                IEnumerator uiSteps = VerifyUiSteps();
                while (uiSteps.MoveNext()) yield return uiSteps.Current;
                yield break;
            }
            var session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), 12);
            var terrain = UnityEngine.Object.Instantiate(session.Assets.GroundPrefab);
            var camera = new GameObject("Map12SourceCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var origin = session.Definition.Navigation.Origin;
            var entityRoot = new GameObject("Map12SourceEntities");
            var entities = entityRoot.AddComponent<OfficialMap10EntityView>();
            entities.Initialize(entityRoot.transform, new Vector2(origin.X, origin.Y), camera, 12, "Map12/Entities/native-catalog");
            yield return null;
            if (terrain.GetComponentsInChildren<OfficialMap10TerrainView>().Sum(view => view.BatchCount) != 18683 + 56
                || entities.EntityCount != 2591)
                throw new InvalidDataException("Map12 terrain/base ground/entity runtime counts differ from source.");
            var frame = new RenderTexture(450, 800, 24, RenderTextureFormat.ARGB32);
            frame.Create();
            camera.targetTexture = frame;
            camera.aspect = 450f / 800f;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.tools/map12-source-render-session"));
            Directory.CreateDirectory(output);
            foreach (int id in new[] { 101, 746, 202 })
            {
                var entity = session.Definition.Entities.Single(value => value.Id == id);
                camera.transform.position = new Vector3(origin.X + entity.Grid[0], origin.Y + entity.Grid[1], -100);
                yield return null;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = frame;
                var image = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, id + ".png"), image.EncodeToPNG());
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(image);
            }
            camera.targetTexture = null;
            frame.Release();
            UnityEngine.Object.Destroy(frame);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
