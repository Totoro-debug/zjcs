using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialBattleDamagePlaybackAcceptance
    {
        private const string Key = "Zjcs.NativeDamageAcceptance";
        static OfficialBattleDamagePlaybackAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Original damage validation").AddComponent<NativeDamageAcceptanceRunner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetInt(Key + ".exit", 1);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        internal static void Finish(string failure)
        {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "native-damage-result.json"), JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
            SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class Result { public bool passed; public string error; }
    }

    public sealed class NativeDamageAcceptanceRunner : MonoBehaviour
    {
        [Serializable] private sealed class Catalog { public Binding[] damages; }
        [Serializable] private sealed class Binding
        {
            public int damage_entity_id;
            public bool component_enabled;
            public string entity_resource;
            public Flags flags;
            public Graph[] playables;
        }
        [Serializable] private sealed class Flags { public bool myShowZeroDamage, myHideDamage; }
        [Serializable] private sealed class Graph { public string field, resource; }
        private sealed class Played
        {
            public GameObject Graph, Source, Target, Damage;
        }
        private sealed class Grid : ECGridTransformData
        {
            public override Pos2d Pos => new Pos2d(7, 5);
            public override Direction Direction => Direction.Right;
            public override bool FlipX => false;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d point) => new Vector3(point.X, point.Y, 0);
            public override Pos2d PositionToGrid(Vector3 point) => new Pos2d((int)point.x, (int)point.y);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
        }
        private sealed class Services : IFightDamageViewServices
        {
            public OfficialBattleSkillEntityPlayback Skills;
            public EcEntityIdentifier Missing;
            public readonly List<string> Notifications = new List<string>();
            public MyEntity GetEntity(EcEntityIdentifier id) => id.Equals(Missing) ? null : Skills.GetEntity(id);
            public Vector3 GridToPos(Pos2d position) => Skills.GridToPos(position);
            public void FireDamageEvent(MyEntity target, MyFightDamage damage) => Notifications.Add("target");
            public void FireHitEvent(MyEntity source, MyFightDamage damage) => Notifications.Add("source");
        }
        private Camera cameraView;
        private OfflineBattleNativePlayableScope scope;
        private OfficialBattleSkillEntityPlayback skills;
        private OfficialBattleDamageEntityPlayback damages;
        private IDisposable pipeline;
        private string failure;
        private int serial;
        private readonly List<Played> played = new List<Played>();
        private void LateUpdate() { if (scope != null) scope.LateTick(); }

        private IEnumerator Start()
        {
            Application.logMessageReceived += Observe;
            var iterator = Check();
            while (true)
            {
                object current;
                try { if (!iterator.MoveNext()) break; current = iterator.Current; }
                catch (Exception error) { failure = error.ToString(); break; }
                yield return current;
            }
            try { damages?.Dispose(); skills?.Dispose(); }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            damages = null; skills = null; scope = null;
            try { pipeline?.Dispose(); }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            Application.logMessageReceived -= Observe;
            OfficialBattleDamagePlaybackAcceptance.Finish(failure);
        }

        private IEnumerator Check()
        {
            AudioListener.volume = 0f;
            Application.runInBackground = true;
            Time.maximumDeltaTime = 1f / 60f;
            var assets = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
            Require(assets, "Original battle render pipeline is packaged.");
            pipeline = assets.Enter();
            cameraView = new GameObject("Native damage camera").AddComponent<Camera>();
            cameraView.transform.position = new Vector3(6, 5, -20);
            cameraView.orthographic = true;
            cameraView.orthographicSize = 4;
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(.06f, .09f, .15f);
            var root = new GameObject("Native damage coordinates").transform;
            var grid = new FightBattleGrid { Width = 16, Height = 16 };
            var mapper = new OfficialBattleGridMapper(root, grid);
            var sourceState = new FightBattleUnitState { Id = 1, CharacterType = 1, X = 4, Y = 5, Hp = 100, MaxHp = 100 };
            var targetState = new FightBattleUnitState { Id = 2, CharacterType = 2, X = 7, Y = 5, Hp = 100, MaxHp = 100 };
            var units = new[] { sourceState, targetState };
            var source = new GameObject("Original damage source").transform;
            var target = new GameObject("Original damage target").transform;
            source.SetParent(root, false); target.SetParent(root, false);
            source.localPosition = new Vector3(4, 5); target.localPosition = new Vector3(7, 5);
            source.gameObject.AddComponent<MyTransform>(); target.gameObject.AddComponent<MyTransform>();
            // Actor stubs expose the material-role contract without inventing
            // character art. Actual character/material raster is a separate flow.
            foreach (var actor in new[] { source, target })
            {
                var renderer = actor.gameObject.AddComponent<MeshRenderer>();
                renderer.enabled = false;
                renderer.sharedMaterials = Array.Empty<Material>();
                actor.gameObject.AddComponent<MeshMaterialProperty>().MeshRenderer = renderer;
            }
            scope = new OfflineBattleNativePlayableScope(root, null);
            var services = new Services();
            scope.BindNativePrefabAdapter(instance =>
            {
                var graph = instance.GetComponent<MyPlayable>();
                if (graph) graph.OnPlay += value =>
                {
                    if (!(value.TryGetParam("Damage") is GameObject damage)) return;
                    Require(services.Notifications.SequenceEqual(new[] { "target", "source" }),
                        "Original role notifications precede every damage graph.");
                    played.Add(new Played { Graph = instance, Source = value.mySource, Target = value.myTarget, Damage = damage });
                };
            });
            skills = new OfficialBattleSkillEntityPlayback(root, mapper, scope, id => units.FirstOrDefault(unit => unit.Id == id));
            services.Skills = skills;
            var skill = new FightBattleSkillDefinition { EntityId = 10100, Slot = -1, Duration = -1f, SkillTargetType = 1 };
            var presentation = FightBattleSkillCastPresentation.Capture(sourceState, skill, new List<FightBattleUnitState> { targetState });
            skills.Start(new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 10100,
                Presentation = presentation }, source, target);
            var skillEntity = root.GetComponentInChildren<MyFightSkill>().myEntity;
            // MyFightDamage queries the source through MyFightSkill.Source;
            // inject the same lookup boundary for the missing-source probe.
            skillEntity.GetComponent<MyFightSkill>().ViewServices = services;
            var targetEntity = target.GetComponent<MyEntity>();
            var sourceEntity = source.GetComponent<MyEntity>();
            damages = new OfficialBattleDamageEntityPlayback(root, root.GetComponentInChildren<MyContainer>(), services, scope);
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Combat/skill-native-graphs").text);
            Require(catalog.damages.Length == 1368, "Complete selected original damage entity catalog, including Global.");
            var fields = new[] { "myCommonPlayable", "myDamagePlayable", "myBlockPlayable", "myCritPlayable", "myTriggerStatusPlayable" };
            foreach (var binding in catalog.damages)
            {
                var prefab = Resources.Load<GameObject>(binding.entity_resource);
                Require(prefab && !prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null),
                    "Original damage prefab has missing bindings: " + binding.damage_entity_id);
                var view = prefab.GetComponent<MyFightDamage>();
                Require(view && view.enabled == binding.component_enabled
                    && view.myHideDamage == binding.flags.myHideDamage && view.myShowZeroDamage == binding.flags.myShowZeroDamage,
                    "Original damage flags/enabled state changed: " + binding.damage_entity_id);
                foreach (string field in fields)
                {
                    var reference = (GameObject)typeof(MyFightDamage).GetField(field).GetValue(view);
                    var expected = binding.playables.FirstOrDefault(value => value.field == field);
                    Require(reference == (expected == null ? null : Resources.Load<GameObject>(expected.resource)),
                        "Original damage graph pointer changed: " + binding.damage_entity_id + ":" + field);
                }
            }
            var ordinary = catalog.damages.Single(binding => binding.damage_entity_id == 10004);
            Require(new[] { "myCommonPlayable", "myDamagePlayable", "myBlockPlayable", "myCritPlayable" }
                .All(field => ordinary.playables.Any(value => value.field == field)), "Original shared 10004 damage graph fields.");
            var trigger = catalog.damages.Single(binding => binding.damage_entity_id == 13012);
            var commonOnly = catalog.damages.First(binding => binding.playables.Length == 1
                && binding.playables[0].field == "myCommonPlayable");
            var empty = catalog.damages.First(binding => binding.playables.Length == 0);
            var pooled = new Dictionary<int, MyEntity>();
            yield return null;
            foreach (var binding in new[] { ordinary, trigger, commonOnly, empty })
                for (int mask = 0; mask < 4; mask++)
                {
                    played.Clear(); services.Notifications.Clear();
                    bool block = (mask & 1) != 0, critical = (mask & 2) != 0;
                    var state = new OfficialBattleDamageEntityData(new EcEntityIdentifier(7, ++serial), binding.damage_entity_id,
                        skillEntity.Id, targetEntity.Id, block, critical, binding == trigger,
                        target.localPosition, Quaternion.identity, Vector3.one, new Grid());
                    var entity = damages.Create(state);
                    Require(services.Notifications.SequenceEqual(new[] { "target", "source" }),
                        "Original role notifications also occur when graph fields are empty.");
                    if (pooled.TryGetValue(binding.damage_entity_id, out var previous))
                        Require(ReferenceEquals(previous, entity), "Original damage FIFO pool reuses its removed instance.");
                    pooled[binding.damage_entity_id] = entity;
                    var view = entity.GetComponent<MyFightDamage>();
                    var expected = binding == trigger ? new[] { view.myTriggerStatusPlayable }
                        : binding == ordinary ? new[] { view.myCommonPlayable,
                            new[] { view.myDamagePlayable, view.myBlockPlayable, view.myCritPlayable, view.myBlockPlayable }[mask] }
                        : binding == commonOnly ? new[] { view.myCommonPlayable } : new GameObject[0];
                    Require(played.Count == expected.Length, "Original damage graph branch count.");
                    for (int index = 0; index < expected.Length; index++)
                        Require(played[index].Graph.name.Replace("(Clone)", "") == expected[index].name
                            && played[index].Source == target.gameObject && played[index].Target == source.gameObject
                            && played[index].Damage == entity.gameObject, "Original ordered graph, direction and Damage parameter.");
                    bool rejected = false;
                    try { damages.Create(state); } catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && damages.GetEntity(state.Id) == entity, "Duplicate identity leaves the original damage entity live.");
                    for (int frame = 0; frame < 15; frame++) { scope.Tick(1f / 60f); yield return null; }
                    // 10004's ordinary graph only drives the actor Animator and
                    // camera; this fixture validates its dispatch, not a model.
                    // 13012 contains the original visible particle graph.
                    if (binding == trigger) Capture("damage-" + binding.damage_entity_id + "-" + mask + ".png");
                    damages.Remove(state.Id);
                    Require(damages.Count == 0 && !entity.gameObject.activeSelf, "Original damage entity removed to its pool.");
                    for (int frame = 0; frame < 45; frame++) { scope.Tick(1f / 60f); yield return null; }
                }
            foreach (var missing in new[] { targetEntity.Id, skillEntity.Id, sourceEntity.Id })
            {
                services.Missing = missing;
                played.Clear(); services.Notifications.Clear();
                var state = new OfficialBattleDamageEntityData(new EcEntityIdentifier(7, ++serial), ordinary.damage_entity_id,
                    skillEntity.Id, targetEntity.Id, false, false, false, target.localPosition, Quaternion.identity, Vector3.one, new Grid());
                damages.Create(state);
                Require(played.Count == 0 && services.Notifications.SequenceEqual(missing.Equals(targetEntity.Id)
                    ? new string[0] : new[] { "target" }), "Original missing-target/skill/source branches.");
                damages.Remove(state.Id);
            }
            services.Missing = default;
            var originalOverride = catalog.damages.Single(binding => binding.damage_entity_id == 13202);
            var overridePrefab = Resources.Load<GameObject>(originalOverride.entity_resource);
            Require(!overridePrefab.GetComponent<MyPlayableOverrider>().enabled, "Retain originally disabled 13202 override driver.");
            var status = new OfficialBattleSkillSourceStatusSnapshot(presentation, sourceEntity.Id, OfficialCombatAssetCatalog.LoadDefault().Load());
            var overrideState = new OfficialBattleDamageEntityData(new EcEntityIdentifier(7, ++serial), 13202,
                skillEntity.Id, targetEntity.Id, false, false, false, target.localPosition, Quaternion.identity, Vector3.one, new Grid());
            played.Clear(); services.Notifications.Clear();
            var originalEntity = damages.Create(overrideState, status);
            Require(!originalEntity.GetComponent<MyPlayableOverrider>().enabled
                && originalEntity.GetComponent<MyFightDamage>().myCommonPlayable == overridePrefab.GetComponent<MyFightDamage>().myCommonPlayable,
                "Disabled original status override is not enabled by local loading.");
            damages.Remove(overrideState.Id);
            Debug.Log("Original damage entities passed: all 1368 native prefabs and graph fields, ordered notifications, common/block/crit/status branches, actual Damage parameters, missing entities, duplicate identities, pool removal and disabled original override.");
        }
        private void Observe(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failure = failure ?? text + "\n" + stack;
        }
        private void Capture(string name)
        {
            var target = new RenderTexture(540, 960, 24);
            var previous = RenderTexture.active;
            var oldTarget = cameraView.targetTexture;
            Texture2D image = null;
            try
            {
                cameraView.targetTexture = target; cameraView.Render(); RenderTexture.active = target;
                image = new Texture2D(540, 960, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG());
                Require(image.GetPixels32().Select(pixel => ((int)pixel.r << 16) | ((int)pixel.g << 8) | pixel.b)
                    .Distinct().Take(5).Count() >= 5, "Original damage capture is empty: " + name);
            }
            finally
            {
                cameraView.targetTexture = oldTarget; RenderTexture.active = previous;
                if (image) Destroy(image); Destroy(target);
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
