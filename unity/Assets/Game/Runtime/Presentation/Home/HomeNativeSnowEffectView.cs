using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeWeather;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    // Weather-only projections of original ground prefabs. The original creator
    // retains path/weather precedence, cache decisions and creation notifications.
    internal sealed class HomeNativeSnowEffectView : IHomeEntityManagerContext, IHomeEntityResourceLoader, IDisposable
    {
        [Serializable] sealed class Catalog { public Entry[] entries; }
        [Serializable] sealed class Entry { public string path, resource; }
        sealed class Handle : IHomeEntityPrefabHandle
        {
            public GameObject Result { get; private set; }
            internal Handle(GameObject prefab) { Result = prefab; }
            public void Release() { Result = null; }
        }
        readonly IHomeEntityManagerContext owner;
        readonly Dictionary<string, string> resources = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly ThemeEntityCreator creator;
        readonly HomeNativePrefabFactory factory;
        readonly GameObject anchor;
        bool disposed;
        internal GameObject Instance { get; private set; }

        internal HomeNativeSnowEffectView(Transform parent, IHomeEntityManagerContext owner,
            IHomeEnvironmentManager environment, IHomeFurnitureViewModes modes)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (modes == null) throw new ArgumentNullException(nameof(modes));
            var asset = Resources.Load<TextAsset>("Home/Weather/catalog");
            var catalog = asset ? JsonUtility.FromJson<Catalog>(asset.text) : null;
            if (catalog?.entries == null || catalog.entries.Length == 0)
                throw new InvalidOperationException("The original ground weather-effect catalog is missing.");
            foreach (var entry in catalog.entries)
                resources.Add(entry.path, entry.resource);
            anchor = new GameObject("NativeGroundWeather");
            anchor.transform.SetParent(parent, false);
            try
            {
                Transform slot = HomeNativeSceneSlots.Create(anchor.transform, "Home/Weather/ground-slot").Find("GroundSlot");
                creator = new ThemeEntityCreator(new Dictionary<HomeThemeType, Transform> { { HomeThemeType.Ground, slot } });
                factory = new HomeNativePrefabFactory(root =>
                {
                    foreach (var snow in root.GetComponentsInChildren<HomeWeatherSnow>(true))
                    {
                        snow.HomeEnvironmentManager = environment;
                        snow.HomeData = modes;
                    }
                });
                if (!creator.GetOrCreate(HomeThemeType.Ground, this))
                    throw new InvalidOperationException("The original ground weather effect failed to open.");
            }
            catch { Dispose(); throw; }
        }

        internal void Refresh()
        {
            if (disposed) return;
            creator.OnSkinChanged(HomeThemeType.Ground, PlayerData.GetThemeSkinId(HomeThemeType.Ground), this, out _);
        }
        public IHomeEntityPrefabHandle LoadPrefab(string path)
        {
            if (!resources.TryGetValue(path, out var resource))
                throw new InvalidOperationException("Unbound original ground weather effect: " + path);
            var prefab = Resources.Load<GameObject>(resource);
            if (!prefab) throw new InvalidOperationException("Missing original ground weather effect: " + resource);
            return new Handle(prefab);
        }
        public IHomeEntityResourceLoader ResourceLoader => this;
        public IHomeFurnitureChangePublisher FurnitureChangePublisher => owner.FurnitureChangePublisher;
        public IHomeEntitySkinData PlayerData => owner.PlayerData;
        public IHomeEntityPrefabTables Tables => owner.Tables;
        public HomeEntityPrefabPaths PrefabPaths => owner.PrefabPaths;
        public int PreviewWeatherId => owner.PreviewWeatherId;
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            => factory.Instantiate(prefab, parent, worldPositionStays);
        public void OnEntityWillDestroy(GameObject instance) { if (Instance == instance) Instance = null; }
        public void OnEntityCreated(GameObject instance) { Instance = instance; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            creator?.ReleaseAll(this);
            factory?.Dispose();
            if (anchor) { anchor.SetActive(false); Object.Destroy(anchor); }
        }
    }
}
