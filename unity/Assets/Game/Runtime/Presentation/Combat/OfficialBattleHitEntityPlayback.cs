using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Local resources and shared identities for the original entity loader.</summary>
    public sealed class OfficialBattleHitEntityPlayback : IDisposable
    {
        [Serializable] private sealed class Catalog { public string manifest; public Binding[] hits; }
        [Serializable] private sealed class Binding { public int hit_entity_id; public string entity_resource; }
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
            IEntityPoolScheduler, IApplicationService, IMyEntityIndexHandler, IResourceLoader
        {
            public OfficialBattleHitEntityPlayback Owner;
            public MonoBehaviour Host => Owner.container;
            public MyContainer LifecycleContainer => Owner.container;
            public IEntityLoadSharedContainer Container => this;
            public Transform EntitySlot => Owner.hitRoot.transform;
            public ECViewEntityData RootEntity => throw new NotSupportedException("Hit notifications do not load a shared root.");
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
                throw new NotSupportedException("Hit creation requires the actual shared hit identity.");
            public string GetEntityPath(int classId) => Owner.resources[classId];
            public string GetDir(int classId) => "Fight/FightSkill";
            public int[] GetIdsByDir(string directory) => directory == "Fight/FightSkill"
                ? new List<int>(Owner.resources.Keys).ToArray() : Array.Empty<int>();
            public object GetService(Type type) => type.IsInstanceOfType(this) ? this : null;
            public AssetOperationHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object =>
                new Handle<T>(Resources.Load<T>(path));
            public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
            {
                var instance = Owner.scope.Instantiate(prefab, parent, false);
                instance.transform.SetPositionAndRotation(position, rotation);
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
        private readonly IFightHitViewServices services;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly GameObject hitRoot;
        private readonly bool usePool;
        private readonly MyFightEntityLoadHandler loader;
        private readonly Dictionary<int, string> resources = new Dictionary<int, string>();
        private readonly Dictionary<EcEntityIdentifier, ECViewEntityData> data = new Dictionary<EcEntityIdentifier, ECViewEntityData>();
        private readonly Dictionary<EcEntityIdentifier, MyEntity> live = new Dictionary<EcEntityIdentifier, MyEntity>();
        private bool disposed;

        public OfficialBattleHitEntityPlayback(Transform parent, MyContainer container,
            IFightHitViewServices services, OfflineBattleNativePlayableScope scope)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            this.container = container ? container : throw new ArgumentNullException(nameof(container));
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            var asset = Resources.Load<TextAsset>("Combat/skill-native-graphs");
            var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
            var settingsAsset = Resources.Load<TextAsset>("Combat/hit-native-settings");
            var settings = settingsAsset ? JsonUtility.FromJson<Settings>(settingsAsset.text) : null;
            if (catalog == null || catalog.manifest != "884_156692" || catalog.hits == null
                || settings == null || settings.manifest != "884_156692")
                throw new InvalidOperationException("Original native hit catalog/settings are unavailable.");
            foreach (var binding in catalog.hits)
            {
                if (binding == null || binding.hit_entity_id <= 0
                    || string.IsNullOrEmpty(binding.entity_resource) || resources.ContainsKey(binding.hit_entity_id))
                    throw new InvalidOperationException("Original native hit catalog contains an invalid binding.");
                resources.Add(binding.hit_entity_id, binding.entity_resource);
            }
            usePool = settings.ECIsUseGameObjectPool;
            hitRoot = new GameObject("Battle native hit entities");
            hitRoot.transform.SetParent(parent, false);
            var bindings = new LoaderBindings { Owner = this };
            loader = new MyFightEntityLoadHandler(bindings, bindings);
        }
        public int Count => live.Count;
        public bool ContainsClass(int classId) => resources.ContainsKey(classId);
        public MyEntity GetEntity(EcEntityIdentifier id) => live.TryGetValue(id, out var entity) ? entity : null;

        public MyEntity Create(ECViewEntityData entityData)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleHitEntityPlayback));
            if (entityData == null) throw new ArgumentNullException(nameof(entityData));
            if (data.ContainsKey(entityData.Id)) throw new InvalidOperationException("Native hit identity is already live.");
            if (!resources.ContainsKey(entityData.ClassId))
                throw new InvalidOperationException("Original native hit class is unavailable: " + entityData.ClassId);
            data.Add(entityData.Id, entityData);
            MyEntity result = null;
            scope.Dispatch(() => loader.CreateMyEntity(entityData, true, entity =>
            {
                if (!entity) return;
                live.Add(entityData.Id, entity);
                int count = 0;
                foreach (var component in entity.GetComponents<MonoBehaviour>())
                    if (component is IFightHitViewBinding view)
                    {
                        view.ViewServices = services;
                        view.mMyPlayableManager = scope;
                        count++;
                    }
                if (count != 1) throw new InvalidOperationException("Native hit prefab requires one original hit view.");
                entity.MyStart();
                result = entity;
            }));
            if (!result)
            {
                if (live.ContainsKey(entityData.Id)) Remove(entityData.Id);
                else data.Remove(entityData.Id);
                throw new InvalidOperationException("Original native hit loader did not create " + entityData.ClassId);
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
            if (Application.isPlaying) UnityEngine.Object.Destroy(hitRoot);
            else UnityEngine.Object.DestroyImmediate(hitRoot);
        }
    }
}
