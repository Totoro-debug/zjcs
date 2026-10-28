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
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14EntityRadar;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using MyContainer = Zjcs.Official.EntityContainers.MyContainer;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityRadarAcceptance
    {
        private const string Key = "Map14.EntityRadar";
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
                Debug.Log("Map14 original entity radar core Play Mode passed: native grid/settings/body/removal queries, original scan enter/leave and subscription teardown, provider override, anchor formula, tip hide callbacks and original pooled tip reuse; award/input/component/index/container/loading/four-statue regression passed. Specialized tip components, original radar prefab payloads, formal player/input/UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }
        private static IEnumerator Steps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            using (var configuration = new NativeMap14SharedConfiguration(shared))
            {
                var prefabs = new NativeMap14SharedPrefabs(shared, catalog);
                var views = new Dictionary<object, NativeMap14SharedViewEntity>();
                Func<object, NativeMap14SharedViewEntity> wrap = native => views[native];
                var dispatcher = Activator.CreateInstance(shared.CommonType("EcContainerRemoveEntityEventDelegate"));
                using (var removal = new NativeMap14RadarRemovalEvents(shared, dispatcher, wrap))
                {
                    var containerHost = new GameObject("Original radar container fixture");
                    var container = containerHost.AddComponent<MyContainer>(); container.MyContainerHandler = new ContainerHandler();
                    var hosts = new List<GameObject>(); var entities = new List<MyEntity>();
                    var supports = new List<MapControllerEntityRadarSupport>();
                    var ground = Activator.CreateInstance(shared.CommonType("GridGroundComponent"));
                    var positions = (IDictionary)GetPrivate(ground, "mPosDataDict");
                    var active = false;
                    var queries = new NativeMap14RadarWorldQueries(shared, configuration, ground, () => active, wrap);
                    Require(queries.MapStageCostShowDistance == (int)NativeMap14SharedPrefabs.Get(configuration.Settings, "MapStageCostShowDistance"), "original radar distance differs");
                    Require(queries.FindGamePosData(new Pos2d(0, 0)) == null, "uninitialized input did not short circuit");
                    for (int id = 6570; id <= 6573; id++)
                    {
                        var native = prefabs.CreateClientPrefab(id);
                        Set(native, "Id", Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), 14L, id));
                        var view = new NativeMap14SharedViewEntity(shared, native); views.Add(native, view);
                        NativeMap14RadarWorldQueries.BindBody(view); NativeMap14RadarWorldQueries.BindBody(view);
                        var host = new GameObject("Original radar support fixture " + id); hosts.Add(host);
                        var entity = host.AddComponent<MyEntity>(); entity.Entity = view; entity.myContainer = container; entities.Add(entity);
                        var grid = host.AddComponent<MyGridTransform>(); grid.Bind();
                        var support = host.AddComponent<MapControllerEntityRadarSupport>(); support.RemovalEvents = removal; supports.Add(support);
                        CallPrivate(container, "AddEntity", entity);
                        Require(ReferenceEquals(support.MyEntity, entity) && ReferenceEquals(support.MyContainer, container) && ReferenceEquals(MapControllerEntityRadarSupportUtils.GetMapControllerEntityRadarSupport(entity), support), "original root support lookup differs");
                        var position = new Pos2d(id == 6570 ? 0 : id == 6571 ? 1 : id == 6572 ? -1 : 0, id == 6573 ? 1 : 0);
                        Set(view.Grid.Original, "Pos", Activator.CreateInstance(shared.CommonType("Pos2d"), position.X, position.Y));
                        var cell = Activator.CreateInstance(shared.CommonType("GamePosData"));
                        cell.GetType().GetMethod("DoAddEntity").Invoke(cell, new[] { native });
                        positions.Add(Activator.CreateInstance(shared.CommonType("Pos2d"), position.X, position.Y), cell);
                    }
                    active = true;
                    var cellData = queries.FindGamePosData(new Pos2d(1, 0));
                    Require(cellData.ExistEntitys() && ReferenceEquals(cellData.Entitys.Single(), entities[1].Entity), "native grid cell/order/entity identity differs");
                    var controllerHost = new GameObject("Original radar controller fixture"); controllerHost.SetActive(false);
                    var controller = controllerHost.AddComponent<Controller>(); controller.Entity = entities[0];
                    var manager = new CharacterManager(); manager.Current = controller;
                    var radar = controllerHost.AddComponent<RadarProbe>();
                    radar.MapMyCharacterManager = manager; radar.MapInput = new ScanQueries(queries); radar.MapRootManagerProxy = new Lookup(entities);
                    controllerHost.SetActive(true); radar.Begin();
                    Require(radar.Calls.Count == 3 && radar.Calls.All(call => call.StartsWith("enter:")) && removal.ReceiverCount == 3,
                        "original scan did not enter the three adjacent entities");
                    var offsets = (IList)GetPrivate(typeof(MapControllerEntityRadar), radar, "mAroundPosOffsetList");
                    Require(offsets.Count == 4, "original Manhattan radius-one offsets differ");
                    var firstSupport = supports[1];
                    dispatcher.GetType().GetMethod("FireEvent").Invoke(dispatcher, new[] { ((NativeMap14SharedViewEntity)entities[2].Entity).Original, null });
                    Require(radar.Calls.Last() == "remove:6572:True" && removal.ReceiverCount == 2, "original native removal callback differs");
                    var overrideProvider = new ProviderProbe(firstSupport);
                    firstSupport.SetOverriderRadarTipProvider(overrideProvider);
                    Require(ReferenceEquals(firstSupport.RadarTipProvider, overrideProvider) && overrideProvider.Entered && overrideProvider.Instance && removal.ReceiverCount == 1,
                        "original provider override order differs");
                    hosts[1].SetActive(false); Require(!ReferenceEquals(firstSupport.RadarTipProvider, overrideProvider), "disable did not clear the provider override"); hosts[1].SetActive(true);
                    controllerHost.SetActive(false);
                    Require(((NativeMap14SharedViewEntity)entities[0].Entity).Grid.ReceiverCount == 0, "original scan grid subscription leaked");
                    foreach (var support in supports) support.RadarTipProvider.OnDisable();
                    Require(removal.ReceiverCount == 0, "provider disable left native remove receivers");
                    // A body fixture exercises all anchor branches independently of prefab geometry.
                    var bodyHost = new GameObject("Original radar anchor fixture");
                    var bodyEntity = bodyHost.AddComponent<MyEntity>(); bodyEntity.Entity = new BodyEntity();
                    float[] xs = { -3.5f, 0f, 3.5f }; float[] ys = { 2.5f, 0.5f, -1.5f };
                    for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
                        Require(MapMyEntityUtils.GetOffsetByAnchor(bodyEntity, (HorizontalAnchor)x, (VerticalAnchor)y) == new Vector3(xs[x], ys[y], 0), "original anchor arithmetic differs");
                    var poolHost = new GameObject("Original radar tip pool fixture"); var tipManager = poolHost.AddComponent<MapControllerEntityRadarTipManager>();
                    var prefabHost = new GameObject("Original camp tip component fixture"); prefabHost.SetActive(false);
                    var prefab = prefabHost.AddComponent<MapControllerEntityRadarTipStageCamp>();
                    var previousFactory = StoryPlayableInstantiation.Resolve;
                    try
                    {
                        StoryPlayableInstantiation.Resolve = scope => new Factory();
                        var tip = tipManager.GetTip(bodyHost.transform, prefab); tip.FixedOffset = new Vector3(2, 3, 0); tip.Init(bodyEntity);
                        Require(tip.transform.localPosition == new Vector3(-1.5f, 5.5f, 0) && tip.CanShowTip(bodyEntity), "original tip anchor/offset differs");
                        int hidden = 0; tip.Hide(false, value => { Require(ReferenceEquals(tip, value), "hide callback identity differs"); hidden++; });
                        Require(hidden == 1, "missing animation did not complete hide immediately");
                        tip.transform.localScale = new Vector3(-2, 3, 1); tip.Init(bodyEntity);
                        Require(tip.transform.localScale.x == 2, "original tip flip correction differs");
                        tipManager.RecycleTip(tip); var borrowed = tipManager.GetTip(bodyHost.transform, prefab);
                        Require(ReferenceEquals(tip, borrowed), "original tip pool did not reuse the component");
                        borrowed.Hide(true, value => hidden++); Require(hidden == 2, "immediate hide callback differs");
                    }
                    finally { StoryPlayableInstantiation.Resolve = previousFactory; }
                    UnityEngine.Object.Destroy(prefabHost); UnityEngine.Object.Destroy(poolHost); UnityEngine.Object.Destroy(bodyHost);
                    UnityEngine.Object.Destroy(controllerHost);
                    foreach (var entity in entities) CallPrivate(container, "RemoveEntity", entity);
                    foreach (var host in hosts) UnityEngine.Object.Destroy(host);
                    UnityEngine.Object.Destroy(containerHost); yield return null;
                    foreach (var view in views.Values) view.Dispose();
                }
            }
            yield return (IEnumerator)typeof(OfficialMap14EntityAwardHookAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private static object GetPrivate(object owner, string name) => GetPrivate(owner.GetType(), owner, name);
        private static object GetPrivate(Type type, object owner, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static object CallPrivate(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);
        private static void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
        private sealed class Controller : MonoBehaviour, IRadarCharacterController
        { public MyEntity Entity; public MyEntity MyEntity => Entity; public Pos2d GridPos => Entity.myGridTransform.Pos; }
        private sealed class CharacterManager : IRadarCharacterManager
        { public IRadarCharacterController Current; public IRadarCharacterController MyMeUiCharacter => Current;
            public event Action<IRadarCharacterController, IRadarCharacterController> OnMeMapControllerUpdated { add { } remove { } } }
        private sealed class RadarProbe : MapControllerEntityRadar
        {
            public readonly List<string> Calls = new List<string>(); protected override void Start() { }
            public void Begin() { base.Start(); }
            public override MapControllerEntityRadarTipBase AddTip(IMapControllerEntityRadarSupport support, MapControllerEntityRadarTipBase prefab)
            { Calls.Add("enter:" + support.MyEntity.ClassId); return null; }
            public override void RemoveTip(MyEntity entity, bool immidietaly = false) { Calls.Add("remove:" + entity.ClassId + ":" + immidietaly); }
        }
        private sealed class ScanQueries : IRadarWorldQueries
        { private readonly IRadarWorldQueries source; public ScanQueries(IRadarWorldQueries source) { this.source = source; }
            public int MapStageCostShowDistance => 1; public IRadarGamePosData FindGamePosData(Pos2d position) => source.FindGamePosData(position); }
        private sealed class Lookup : IWorldInputRootLookup
        {
            private readonly List<MyEntity> entities; public Lookup(List<MyEntity> entities) { this.entities = entities; }
            public int CurrentMapClassId => 14;
            public bool TryGetEntity(EcEntityIdentifier id, out MyEntity entity) { entity = entities.FirstOrDefault(value => value.Id == id); return entity; }
        }
        private sealed class ProviderProbe : MapControllerEntityRadarTipProviderBase
        {
            public bool Entered, Instance; public ProviderProbe(IMapControllerEntityRadarSupport source) : base(source) { }
            public override void EntityEnter(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support, bool isInstance = false) { Entered = true; Instance = isInstance; }
            public override void EntityLeave(MapControllerEntityRadar radar, IMapControllerEntityRadarSupport support) { }
        }
        private sealed class ContainerHandler : IMyContainerHandler
        {
            public MyContainer MyContainer => null; public IClientEntityContainer Container => null;
            public EntityGridGroundData GridGround => null; public MyEntity MyRootEntity => null; public ECViewEntityData RootEntity => null;
            public void RegisterContainer(MyContainer container, IClientEntityContainer original) { } public void UnregisterContainer() { }
        }
        private sealed class BodyEntity : ECViewEntityData, IRadarBodyInfo
        {
            public override int ClassId => 6570; public override EcEntityIdentifier Id => new EcEntityIdentifier(14, 1);
            public override T GetComponent<T>() => this is T value ? value : default;
            public Vector2 RealCenter => new Vector2(1, 1);
            public IEnumerable<RadarBodyPosInfo> BodyRangeInfos => new[] { new RadarBodyPosInfo { Pos = new Pos2d(-2, 1) }, new RadarBodyPosInfo { Pos = new Pos2d(4, 3) } };
        }
        private sealed class Factory : IStoryPlayableInstanceFactory
        {
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            {
                Require(!prefab.activeSelf, "tip factory fixture must start inactive before naming/injection");
                var result = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays);
                // APK Framework.Core ServiceProviderExtensions.SetName copies
                // prefab.name before injection/reactivation (0x0b74e1ac).
                result.name = prefab.name;
                return result;
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
