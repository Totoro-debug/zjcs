using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialBattleStatusModelAcceptance
    {
        private const string Key = "Zjcs.StatusModelAcceptance";
        static OfficialBattleStatusModelAcceptance()
        {
            EditorApplication.update += () => {
                if (!SessionState.GetBool(Key + ".done", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key + ".done", false);
                EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
            };
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) new GameObject("Status model validation").AddComponent<Runner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }
        public static void Run()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView", true);
            var property = type.GetProperty("enterPlayModeBehavior", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var view in Resources.FindObjectsOfTypeAll(type)) property.SetValue(view, Enum.Parse(property.PropertyType, "PlayUnfocused"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".done", false);
            SessionState.SetInt(Key + ".exit", 1); AudioListener.volume = 0;
            EditorApplication.isPlaying = true;
        }
        private sealed class Runner : MonoBehaviour
        {
            private string failure;
            private void Observe(string message, string stack, LogType type)
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = failure ?? message + "\n" + stack; }
            private IEnumerator Start()
            {
                Application.runInBackground = true; AudioListener.volume = 0; Time.timeScale = 0;
                Application.logMessageReceived += Observe;
                var run = Check();
                while (true)
                {
                    object current = null; bool more = false;
                    try { more = run.MoveNext(); if (more) current = run.Current; }
                    catch (Exception error) { failure = failure ?? error.ToString(); }
                    if (!more) break;
                    yield return current;
                }
                (run as IDisposable)?.Dispose(); Application.logMessageReceived -= Observe; Time.timeScale = 1;
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "status-model-result.json"),
                    JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
                SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1); SessionState.SetBool(Key + ".done", true);
                EditorApplication.isPlaying = false;
            }
        }
        [Serializable] private sealed class Result { public bool passed; public string error; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static FightBattleCheckpoint State()
        {
            var value = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 0 } };
            var unit = new FightBattleUnitState { Id = 1, CharacterType = 2, NpcClassId = 3204,
                MonsterId = 3204, MonsterTemplateId = 3204, Side = 1,
                X = 5, Y = 5, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000, Level = 150,
                SubRank = 1, MoveMask = 5, Direction = 3, HasFlipX = true };
            unit.Turn.Id = 1; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 100;
            foreach (var p in new[] { new[] { 1, 10001 }, new[] { 2, 10003 }, new[] { 3, 1000000 }, new[] { 4, 100 } })
                unit.Properties.Add(new FightPropertyValue { Id = p[0], Value = p[1] });
            value.Units.Add(unit);
            return value;
        }
        private static FightBattleStepEvent Event(FightBattleStatusViewEventKind kind, FightBattleStatusViewState state)
            => new FightBattleStepEvent { StatusViewEvents = new List<FightBattleStatusViewEvent> {
                new FightBattleStatusViewEvent { OwnerId = 1, Kind = kind, Status = state } } };
        private static IEnumerator Check()
        {
            using var pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets").Enter();
            foreach (int statusId in new[] { 10712, 24010, 24170 })
            foreach (int mode in new[] { 0, 1, 2, 3 })
            {
                var root = new GameObject("Status owner " + statusId + " mode " + mode);
                using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
                var assets = OfficialCombatAssetCatalog.LoadDefault(); var catalog = assets.Load(); var cp = State();
                var executor = FightBattleExecutor.Begin(cp, assets.LoadDamageFormula(), new FightDamageSettlement(), catalog, assets.LoadSkillItems());
                string saved = FightBattleCheckpointSerializer.Save(cp);
                var snapshot = FightBattleStatusViewState.Capture(catalog.CreateStatus(statusId, 1, 1, 100, 10100));
                var mapper = new OfficialBattleGridMapper(root.transform, cp.Grid);
                using var entities = new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope, id => cp.Units[0]);
                using var graphs = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope, entities);
                Transform body = null;
                Transform Resolve(int id) { if (id != 1 || !body) return null; entities.EnsureUnit(id, body); return body; }
                void CreateBody()
                {
                    body = new GameObject("Controlled owner " + statusId).transform; body.SetParent(root.transform, false);
                    body.position = mapper.GridToWorld(new FightGridPoint(5, 5));
                    body.gameObject.AddComponent<MyTransform>();
                }
                using var playback = new OfficialBattleStatusPlayback(executor, root.transform, scope, Resolve, _ => false, "Combat/first-promotion-statuses");
                playback.Restore();
                if (mode < 2) CreateBody();
                playback.Enqueue(Event(FightBattleStatusViewEventKind.Start, snapshot)); playback.DispatchDue();
                var original = root.GetComponentsInChildren<MyFightStatus>().SingleOrDefault();
                Require(playback.Count == (mode < 2 ? 1 : 0), "A status must wait for its original owner model.");
                MyEntity oldOwner = body ? body.GetComponent<MyEntity>() : null;
                if (mode == 0) CreateBody();
                if (mode == 1)
                {
                    UnityEngine.Object.Destroy(body.gameObject); body = null; yield return null;
                    playback.DispatchDue(); Require(playback.Count == 0, "Destroyed model retained an active status lease.");
                }
                var updated = FightBattleStatusViewState.Capture(catalog.CreateStatus(statusId, 1, 1, 100, 10100));
                updated.StackCount = 7; updated.Forbidden = true;
                playback.Enqueue(Event(FightBattleStatusViewEventKind.Update, updated)); playback.DispatchDue();
                if (mode == 2)
                {
                    playback.Enqueue(Event(FightBattleStatusViewEventKind.End, updated)); playback.DispatchDue();
                    CreateBody(); playback.DispatchDue(); Require(playback.Count == 0, "Ended deferred status appeared on a late model.");
                }
                else
                {
                    if (!body) CreateBody(); playback.DispatchDue();
                    var current = root.GetComponentsInChildren<MyFightStatus>().Single();
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject) == 0,
                        "A selected original status prefab lost a serialized script.");
                    Require(current.Context.Owner == body.gameObject && current.Component.StackedCount == 7 && current.Component.Forbidden,
                        "Rebound status lost its latest owner, layers or forbidden state.");
                    if (mode < 2) Require(current != original && !current.GetComponent<MyEntity>().myNewEntity,
                        "Recreated model reused its stale status view or was marked as a new status.");
                    if (mode == 3) Require(current.GetComponent<MyEntity>().myNewEntity, "First delayed attachment lost its original new-status flag.");
                    foreach (var action in current.GetComponents<MyAction>()) Require(action.myApplyEntity == body.GetComponent<MyEntity>(), "Action retained an old owner.");
                    if (mode == 0 && original)
                        foreach (var action in original.GetComponents<MyAction>()) Require(action.myApplyEntity == oldOwner, "Removing an old view redirected its action to the new model.");
                    var resumed = FightBattleStatusViewState.Capture(catalog.CreateStatus(statusId, 1, 1, 100, 10100));
                    resumed.StackCount = 7;
                    playback.Enqueue(Event(FightBattleStatusViewEventKind.Update, resumed)); playback.DispatchDue();
                    var stable = current; playback.DispatchDue(); playback.DispatchDue();
                    Require(root.GetComponentsInChildren<MyFightStatus>().Single() == stable
                        && body.GetComponent<MyFightStatusAgent>().MyStatuses.SetEquals(new[] { stable }), "Stable dispatch restarted or duplicated a status view.");
                    playback.Enqueue(Event(FightBattleStatusViewEventKind.End, resumed)); playback.DispatchDue();
                    Require(playback.Count == 0 && body.GetComponent<MyFightStatusAgent>().MyStatuses.Count == 0, "End failed to detach a rebound view.");
                }
                Require(FightBattleCheckpointSerializer.Save(cp) == saved, "Controlled owner lifecycle changed combat state.");
                playback.Dispose(); graphs.Dispose(); scope.Dispose(); UnityEngine.Object.Destroy(root); yield return null;
                Debug.Log("[Status model] " + statusId + " mode=" + mode + " passed.");
            }
            var native = NativeModelSteps(); while (native.MoveNext()) yield return native.Current;
        }

        private static IEnumerator NativeModelSteps()
        {
            var root = new GameObject("Actual 3204 status death and revival");
            using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
            var assets = OfficialCombatAssetCatalog.LoadDefault(); var catalog = assets.Load(); var cp = State();
            foreach (int id in new[] { 24010, 24170 }) cp.Units[0].Statuses.Add(catalog.CreateStatus(id, 1, 1, id, 10100));
            var executor = FightBattleExecutor.Begin(cp, assets.LoadDamageFormula(), new FightDamageSettlement(), catalog, assets.LoadSkillItems());
            var mapper = new OfficialBattleGridMapper(root.transform, cp.Grid);
            var models = ScriptableObject.CreateInstance<OfficialBattleModelCatalog>();
            using var scene = new OfficialBattleSceneView(models, root.transform, _ => null, mapper, (_, __) => "idle_01", (_, __) => {}, nativePlayableScope: scope);
            using var entities = new OfficialBattleSkillEntityPlayback(root.transform, mapper, scope, id => cp.Units[0]);
            using var graphs = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), scope, entities);
            typeof(OfficialBattleSceneView).GetMethod("BindUnitEndClock", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene, new object[] { graphs });
            Transform Resolve(int id) { var body = scene.TryGetUnitTransform(id); if (body) entities.EnsureUnit(id, body); return body; }
            using var playback = new OfficialBattleStatusPlayback(executor, root.transform, scope, Resolve, _ => false, "Combat/first-promotion-statuses");
            void Advance(float delta) { scene.Tick(delta); graphs.Tick(delta); playback.DispatchDue(); graphs.LateTick(); }
            scene.Refresh(cp); playback.Restore(); Advance(.05f); yield return null;
            var first = Resolve(1); Require(first && playback.Count == 2, "Actual 3204 initial states were not bound: model=" + (first != null) + ", states=" + playback.Count);
            Capture(first, "status-model-native-alive.png");
            cp.Units[0].Hp = 1;
            var step = new FightBattleStepEvent { HasUnitLifeEvents = true };
            step.UnitLifeEvents.Add(new FightBattleUnitLifeEvent { UnitId = 1, BeforeHp = 1000000, BeforeMaxHp = 1000000, Hp = 0, MaxHp = 1000000, IsDead = true, Delay = .2f });
            step.UnitLifeEvents.Add(new FightBattleUnitLifeEvent { UnitId = 1, BeforeHp = 0, BeforeMaxHp = 1000000, Hp = 1, MaxHp = 1000000, Delay = 1.6f });
            var saved = FightBattleCheckpointSerializer.Save(cp);
            scene.Refresh(cp, step); graphs.Play(step, cp, scene.TryGetUnitTransform);
            Advance(.21f); yield return null; Capture(Resolve(1), "status-model-native-death.png");
            Advance(.99f); Advance(.02f); yield return null; Advance(0);
            Require(!Resolve(1) && playback.Count == 0, "Released death model retained state effects.");
            Advance(.4f); yield return null; Advance(.02f);
            var revived = Resolve(1);
            Require(revived && !ReferenceEquals(first, revived) && playback.Count == 2, "Revived native model failed to restore its existing state effects.");
            Require(root.GetComponentsInChildren<MyFightStatus>().All(view => view.Context.Owner == revived.gameObject && !view.GetComponent<MyEntity>().myNewEntity), "Native revival retained a stale owner or restarted status identity.");
            Capture(revived, "status-model-native-revived.png");
            Require(FightBattleCheckpointSerializer.Save(cp) == saved, "Model lifecycle presentation changed the settled checkpoint.");
            playback.Dispose(); graphs.Dispose(); scene.Dispose(); scope.Dispose(); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(models);
            Debug.Log("[Status model] Actual 3204 death, release, revival, restored original state effects and unchanged checkpoint passed.");
        }
        private static void Capture(Transform body, string name)
        {
            Require(body, "Capture requires the actual model.");
            var renderer = body.GetComponentsInChildren<MeshRenderer>().First(value => value.enabled && value.bounds.size.sqrMagnitude > 0);
            var camera = new GameObject("Status model capture").AddComponent<Camera>();
            var light = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global; light.intensity = 1;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(2, renderer.bounds.extents.y * 2);
            camera.transform.position = renderer.bounds.center + new Vector3(0, 0, -20);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f, .23f, .28f);
            var target = new RenderTexture(540, 720, 24); var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(540, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 540, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; if (pixels) UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(camera.gameObject); }
        }
    }
}
