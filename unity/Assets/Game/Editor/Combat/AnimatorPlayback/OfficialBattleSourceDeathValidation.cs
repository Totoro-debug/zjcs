using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSourceDeathValidation
    {
        private const string Key = "Zjcs.Battle.SourceDeathValidation";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Source death validation timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += (_, __, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            new GameObject("Original source death checks").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            try
            {
                using var library = OfficialBattleNativeAnimationLibrary.LoadDefault();
                foreach (int monsterId in new[] { 3322, 3803 })
                {
                    var root = new GameObject("Source death scene");
                    var fixture = new GameObject("Stop callback fixture"); fixture.SetActive(false);
                    fixture.AddComponent<TimedPlayable>();
                    fixture.AddComponent<ApplyOnStopCheckSourceDie>();
                    var checkpoint = new FightBattleCheckpoint();
                    var unit = new FightBattleUnitState { Id = 1, CharacterType = 2, MonsterId = monsterId, Hp = 100, MaxHp = 100 };
                    checkpoint.Units.Add(unit);
                    var mapper = new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 8, Height = 8 });
                    OfficialBattleNativeModelView model = null;
                    using var scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(), root.transform,
                        _ => null, mapper, (_, __) => null, (_, __) => { }, nativeNpcFactory: state =>
                        model = new OfficialBattleNativeModelView(library, library.FindModel(library.ResolveModelKey(state)), root.transform, "Actual original monster"));
                    using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
                    using var playback = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope,
                        new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope, _ => null));
                    playback.BindSourceDeathView(scene);
                    try
                    {
                        scene.Refresh(checkpoint);
                        var source = model.Transform.gameObject;
                        var graph = scope.Play(fixture, source, source);
                        Stop(scope, graph);
                        Require(model.Animation == "idle_01", "living source entered death");
                        unit.Hp = 0; scene.Refresh(checkpoint);
                        Require(model.Animation == "die_01", "real native model did not enter death");
                        model.Tick(.1f);
                        Require(Elapsed(model) > .09f, "real death clip did not advance");
                        var replay = scope.Play(fixture, source, source);
                        Require(ReferenceEquals(graph, replay), "original pool did not reuse callback graph");
                        Stop(scope, replay);
                        Require(model.Animation == "die_01" && Elapsed(model) == 0,
                            "stop callback did not restart an already running death clip");
                        yield return null;
                        Capture(source, monsterId + "-death");
                        model.Tick(.05f);
                        unit.Hp = 100;
                        replay = scope.Play(fixture, source, source); Stop(scope, replay);
                        Require(Elapsed(model) > .04f, "same-hit revival incorrectly restarted death");
                        scene.Refresh(checkpoint);
                        Require(model.Animation == "idle_01", "revived source did not return to idle");
                        unit.Hp = 0;
                        ((IBattleSourceDeathView)scene).ForcePlayDiePlayable(model.ModelTransform.gameObject);
                        ((IBattleSourceDeathView)scene).ForcePlayDiePlayable(null);
                        Require(model.Animation == "idle_01", "null/model descendant changed animation state");
                        checkpoint.Units.Clear();
                        ((IBattleSourceDeathView)scene).ForcePlayDiePlayable(source);
                        Require(model.Animation == "idle_01", "removed source changed animation state");
                    }
                    finally
                    {
                        scope.Dispose(); scene.Dispose();
                        Object.Destroy(fixture); Object.Destroy(root);
                    }
                    yield return null;
                }
                Require(errors == 0, "Unity reported errors");
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Source death Play Mode passed: actual original native monsters 3322/3803, live/dead/revived/removed sources, advancing and restarting actual death clips, production pre-Awake injection, original pooled stop callbacks and camera renders. Attached player death motions and full battle/Windows Player remain incomplete.");
            }
            finally { EditorApplication.isPlaying = false; }
        }

        private static float Elapsed(OfficialBattleNativeModelView model) =>
            (float)typeof(OfficialBattleNativeModelView).GetField("elapsed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(model);
        private static void Stop(OfflineBattleNativePlayableScope scope, MyPlayable graph)
        { scope.Stop(graph, true); scope.Tick(0); scope.LateTick(); }
        private static void Capture(GameObject model, string name)
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Missing validation workspace.");
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = default; bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            Require(found, "original model has no visible renderers");
            var go = new GameObject("Actual death camera"); var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(.1f, Mathf.Max(bounds.extents.y, bounds.extents.x / (384f / 512f)) * 1.3f);
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var rt = new RenderTexture(384, 512, 24); var texture = new Texture2D(384, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 384, 512), 0, 0); texture.Apply();
                int visible = 0; foreach (var pixel in texture.GetPixels32())
                    if (pixel.r > 16 || pixel.g > 16 || pixel.b > 16) visible++;
                Require(visible > 100, "actual death camera rendered no character pixels");
                File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(go); Object.Destroy(rt); Object.Destroy(texture); }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Source death: " + message); }
        private sealed class Runner : MonoBehaviour { }
        public sealed class TimedPlayable : MyPlayable { public override float GetDuration() => 10f; }
    }
}
