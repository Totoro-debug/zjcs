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
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using ClientContainer = Zjcs.Official.EntityContainers.MyContainer;
using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityContainerAcceptance
    {
        private const string Key = "Map14.EntityContainers";
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
                Debug.Log("Map14 original client container Play Mode passed: four subscription lifetimes, initial registration/root/group lookup, start/add ordering, prepare/replace dictionary identity, deferred fade/remove/unload, negative client identifiers, source ownership/grid assignment, story NPC uniqueness, immediate/bulk removal and teardown. Original loader and four-statue regression passed. Shared event/data adapters, formal world entity host, movement, pointer/UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity container: " + text); }
        private static IEnumerator Steps()
        {
            Trace.Clear();
            var host = new GameObject("Original client container source fixture"); host.SetActive(false);
            var container = host.AddComponent<ClientContainer>(); container.myEntitySlot = host.transform;
            var source = new Source(); var services = new Services(); var handler = new Registry();
            container.SharedServices = services; container.MyContainerHandler = handler; host.SetActive(true);
            var root = new Input(3000, new EcEntityIdentifier(14, 1));
            var other = new Input(3001, new EcEntityIdentifier(14, 2));
            source.Root = root; source.Entities.Add(root.Id, root); source.Entities.Add(other.Id, other);
            var loader = new Loader(container, source);
            container.OnInited += () => Trace.Add("inited");
            var receiver = new Receiver { Container = container };
            container.EntityAddEvent += (IMyContainerAddEntityEventReceiver)receiver;
            container.EntityRemoveEvent += (IMyContainerRemoveEntityEventReceiver)receiver;
            container.EntityPrepareReplaceEvent += (IMyContainerPrepareReplaceEntityEventReceiver)receiver;
            container.EntityReplaceEvent += (IMyContainerReplaceEntityEventReceiver)receiver;
            yield return container.Init(source, loader);
            Require(source.Subscriptions.SequenceEqual(new[] { "start", "destroy", "prepare", "replace" }), "subscription order changed");
            Require(container.Inited && container.MyRootEntity == container.GetEntity(root.Id) &&
                container.MyEntityDict.Count == 2 && container.ExistEntityGroupByClassId(other.ClassId), "initial registration or root/group lookup failed");
            Require(Trace.IndexOf("register") < Trace.IndexOf("inited"), "inited preceded registry registration");
            Require(Trace.IndexOf("start:3001") < Trace.IndexOf("add:3001"), "container add preceded component start");
            var current = container.GetEntity(other.Id);
            current.PrepareReplaceEvent += (Zjcs.Official.EntityLifecycle.IMyEntityPrepareReplaceEventReceiver)receiver;
            current.ReplaceEvent += (Zjcs.Official.EntityLifecycle.IMyEntityReplaceEventReceiver)receiver;
            var previous = other.Id; Trace.Clear(); source.Prepare(other, root);
            other.Identifier = new EcEntityIdentifier(14, 20);
            source.Entities.Remove(previous); source.Entities.Add(other.Id, other); source.Replace(other, previous);
            Require(Trace.SequenceEqual(new[] { "entity-prepare", "container-prepare", "entity-replace", "container-replace" }), "prepare/replace event order changed");
            Require(container.GetEntity(previous) == null && container.GetEntity(other.Id) == current, "replace did not move the existing client object");
            bool faded = false; current.FadeOutAction = () => faded; Trace.Clear(); source.Destroy(other);
            Require(container.GetEntity(other.Id) == current && Trace.SequenceEqual(new[] { "end:3001" }), "fade removal did not wait after End");
            yield return null; faded = true; yield return null; yield return null;
            Require(container.GetEntity(other.Id) == null && !container.ExistEntityGroupByClassId(other.ClassId), "fade removal retained dictionary/group membership");
            Require(Trace.SequenceEqual(new[] { "end:3001", "remove:3001", "removed:3001", "unload:3001" }), "fade/end/remove/unload order changed");

            Trace.Clear(); MyEntity client = null;
            container.MyClientEntityHandler.CreateClientEntity(3100, new Pos2d(5, 6), entity =>
            { client = entity; Require(entity.myStarted, "client callback preceded start"); Trace.Add("client-callback"); },
                initCallback: entity => { Require(!entity.myStarted, "client init followed start"); Trace.Add("client-init"); });
            var data = (Input)client.Entity;
            Require(client.Id == new EcEntityIdentifier(14, -1) && data.SourceData.EntitySourceType == EcEntitySourceType.None &&
                data.SourceData.Ownership.Type == EntityOwnershipType.Player && data.SourceData.Ownership.OwnerId == 123 &&
                data.GridData.Pos.X == 5 && data.GridData.Pos.Y == 6, "client identifier/ownership/grid changed");
            Require(Trace.IndexOf("client-init") < Trace.IndexOf("start:3100") && Trace.IndexOf("start:3100") < Trace.IndexOf("client-callback"),
                "client initialization/start/callback order changed");
            Require(services.Calls.Take(6).SequenceEqual(new[] { "prefab", "instantiate", "container", "identifier", "source", "grid" }) &&
                services.Calls[6] == "awake", "shared creation/component initialization order changed");
            MyEntity npc = null;
            container.MyClientStoryNPCEntityHandler.CreateClientEntity(3200, new Pos2d(7, 8), entity => npc = entity);
            Require(npc.Id == new EcEntityIdentifier(-1, -1), "story NPC identifier changed");
            int created = loader.Created.Count; bool duplicate = false;
            container.MyClientStoryNPCEntityHandler.CreateClientEntity(3200, new Pos2d(0, 0), entity => duplicate = entity == null);
            Require(duplicate && loader.Created.Count == created && container.MyClientStoryNPCEntityHandler.GetClientEntity(3200) == npc,
                "duplicate story NPC replaced the existing object");
            bool npcRemoved = false; container.MyClientStoryNPCEntityHandler.RemoveClientEntity(3200, () => npcRemoved = true);
            yield return null; Require(npcRemoved && container.MyClientStoryNPCEntityHandler.GetClientEntity(3200) == null, "story NPC remove callback/dictionary failed");
            bool clientRemoved = false; container.MyClientEntityHandler.RemoveClientEntity(-1, () => clientRemoved = true);
            yield return null; Require(clientRemoved && container.MyClientEntityHandler.GetClientEntity(-1) == null, "client remove callback/dictionary failed");

            var immediate = new Input(3300, new EcEntityIdentifier(14, 33)); source.Entities.Add(immediate.Id, immediate);
            source.Start(immediate, true); var immediateObject = container.GetEntity(immediate.Id);
            Require(immediateObject && immediateObject.myNewEntity && immediateObject.myStarted, "shared start did not create/start a new view");
            Trace.Clear(); container.DoEntityDestroyImmediate(immediateObject, null);
            Require(Trace.SequenceEqual(new[] { "end:3300", "remove:3300", "removed:3300", "unload:3300" }), "immediate destroy order changed");
            Trace.Clear(); container.RemoveAllEntities();
            Require(Trace.First() == "before-all" && container.MyEntityDict.Count == 0 && !container.ExistEntityGroupByClassId(root.ClassId), "bulk cleanup did not clear registrations");
            Require(Trace.Skip(1).SequenceEqual(new[] { "end:3000", "remove:3000", "removed:3000", "unload:3000" }), "bulk cleanup order changed");
            Trace.Clear(); UnityEngine.Object.Destroy(container); yield return null;
            Require(source.Active == 0 && loader.Disposed && Trace.Take(6).SequenceEqual(new[] { "unregister", "unsubscribe:start", "unsubscribe:destroy", "unsubscribe:prepare", "unsubscribe:replace", "dispose-loader" }),
                "container teardown changed registration/subscription/loader order");
            UnityEngine.Object.Destroy(host); yield return null;
            yield return (IEnumerator)typeof(OfficialMap14EntityLoadingAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private static readonly List<string> Trace = new List<string>();
        private sealed class Input : ECViewEntityData
        {
            private readonly int classId; public EcEntityIdentifier Identifier;
            public readonly SourceData SourceData = new SourceData(); public readonly GridData GridData = new GridData();
            public Input(int classId, EcEntityIdentifier identifier) { this.classId = classId; Identifier = identifier; }
            public override int ClassId => classId; public override EcEntityIdentifier Id => Identifier;
            public override T GetComponent<T>() => default;
        }
        private sealed class SourceData : IClientEntitySource
        { public EcEntitySourceType EntitySourceType { get; set; } public EntityOwnershipData Ownership { get; set; } }
        private sealed class GridData : IClientEntityGrid { public Pos2d Pos { get; set; } }
        private sealed class Source : IClientEntityContainer
        {
            public Input Root; public bool Destroying => false; public long Id => 14; public int ClassId => 14;
            public ECViewEntityData RootEntity => Root;
            public readonly Dictionary<EcEntityIdentifier, ECViewEntityData> Entities = new Dictionary<EcEntityIdentifier, ECViewEntityData>();
            public IReadOnlyDictionary<EcEntityIdentifier, ECViewEntityData> EntityDict => Entities;
            public readonly List<string> Subscriptions = new List<string>(); public int Active;
            private Action<ECViewEntityData, bool> start; private Action<ECViewEntityData, object> destroy;
            private Action<ECViewEntityData, ECViewEntityData> prepare; private Action<ECViewEntityData, EcEntityIdentifier> replace;
            private IDisposable Lease(string name, Action release)
            { Subscriptions.Add(name); Active++; return new Subscription(() => { Active--; Trace.Add("unsubscribe:" + name); release(); }); }
            public IDisposable SubscribeStartEntity(Action<ECViewEntityData, bool> callback) { start += callback; return Lease("start", () => start -= callback); }
            public IDisposable SubscribeDestroyEntity(Action<ECViewEntityData, object> callback) { destroy += callback; return Lease("destroy", () => destroy -= callback); }
            public IDisposable SubscribePrepareReplaceEntity(Action<ECViewEntityData, ECViewEntityData> callback) { prepare += callback; return Lease("prepare", () => prepare -= callback); }
            public IDisposable SubscribeReplaceEntity(Action<ECViewEntityData, EcEntityIdentifier> callback) { replace += callback; return Lease("replace", () => replace -= callback); }
            public void Start(Input entity, bool isNew) => start?.Invoke(entity, isNew);
            public void Destroy(Input entity) => destroy?.Invoke(entity, null);
            public void Prepare(Input source, Input target) => prepare?.Invoke(source, target);
            public void Replace(Input source, EcEntityIdentifier previous) => replace?.Invoke(source, previous);
        }
        private sealed class Loader : IEntityLoadHandler
        {
            private readonly ClientContainer container; private readonly Source source;
            public readonly List<MyEntity> Created = new List<MyEntity>(); public bool Disposed;
            public Loader(ClientContainer container, Source source) { this.container = container; this.source = source; }
            public IEnumerator LoadForInit(Action<MyEntity> callback)
            { foreach (var data in source.Entities.Values) CreateMyEntity(data, false, callback); yield break; }
            public void CreateMyEntity(ECViewEntityData data, bool isNew, Action<MyEntity> callback)
            {
                var obj = new GameObject("Original container lifecycle fixture"); obj.transform.SetParent(container.transform, false);
                var entity = obj.AddComponent<MyEntity>(); obj.AddComponent<Probe>(); Created.Add(entity);
                entity.Bind(container, data, isNew); callback?.Invoke(entity);
            }
            public void PreLoadGameObject(int classId, Action<MyEntity> callback = null) => throw new NotSupportedException();
            public void OnBeforeRemoveAllEntities() => Trace.Add("before-all");
            public void DestroyMyEntity(MyEntity entity) { Trace.Add("unload:" + entity.ClassId); UnityEngine.Object.Destroy(entity.gameObject); }
            public void Dispose() { Disposed = true; Trace.Add("dispose-loader"); }
        }
        private sealed class Probe : MyComponent
        {
            public override void OnStart(bool isNew) => Trace.Add("start:" + myEntity.ClassId);
            public override void OnEnd(object data) => Trace.Add("end:" + myEntity.ClassId);
            public override void OnRemove(object data) => Trace.Add("remove:" + myEntity.ClassId);
        }
        private sealed class Services : IClientEntityServices
        {
            public readonly List<string> Calls = new List<string>(); public long MainPlayerId => 123;
            public ECViewEntityData GetCommonPrefab(int classId) { Calls.Add("prefab"); return new Input(classId, default); }
            public ECViewEntityData Instantiate(ECViewEntityData prefab) { Calls.Add("instantiate"); return new Input(prefab.ClassId, default); }
            public void SetContainer(ECViewEntityData entity, IClientEntityContainer container) => Calls.Add("container");
            public void SetIdentifier(ECViewEntityData entity, EcEntityIdentifier id) { Calls.Add("identifier"); ((Input)entity).Identifier = id; }
            public void Awake(ECViewEntityData entity) => Calls.Add("awake");
            public IClientEntitySource Source(ECViewEntityData entity) { Calls.Add("source"); return ((Input)entity).SourceData; }
            public IClientEntityGrid Grid(ECViewEntityData entity) { Calls.Add("grid"); return ((Input)entity).GridData; }
            public bool HasMoveAgent(MyEntity entity) => throw new NotSupportedException();
            public void AddMoveAgent(MyEntity entity) => throw new NotSupportedException();
        }
        private sealed class Registry : IMyContainerHandler
        {
            public ClientContainer MyContainer { get; private set; } public IClientEntityContainer Container { get; private set; }
            public EntityGridGroundData GridGround => null; public MyEntity MyRootEntity => MyContainer?.MyRootEntity;
            public ECViewEntityData RootEntity => Container?.RootEntity;
            public void RegisterContainer(ClientContainer view, IClientEntityContainer source)
            { MyContainer = view; Container = source; Trace.Add("register"); }
            public void UnregisterContainer() { MyContainer = null; Container = null; Trace.Add("unregister"); }
        }
        private sealed class Receiver : IMyContainerAddEntityEventReceiver, IMyContainerRemoveEntityEventReceiver,
            IMyContainerPrepareReplaceEntityEventReceiver, IMyContainerReplaceEntityEventReceiver,
            Zjcs.Official.EntityLifecycle.IMyEntityPrepareReplaceEventReceiver, Zjcs.Official.EntityLifecycle.IMyEntityReplaceEventReceiver
        {
            public ClientContainer Container;
            public void MyContainer_OnEntityAdd(MyEntity entity) => Trace.Add("add:" + entity.ClassId);
            public void MyContainer_OnEntityRemove(MyEntity entity) => Trace.Add("removed:" + entity.ClassId);
            public void MyContainer_OnEntityPrepareReplace(MyEntity entity) => Trace.Add("container-prepare");
            public void MyContainer_OnEntityReplace(MyEntity entity, EcEntityIdentifier previous)
            { Require(Container.GetEntity(entity.Id) == entity && Container.GetEntity(previous) == null, "replace container event preceded dictionary update"); Trace.Add("container-replace"); }
            public void MyEntity_OnPrepareReplace(MyEntity entity) => Trace.Add("entity-prepare");
            public void MyEntity_OnReplace(MyEntity entity, EcEntityIdentifier previous)
            { Require(Container.GetEntity(entity.Id) == null && Container.GetEntity(previous) == null, "replace entity event followed dictionary reinsertion"); Trace.Add("entity-replace"); }
        }
        private sealed class Subscription : IDisposable
        {
            private Action release; public Subscription(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
