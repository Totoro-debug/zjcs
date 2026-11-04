using System;
using System.Collections.Generic;
using UnityEngine;
using IProfessionSoundHost = Zjcs.Official.ProfessionUI.IProfessionSoundHost;
using OfficialWwiseEvent = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Offline.BattleNativePlayables
{
    // Explicit battle DI boundary. The original manager, center, sequence and
    // FIFO pools own execution; the supplied backend owns original event DSP.
    public sealed class OfflineBattleNativePlayableScope : IStoryPlayableInstanceFactory, IMyPlayableManager,
        IBattleAfterimageHost, IDisposable
    {
        private readonly GameObject root, staging;
        private readonly IProfessionSoundHost sound;
        private IBattleCameraOffsetHost cameraOffsetHost;
        private IBattleCameraPerformanceHost cameraPerformanceHost;
        private IBattleMasterEntityResolver masterEntityResolver;
        private IBattleSourceDeathView sourceDeathView;
        private IBattleCharacterPartVisibilityHost characterPartVisibilityHost;
        private readonly BattleAfterimageTexturePool afterimageTextures = new BattleAfterimageTexturePool();
        private readonly MyPlayableCenter center = new MyPlayableCenter();
        private readonly MyPlayableManager manager;
        private readonly HashSet<MyPlayable> owned = new HashSet<MyPlayable>();
        private readonly Zjcs.Official.StoryPlayables.GAction lowMemory = new Zjcs.Official.StoryPlayables.GAction();
        private bool disposed;

        public OfflineBattleNativePlayableScope(Transform parent, IProfessionSoundHost sound,
            IBattleCameraOffsetHost cameraOffsetHost = null,
            IBattleCharacterPartVisibilityHost characterPartVisibilityHost = null,
            IBattleCameraPerformanceHost cameraPerformanceHost = null,
            IBattleMasterEntityResolver masterEntityResolver = null)
        {
            this.sound = sound ?? NullProfessionSoundHost.Instance;
            this.cameraOffsetHost = cameraOffsetHost;
            this.cameraPerformanceHost = cameraPerformanceHost;
            this.masterEntityResolver = masterEntityResolver;
            this.characterPartVisibilityHost = characterPartVisibilityHost;
            root = new GameObject("Original battle native playables");
            root.transform.SetParent(parent, false);
            staging = new GameObject("Original battle inactive injection");
            staging.SetActive(false); staging.transform.SetParent(root.transform, false);
            manager = new MyPlayableManager { ScopeRoot = root, InstanceFactory = this,
                MyPlayableCenter = center, LowMemory = lowMemory };
            manager.Initialize();
            manager.SetObjectPool(new GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>>(
                manager.PlayableSlot, (slot, prefab) => new GameObjectPool<MyPlayable>(slot,
                    (poolRoot, unused, autoActive) =>
                    {
                        var instance = Instantiate(prefab, poolRoot, false);
                        if (autoActive) instance.SetActive(true);
                        return instance;
                    }, prefab.name)));
            Application.lowMemory += OnLowMemory;
        }

        /// <summary>
        /// Binds scene-owned hosts after the battle camera and character roots exist.
        /// Skill graphs are instantiated after Begin, so late binding preserves the
        /// original graph components' source/target initialization order.
        /// </summary>
        public void BindHosts(IBattleCameraOffsetHost cameraOffsetHost = null,
            IBattleCharacterPartVisibilityHost characterPartVisibilityHost = null,
            IBattleCameraPerformanceHost cameraPerformanceHost = null,
            IBattleMasterEntityResolver masterEntityResolver = null)
        {
            RequireLive();
            this.cameraOffsetHost = cameraOffsetHost;
            this.characterPartVisibilityHost = characterPartVisibilityHost;
            this.cameraPerformanceHost = cameraPerformanceHost;
            this.masterEntityResolver = masterEntityResolver;
        }

        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            RequireLive();
            // Inject before Awake/OnEnable: preserve original leaf subscriptions.
            var instance = UnityEngine.Object.Instantiate(prefab, staging.transform, worldPositionStays);
            foreach (var playable in instance.GetComponentsInChildren<MyPlayable>(true)) playable.MyPlayableCenter = center;
            foreach (MonoBehaviour component in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is IBattleCameraPlayable cameraPlayable)
                    cameraPlayable.BindCameraOffsetHost(cameraOffsetHost);
                if (component is IBattleCharacterPartVisibilityPlayable visibilityPlayable)
                    visibilityPlayable.BindCharacterPartVisibilityHost(characterPartVisibilityHost);
                if (component is IBattleAfterimagePlayable afterimagePlayable)
                    afterimagePlayable.BindAfterimageHost(this);
                if (component is IBattleCameraPerformancePlayable performancePlayable)
                    performancePlayable.BindCameraPerformanceHost(cameraPerformanceHost);
                if (component is IBattleMasterEntityOverridePlayable masterOverridePlayable)
                    masterOverridePlayable.BindMasterEntityResolver(masterEntityResolver);
                if (component is IBattleSourceDeathPlayable sourceDeathPlayable)
                    sourceDeathPlayable.BindSourceDeathView(sourceDeathView);
            }
            instance.transform.SetParent(parent, worldPositionStays);
            return instance;
        }

        public void BindSourceDeathView(IBattleSourceDeathView view)
        {
            RequireLive();
            sourceDeathView = view;
        }

        public MyPlayable Play(GameObject prefab, GameObject source, GameObject target, float speed = 1,
            GameObject parent = null, Dictionary<string, object> parameters = null, IMyPlayableNeedStop stopOwner = null)
        {
            RequireLive();
            var playable = WithSound(() => manager.Play(prefab, source, target, speed, parent, parameters, stopOwner));
            if (playable) owned.Add(playable);
            return playable;
        }

        public Transform PlayableSlot { get { RequireLive(); return manager.PlayableSlot; } }

        // Shared entity notifications can stop original leaves synchronously.
        // Keep their original callbacks inside the same explicit sound boundary.
        public void Dispatch(Action callback)
        {
            RequireLive();
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            WithSound(() => { callback(); return 0; });
        }

        public void RemoveAllPlayable(bool isReleasePool)
        {
            Dispatch(() => manager.RemoveAllPlayable(isReleasePool));
        }

        public void SetObjectPool(GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> pools)
            => Dispatch(() => manager.SetObjectPool(pools));

        public void ForceReleaseObjectPool() => Dispatch(() => manager.ForceReleaseObjectPool());

        public RenderTexture GetRT(int width, int height)
        {
            RequireLive();
            return afterimageTextures.GetRT(width, height).rt;
        }

        public void ReturnRT(RenderTexture texture)
        {
            RequireLive();
            afterimageTextures.ReturnRT(texture);
        }

        public void Tick(float deltaTime)
        {
            RequireLive();
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            WithSound(() => { center.Update(deltaTime); return 0; });
        }

        public void LateTick()
        {
            RequireLive();
            WithSound(() => { center.LateUpdate(); return 0; });
        }

        public void Stop(MyPlayable playable, bool immediate = false)
        {
            RequireLive();
            if (playable && !owned.Contains(playable)) throw new ArgumentException("Playable belongs to another scope.", nameof(playable));
            WithSound(() => { if (playable) playable.Stop(immediate); return 0; });
        }

        private T WithSound<T>(Func<T> action)
        {
            var previous = OfficialWwiseEvent.Host;
            try { OfficialWwiseEvent.Host = sound; return action(); }
            finally { OfficialWwiseEvent.Host = previous; }
        }

        private void RequireLive()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineBattleNativePlayableScope));
        }

        private sealed class NullProfessionSoundHost : IProfessionSoundHost
        {
            internal static readonly NullProfessionSoundHost Instance = new NullProfessionSoundHost();
            public uint PostEvent(uint eventId, GameObject emitter) => 0;
        }

        private void OnLowMemory() => WithSound(() => { lowMemory.Invoke(); return 0; });

        public void Dispose()
        {
            if (disposed) return;
            // Stop callbacks must retain the backend through final pool release.
            try
            {
                WithSound(() =>
                {
                    manager.RemoveAllPlayable(true); center.RemoveAll(); manager.ForceReleaseObjectPool();
                    manager.Dispose(); return 0;
                });
            }
            finally
            {
                disposed = true; owned.Clear(); Application.lowMemory -= OnLowMemory;
                afterimageTextures.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
