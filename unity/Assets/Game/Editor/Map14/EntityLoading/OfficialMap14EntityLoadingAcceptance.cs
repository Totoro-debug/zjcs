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
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityLoadingAcceptance
    {
        private const string Key = "Map14.EntityLoading";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS") != "1")
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            else Debug.Log("Explicit entity loading assembly isolation; full ProjectTools.Validate is not executed.");
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
            { Debug.LogError("Original entity loading check timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original entity loading check").AddComponent<Runner>().StartCoroutine(Verify());
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
                Debug.Log("Map14 original entity loading Play Mode passed: asynchronous FIFO and reentrant requests, shared prefab requests, root-first sorted initial load, removed-before-load recovery, embedded pool reuse and reset, 60-second TTL configuration, expiry, low-memory queued/in-use retention, no-pool creation and subscription/resource disposal. Original four-statue display regression also passed. Formal client container, all entity prefabs, pointer/UI/save and Player remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string text)
        { if (!value) throw new InvalidDataException("Original entity loading: " + text); }
        private static IEnumerator Steps()
        {
            var previousFactory = StoryPlayableInstantiation.Resolve;
            var scope = new GameObject("Entity loading local platform fixture");
            var container = scope.AddComponent<FixtureContainer>();
            var prefab = new GameObject("Entity lifecycle loader fixture"); prefab.SetActive(false);
            prefab.AddComponent<MyEntity>(); prefab.AddComponent<Probe>();
            var services = new Services { Prefab = prefab };
            StoryPlayableInstantiation.Resolve = transform => transform.IsChildOf(scope.transform) ? services : previousFactory?.Invoke(transform);
            var loader = new MyEntityLoadHandler(services, container, true);
            var a = new Input(1001, 1); var b = new Input(1002, 2); var c = new Input(1003, 3);
            var trace = new List<int>(); MyEntity first = null, second = null, third = null;
            loader.CreateMyEntity(a, true, entity =>
            {
                first = entity; trace.Add(1);
                loader.CreateMyEntity(c, true, nested => { third = nested; trace.Add(3); });
            });
            loader.CreateMyEntity(b, false, entity => { second = entity; trace.Add(2); });
            int waitingPrefabCallback = 0;
            loader.GetPrefabComponentAsync<MyEntity>(b, entity =>
            { Require(entity == prefab.GetComponent<MyEntity>(), "pending prefab component lookup changed"); waitingPrefabCallback++; });
            services.Complete(b.ClassId); Require(trace.Count == 0, "later asset completion bypassed FIFO");
            Require(waitingPrefabCallback == 1 && services.Requests[b.ClassId] == 1, "pending prefab lookup duplicated its request");
            services.AutoComplete.Add(c.ClassId); services.Complete(a.ClassId);
            Require(trace.SequenceEqual(new[] { 1, 2, 3 }), "reentrant creation changed queue order");
            Require(first.Entity == a && first.myNewEntity && second.Entity == b && !second.myNewEntity,
                "original Bind/new-entity state changed");
            Require(!first.myStarted && first.gameObject.activeSelf, "loader started the entity before the container callback");
            first.MyStart(); loader.DestroyMyEntity(first);
            Require(!first.gameObject.activeSelf && first.transform.position == new Vector3(-5000, -5000, 0), "return offset or activity changed");
            var again = new Input(a.ClassId, 4); MyEntity reused = null;
            loader.CreateMyEntity(again, false, entity => reused = entity);
            Require(reused == first && reused.Entity == again && !reused.myStarted && !reused.myNewEntity,
                "embedded pool did not reset and rebind its original object");
            Require(services.Requests[a.ClassId] == 1, "same-class prefab cache was bypassed");
            var removed = new Input(1004, 5) { Removing = true };
            bool removedCallback = false;
            loader.CreateMyEntity(removed, true, entity => { Require(entity == null, "removed entity was published"); removedCallback = true; });
            int immediate = Probe.ImmediateCalls; services.Complete(removed.ClassId);
            Require(removedCallback && Probe.ImmediateCalls == immediate + 1, "removed entity did not immediately end before callback");
            var pools = (TTLGameObjectPoolCollection<int, MyEntity>)typeof(MyEntityLoadHandler).GetField("mMyEntityPoolCollection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loader);
            Require(pools.TryGetPool(a.ClassId, out var pool) && (float)pool.GetType().GetField("mTTLSecondes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pool) == 60,
                "original loader TTL differs from sixty seconds");
            loader.DestroyMyEntity(second); loader.DestroyMyEntity(third);
            container.InUse.Add(a.ClassId);
            var pending = new Input(1005, 6); bool pendingDone = false;
            loader.CreateMyEntity(pending, true, entity => pendingDone = entity != null);
            services.LowMemory(); yield return null;
            Require(!services.Handles[a.ClassId].Disposed && !services.Handles[pending.ClassId].Disposed,
                "low memory released an in-use or queued class");
            Require(services.Handles[b.ClassId].Disposed && services.Handles[c.ClassId].Disposed && services.Handles[removed.ClassId].Disposed,
                "low memory retained unused class resources");
            Require(reused && reused.gameObject.activeSelf && second == null && third == null,
                "low memory destroyed an active loan or retained idle objects");
            services.Complete(pending.ClassId); Require(pendingDone, "queued asset could not complete after low memory");
            var interrupted = new Input(1006, 7); bool interruptedCallback = false;
            loader.CreateMyEntity(interrupted, true, entity => interruptedCallback = true);
            loader.Dispose(); Require(services.PoolCallbacks.Count == 0 && services.LowMemoryCallbacks.Count == 0,
                "loader disposal leaked timer or low-memory subscription");
            Require(services.Handles.Values.All(handle => handle.Disposed), "loader did not dispose its resource handles");
            services.Complete(interrupted.ClassId); Require(!interruptedCallback, "disposed queue published a late completion");
            pools.Clear();

            services = new Services { Prefab = prefab, CompleteEveryRequest = true };
            StoryPlayableInstantiation.Resolve = transform => transform.IsChildOf(scope.transform) ? services : previousFactory?.Invoke(transform);
            var root = new Input(1100, 99); container.Root = root; container.Entities.Clear();
            foreach (var input in new[] { new Input(1103, 30), root, new Input(1101, 10), new Input(1102, 20) }) container.Entities.Add(input.Id, input);
            trace.Clear(); loader = new MyEntityLoadHandler(services, container);
            yield return loader.LoadForInit(entity => trace.Add(entity.Id.EntityId));
            Require(trace.SequenceEqual(new[] { 99, 10, 20, 30 }), "initial load lost root-first or entity-id ordering");
            var loads = services.Handles.Count; int prefabCallback = 0;
            loader.GetPrefabComponentAsync<MyEntity>(root, value => { Require(value == prefab.GetComponent<MyEntity>(), "prefab component lookup changed"); prefabCallback++; });
            Require(prefabCallback == 1 && services.Handles.Count == loads, "loaded prefab component lookup issued another request");
            loader.Dispose();

            // Keep the original Unity clocks; a short-lived generic pool tests expiry.
            var embed = new GameObjectPoolEmbed<MyEntity>(scope.transform, prefab);
            var expiring = new TTLGameObjectPool<MyEntity>(embed, .05f);
            var idle = expiring.Borrow(scope.transform, false); expiring.Return(idle);
            yield return new WaitForSecondsRealtime(1.15f); expiring.CheckExpire(); yield return null;
            Require(idle == null, "expired idle object was not destroyed");
            expiring.Destory();
            Require(services.Intervals.All(interval => interval == TimeSpan.FromSeconds(2)), "expiry subscription interval differs");

            services = new Services { Prefab = prefab, CompleteEveryRequest = true, Pool = false };
            loader = new MyEntityLoadHandler(services, container);
            MyEntity noPoolA = null, noPoolB = null;
            loader.CreateMyEntity(new Input(1200, 1), true, entity => noPoolA = entity);
            loader.CreateMyEntity(new Input(1200, 2), true, entity => noPoolB = entity);
            Require(noPoolA && noPoolB && noPoolA != noPoolB && services.InstantiateCount == 2,
                "disabled pooling reused an object");
            loader.DestroyMyEntity(noPoolA); loader.DestroyMyEntity(noPoolB); yield return null;
            Require(!noPoolA && !noPoolB, "disabled pooling did not destroy returned entities");
            loader.Dispose();
            StoryPlayableInstantiation.Resolve = previousFactory;
            UnityEngine.Object.Destroy(scope); UnityEngine.Object.Destroy(prefab); yield return null;
            Probe.ImmediateCalls = 0;
            yield return (IEnumerator)typeof(OfficialMap14HeroStatueAcceptance).GetMethod("Steps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }
        private sealed class Input : ECViewEntityData
        {
            private readonly int classId, id;
            public bool Removing;
            public Input(int classId, int id) { this.classId = classId; this.id = id; }
            public override int ClassId => classId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(14, id);
            public override T GetComponent<T>() => default;
        }
        private sealed class FixtureContainer : MyContainer, IEntityLoadContainer, IEntityLoadSharedContainer, IECViewMapRootLifetime
        {
            public readonly Dictionary<EcEntityIdentifier, ECViewEntityData> Entities = new Dictionary<EcEntityIdentifier, ECViewEntityData>();
            public readonly HashSet<int> InUse = new HashSet<int>();
            public Input Root;
            public override IECViewMapRootLifetime Container => this;
            public override MyEntity MyRootEntity => null;
            public override ILogger Logger => Debug.unityLogger;
            public MonoBehaviour Host => this;
            public MyContainer LifecycleContainer => this;
            IEntityLoadSharedContainer IEntityLoadContainer.Container => this;
            public Transform EntitySlot => transform;
            public bool ExistEntityGroupByClassId(int classId) => InUse.Contains(classId);
            public ECViewEntityData RootEntity => Root;
            public IReadOnlyDictionary<EcEntityIdentifier, ECViewEntityData> EntityDict => Entities;
            public bool Destroying => false;
        }
        private sealed class Probe : MyComponent
        {
            public static int ImmediateCalls;
            public override void OnImmediateToEnd() => ImmediateCalls++;
        }
        private sealed class Handle : AssetOperationHandle<GameObject>
        {
            private Action<AssetOperationHandle<GameObject>> completed;
            private bool done;
            public bool Disposed;
            public GameObject Prefab;
            public override int Status => done ? 2 : 0;
            public override GameObject Result => done ? Prefab : null;
            public override event Action<AssetOperationHandle<GameObject>> Completed
            { add { if (done) value?.Invoke(this); else completed += value; } remove { completed -= value; } }
            public void Complete() { if (done) return; done = true; var callback = completed; completed = null; callback?.Invoke(this); }
            public override void Dispose() { Disposed = true; }
        }
        private sealed class Services : IServiceProvider, IEntityLoadFacts, IEntityPoolScheduler, IApplicationService,
            IResourceLoader, IMyEntityIndexHandler, IEntityLoadInstantiation, IStoryPlayableInstanceFactory
        {
            public GameObject Prefab;
            public bool CompleteEveryRequest, Pool = true;
            public int InstantiateCount;
            public readonly Dictionary<int, Handle> Handles = new Dictionary<int, Handle>();
            public readonly Dictionary<int, int> Requests = new Dictionary<int, int>();
            public readonly HashSet<int> AutoComplete = new HashSet<int>();
            public readonly List<Action> PoolCallbacks = new List<Action>(), LowMemoryCallbacks = new List<Action>();
            public readonly List<TimeSpan> Intervals = new List<TimeSpan>();
            public object GetService(Type type) => type.IsInstanceOfType(this) ? this : throw new InvalidOperationException(type.FullName);
            public bool IsMapForbidLowMemoryDeal => false;
            public bool UseGameObjectPool => Pool;
            public ECViewEntityData GetCommonPrefab(int classId) => new Input(classId, 0);
            public bool IsRemoving(ECViewEntityData entity) => ((Input)entity).Removing;
            public bool HasGridGround(ECViewEntityData entity) => false;
            public IDisposable Subscribe(TimeSpan interval, Action callback)
            { Intervals.Add(interval); PoolCallbacks.Add(callback); return new Lease(() => PoolCallbacks.Remove(callback)); }
            public IDisposable SubscribeLowMemory(Action callback)
            { LowMemoryCallbacks.Add(callback); return new Lease(() => LowMemoryCallbacks.Remove(callback)); }
            public void LowMemory() { foreach (var callback in LowMemoryCallbacks.ToArray()) callback(); }
            public void Complete(int classId) => Handles[classId].Complete();
            public string GetDir(int classId) => "fixture";
            public int[] GetIdsByDir(string directory) => throw new NotSupportedException();
            public string GetEntityPath(int classId) => classId.ToString();
            public AssetOperationHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
            {
                Require(typeof(T) == typeof(GameObject), "unexpected prefab resource type");
                int classId = int.Parse(path); Requests.TryGetValue(classId, out int count); Requests[classId] = count + 1;
                var handle = new Handle { Prefab = Prefab }; Handles[classId] = handle;
                if (CompleteEveryRequest || AutoComplete.Contains(classId)) handle.Complete();
                return (AssetOperationHandle<T>)(object)handle;
            }
            public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
            { InstantiateCount++; return UnityEngine.Object.Instantiate(prefab, position, rotation, parent); }
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            { InstantiateCount++; return UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays); }
        }
        private sealed class Lease : IDisposable
        {
            private Action release;
            public Lease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
