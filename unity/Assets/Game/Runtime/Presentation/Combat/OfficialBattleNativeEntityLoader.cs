using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Local resources and shared identities for the original entity loader.</summary>
    internal enum OfficialBattleNativeEntityKind { Skill, Hit, MoveAction, Damage }

    internal sealed class OfficialBattleNativeEntityLoader : IDisposable
    {
        [Serializable] private sealed class Catalog { public string manifest; public Binding[] hits; public Binding[] skills; public Binding[] moves; public Binding[] damages; }
        [Serializable] private sealed class Binding { public int hit_entity_id; public int skill_entity_id; public int action_entity_id; public int damage_entity_id; public string entity_resource; }
        [Serializable] private sealed class Settings { public string manifest; public bool ECIsUseGameObjectPool; }
        private sealed class Lease : IDisposable
        {
            private Action release;
            public Lease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        private sealed class Handle<T> : AssetOperationHandle<T> where T : UnityEngine.Object
        {
            private readonly T result;
            public Handle(T result) { this.result = result; }
            public override int Status => result ? 2 : 3;
            public override T Result => result;
            public override event Action<AssetOperationHandle<T>> Completed
            { add { value?.Invoke(this); } remove { } }
            public override void Dispose() { }
        }
        private sealed class LoaderBindings : IServiceProvider, IEntityLoadContainer,
            IEntityLoadSharedContainer, IEntityLoadFacts, IEntityLoadInstantiation,
            IEntityPoolScheduler, IApplicationService, IMyEntityIndexHandler, IResourceLoader, IStoryPlayableInstanceFactory
        {
            public OfficialBattleNativeEntityLoader Owner;
            public MonoBehaviour Host => Owner.container;
            public MyContainer LifecycleContainer => Owner.container;
            public IEntityLoadSharedContainer Container => this;
            public Transform EntitySlot => Owner.entityRoot.transform;
            public ECViewEntityData RootEntity => throw new NotSupportedException("Incremental entity notifications do not load a shared root.");
            public IReadOnlyDictionary<EcEntityIdentifier, ECViewEntityData> EntityDict => Owner.data;
            public bool IsMapForbidLowMemoryDeal => false;
            public bool UseGameObjectPool => Owner.usePool;
            public bool ExistEntityGroupByClassId(int classId)
            {
                foreach (var item in Owner.data.Values) if (item.ClassId == classId) return true;
                return false;
            }
            public bool IsRemoving(ECViewEntityData entity) => !Owner.data.ContainsKey(entity.Id);
            public bool HasGridGround(ECViewEntityData entity) => entity.GetComponent<ECGridTransformData>() != null;
            public ECViewEntityData GetCommonPrefab(int classId) =>
                throw new NotSupportedException("Entity creation requires the actual shared entity identity.");
            public string GetEntityPath(int classId) => Owner.resources[classId];
            public string GetDir(int classId) => "Fight/FightSkill";
            public int[] GetIdsByDir(string directory) => directory == "Fight/FightSkill"
                ? new List<int>(Owner.resources.Keys).ToArray() : Array.Empty<int>();
            public object GetService(Type type) => type.IsInstanceOfType(this) ? this : null;
            public AssetOperationHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object =>
                new Handle<T>(Resources.Load<T>(path));
            public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
            {
                var instance = Instantiate(prefab, parent, false);
                instance.transform.SetPositionAndRotation(position, rotation);
                return instance;
            }
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            {
                // Original FIFO pools call the scene factory before Bind. Hold the
                // instance inactive until its injection and original Bind complete.
                var instance = Owner.scope.Instantiate(prefab, Owner.staging.transform, worldPositionStays);
                instance.SetActive(false);
                var entity = instance.GetComponent<MyEntity>();
                if (!entity) throw new InvalidOperationException("Original entity prefab has no MyEntity.");
                Owner.prepareInstance?.Invoke(instance);
                entity.BindEvent += new BindReceiver(entity, Owner.prepareBound);
                instance.transform.SetParent(parent, worldPositionStays);
                return instance;
            }
            public IDisposable Subscribe(TimeSpan interval, Action callback)
            {
                var routine = Host.StartCoroutine(Repeat((float)interval.TotalSeconds, callback));
                return new Lease(() => { if (Host) Host.StopCoroutine(routine); });
            }
            private static IEnumerator Repeat(float seconds, Action callback)
            {
                var delay = new WaitForSecondsRealtime(seconds);
                while (true) { yield return delay; callback(); }
            }
            public IDisposable SubscribeLowMemory(Action callback)
            {
                Application.LowMemoryCallback action = () => callback();
                Application.lowMemory += action;
                return new Lease(() => Application.lowMemory -= action);
            }
        }

        private readonly MyContainer container;
        private readonly Action<GameObject> prepareInstance;
        private readonly Action<MyEntity> prepareBound;
        private readonly GameObject staging;
        private readonly LoaderBindings bindings;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly GameObject entityRoot;
        private readonly bool usePool;
        private readonly MyFightEntityLoadHandler loader;
        private readonly Dictionary<int, string> resources = new Dictionary<int, string>();
        private readonly Dictionary<EcEntityIdentifier, ECViewEntityData> data = new Dictionary<EcEntityIdentifier, ECViewEntityData>();
        private readonly Dictionary<EcEntityIdentifier, MyEntity> live = new Dictionary<EcEntityIdentifier, MyEntity>();
        private bool disposed;

        private sealed class BindReceiver : IMyEntityBindEventReceiver
        {
            private readonly MyEntity entity;
            private readonly Action<MyEntity> action;
            public BindReceiver(MyEntity entity, Action<MyEntity> action) { this.entity = entity; this.action = action; }
            public void MyEntity_OnBind() => action?.Invoke(entity);
        }

        public OfficialBattleNativeEntityLoader(Transform parent, MyContainer container,
            OfflineBattleNativePlayableScope scope, bool skills,
            Action<GameObject> prepareInstance, Action<MyEntity> prepareBound = null)
            : this(parent, container, scope, skills ? OfficialBattleNativeEntityKind.Skill
                : OfficialBattleNativeEntityKind.Hit, prepareInstance, prepareBound) { }

        public OfficialBattleNativeEntityLoader(Transform parent, MyContainer container,
            OfflineBattleNativePlayableScope scope, OfficialBattleNativeEntityKind kind,
            Action<GameObject> prepareInstance, Action<MyEntity> prepareBound = null)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            this.container = container ? container : throw new ArgumentNullException(nameof(container));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.prepareInstance = prepareInstance;
            this.prepareBound = prepareBound;
            var asset = Resources.Load<TextAsset>("Combat/skill-native-graphs");
            var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
            var settingsAsset = Resources.Load<TextAsset>("Combat/hit-native-settings");
            var settings = settingsAsset ? JsonUtility.FromJson<Settings>(settingsAsset.text) : null;
            if (catalog == null || catalog.manifest != "884_156692" || catalog.hits == null
                || settings == null || settings.manifest != "884_156692")
                throw new InvalidOperationException("Original native entity catalog/settings are unavailable.");
            var rows = kind == OfficialBattleNativeEntityKind.Skill ? catalog.skills
                : kind == OfficialBattleNativeEntityKind.Hit ? catalog.hits
                : kind == OfficialBattleNativeEntityKind.Damage ? catalog.damages : catalog.moves;
            if (rows == null) throw new InvalidOperationException("Original native entity catalog is unavailable.");
            foreach (var binding in rows)
            {
                int classId = binding == null ? 0 : kind == OfficialBattleNativeEntityKind.Skill
                    ? binding.skill_entity_id : kind == OfficialBattleNativeEntityKind.Hit
                    ? binding.hit_entity_id : kind == OfficialBattleNativeEntityKind.Damage
                    ? binding.damage_entity_id : binding.action_entity_id;
                if (classId <= 0
                    || string.IsNullOrEmpty(binding.entity_resource) || resources.ContainsKey(classId))
                    throw new InvalidOperationException("Original native entity catalog contains an invalid binding.");
                resources.Add(classId, binding.entity_resource);
            }
            usePool = settings.ECIsUseGameObjectPool;
            entityRoot = new GameObject(kind == OfficialBattleNativeEntityKind.Skill ? "Battle native skill entities"
                : kind == OfficialBattleNativeEntityKind.Hit ? "Battle native hit entities"
                : kind == OfficialBattleNativeEntityKind.Damage ? "Battle native damage entities" : "Battle native move actions");
            entityRoot.transform.SetParent(parent, false);
            staging = new GameObject("Inactive original entity injection");
            staging.SetActive(false); staging.transform.SetParent(entityRoot.transform, false);
            bindings = new LoaderBindings { Owner = this };
            loader = new MyFightEntityLoadHandler(bindings, bindings);
        }
        public int Count => live.Count;
        public Transform EntitySlot => entityRoot.transform;

        public bool ContainsClass(int classId) => resources.ContainsKey(classId);
        internal GameObject GetPrefab(int classId) => resources.TryGetValue(classId, out var path)
            ? Resources.Load<GameObject>(path) : null;
        public MyEntity GetEntity(EcEntityIdentifier id) => live.TryGetValue(id, out var entity) ? entity : null;

        public MyEntity Create(ECViewEntityData entityData)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleNativeEntityLoader));
            if (entityData == null) throw new ArgumentNullException(nameof(entityData));
            if (data.ContainsKey(entityData.Id)) throw new InvalidOperationException("Native entity identity is already live.");
            if (!resources.ContainsKey(entityData.ClassId))
                throw new InvalidOperationException("Original native entity class is unavailable: " + entityData.ClassId);
            data.Add(entityData.Id, entityData);
            MyEntity result = null;
            scope.Dispatch(() =>
            {
                var previousFactory = StoryPlayableInstantiation.Resolve;
                try
                {
                    StoryPlayableInstantiation.Resolve = transform => transform == container.transform
                        ? bindings : previousFactory?.Invoke(transform);
                    loader.CreateMyEntity(entityData, true, entity =>
                    {
                        if (!entity) return;
                        live.Add(entityData.Id, entity);
                        result = entity;
                    });
                }
                finally { StoryPlayableInstantiation.Resolve = previousFactory; }
            });
            if (!result)
            {
                if (live.ContainsKey(entityData.Id)) Remove(entityData.Id);
                else data.Remove(entityData.Id);
                throw new InvalidOperationException("Original native entity loader did not create " + entityData.ClassId);
            }
            return result;
        }
        public void Remove(EcEntityIdentifier id)
        {
            if (!live.TryGetValue(id, out var entity)) return;
            scope.Dispatch(() =>
            {
                entity.End(null);
                live.Remove(id);
                data.Remove(id);
                entity.Remove(null);
                loader.DestroyMyEntity(entity);
            });
        }
        public void Dispose()
        {
            if (disposed) return;
            foreach (var id in new List<EcEntityIdentifier>(live.Keys)) Remove(id);
            // Original battle specialization clears its pools before disposal.
            loader.OnLowMemory();
            loader.Dispose();
            disposed = true;
            if (Application.isPlaying) UnityEngine.Object.Destroy(entityRoot);
            else UnityEngine.Object.DestroyImmediate(entityRoot);
        }
    }
}
