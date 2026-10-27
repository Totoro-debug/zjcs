using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using Zjcs.Presentation.World.NativeMap13;
using Zjcs.Official.World.NativeMap13Labels;
using UnityEngine.Tilemaps;
using TMPro;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13NativeWorldHostAcceptance
    {
        private const string Key = "Zjcs.Map13.NativeWorldHostAcceptance";
        private static bool started;

        public static void Run()
        {
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
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
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 runtime acceptance timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13NativeWorldHostAcceptanceRunner").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Map13 native world host passed: original partitions, environment, region labels and local language/font services at three regions; original private/public state transitions and disposal. No gameplay, simulated input or save migration is proved by this fixture.");
            EditorApplication.isPlaying = false;
        }

        private static bool hadError;
        private static void Observe(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) hadError = true;
        }
        private static IEnumerator VerifySteps()
        {
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(value => value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            {
                startup.StopAllCoroutines();
                UnityEngine.Object.Destroy(startup.gameObject);
            }
            hadError = false;
            Application.logMessageReceived += Observe;
            var fixture = new State();
            var cameraObject = new GameObject("Map13 native host camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000f;
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.03f, .03f, .03f, 1f);
            var target = new RenderTexture(540, 960, 24);
            target.Create(); camera.targetTexture = target;
            cameraObject.transform.position = fixture.Position + new Vector3(0, 0, -500);
            var root = new GameObject("Map13 native host fixture");
            var view = OfficialMap13NativeWorldView.Create(root.transform, camera, fixture);
            float deadline = Time.realtimeSinceStartup + 90;
            while (!view.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            if (!view.IsReady) throw new InvalidDataException("Original Map13 initial partition did not become ready.");
            foreach (int region in new[] { 0, 55, 104 })
            {
                fixture.MoveToRegion(region);
                cameraObject.transform.position = fixture.Position + new Vector3(0, 0, -500);
                yield return view.GroundRoot.PartitionController.LoadByPlayerPos();
                yield return null; yield return null;
                if (!view.GroundRoot.PartitionController.Loader.IsPosReady(fixture.Position))
                    throw new InvalidDataException("Original Map13 moving partition is not ready: " + region);
                if (!root.GetComponentsInChildren<TilemapRenderer>(true).Any())
                    throw new InvalidDataException("Original Map13 native tiles were not instantiated.");
                yield return new WaitForEndOfFrame();
                TraceLabels(root, camera, region);
                Capture(target, "private-" + region);
                fixture.PublicRegions.Add(region);
                view.UnlockRegion(fixture.Position);
                yield return new WaitForSecondsRealtime(5.2f);
                yield return new WaitForEndOfFrame();
                Capture(target, "public-" + region);
                if (!fixture.VisibilityUnlocks.Contains(region))
                    throw new InvalidDataException("Original Map13 region visibility callback was not forwarded: " + region);
                if (hadError) throw new InvalidDataException("Original Map13 host logged a runtime error.");
            }
            view.Dispose(); UnityEngine.Object.Destroy(root);
            yield return null; yield return null;
            if (hadError) throw new InvalidDataException("Original Map13 native host disposal logged an error.");
            Application.logMessageReceived -= Observe;
            camera.targetTexture = null; target.Release();
            UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
        }
        private static void TraceLabels(GameObject root, Camera camera, int region)
        {
            if (!TMP_Settings.instance) throw new InvalidDataException("The project TMP Resources settings were omitted from this player fixture.");
            var texts = root.GetComponentsInChildren<TextMeshPro>(true).Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)).ToArray();
            foreach (var text in texts)
                if (text.textInfo == null || text.textInfo.characterCount == 0)
                    throw new InvalidDataException("Original Map13 text did not generate its mesh through the normal runtime lifecycle: " + text.text);
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var visible = texts.Where(t => t.GetComponent<Renderer>() && GeometryUtility.TestPlanesAABB(planes, t.GetComponent<Renderer>().bounds)).ToArray();
            if (!visible.Any(t => t.GetComponent<Zjcs.Official.World.NativeLabelUI.MultiLanguageText>()?.myKey == "region_13_" + region))
                throw new InvalidDataException("Original Map13 region name is outside the rendered camera: " + region);
            Debug.Log("Map13 original text meshes at region " + region + ": " + texts.Length + " active; " + visible.Length + " in camera; " + string.Join(" / ", visible.Select(t => t.text)));
        }

        private static void Capture(RenderTexture target, string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = previous;
            Color32[] pixels = image.GetPixels32();
            int distinct = pixels.Where((p, i) => i % 17 == 0).Distinct().Count();
            if (distinct < 128) throw new InvalidDataException("Original Map13 rendered an empty frame: " + name);
            string output = Path.GetFullPath("../.tools/map13-native-world-current");
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            Debug.Log("Map13 native world rendered " + name + ": " + distinct + " sampled colors.");
        }
        private sealed class State : IMap13NativeWorldState
        {
            private readonly Map13LabelData data = JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
            public Vector3 Position = new Vector3(4.5f, 74.5f, 0f);
            public readonly HashSet<int> PublicRegions = new HashSet<int>();
            public readonly HashSet<int> VisibilityUnlocks = new HashSet<int>();
            private readonly Dictionary<int, Vector3> labelPositions = new Dictionary<int, Vector3>();
            public State()
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Official/World/Map/13/NativeRegionLabels" }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var label in prefab.GetComponentsInChildren<MapRegionDescSupportUI>(true))
                    {
                        Vector3 position = label.transform.position;
                        int region = data.QueryRegion(position);
                        if (region >= 0 && !labelPositions.ContainsKey(region)) labelPositions.Add(region, position);
                    }
                }
            }
            public Vector3 PlayerWorldPosition => Position;
            public int PlayerLevel => 90;
            public int PlayerSubRank => 12;
            public long PlayerCombatRating => 100000;
            public bool IsPublicRegion(int region) => PublicRegions.Contains(region);
            public int GetRegionProgress(int region) => 0;
            public int GetRegionGoddessId(int region) => 0;
            public void UnlockAllRegionPositions(int region) => VisibilityUnlocks.Add(region);
            public void MoveToRegion(int region)
            {
                if (!labelPositions.TryGetValue(region, out Vector3 position))
                    throw new InvalidDataException("Original Map13 region label position is unbound: " + region);
                Position = new Vector3(position.x, position.y, 0f);
            }
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
