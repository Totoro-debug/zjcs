using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeUI;

namespace Zjcs.Official.HomeEntityRuntime
{
    public sealed class HomeEntityItemInfo { public HomeEntitySkinItemType ItemType; }
    public sealed class HomeEntityEnvironmentSkinInfo { public HomeEnvironmentType EnvironmentType; }

    // Local ownership boundaries for the original manager's global client services.
    public interface IHomeEntityRuntimeServices
    {
        IHomeEntityResourceLoader ResourceLoader { get; }
        IHomeEntitySkinData PlayerData { get; }
        IHomeEntityPrefabTables Tables { get; }
        HomeEntityPrefabPaths PrefabPaths { get; }
        IHomeFurnitureChangePublisher FurnitureChangePublisher { get; }
        IHomeEntityManager EntityManager { get; }
        IHomeRuntimeBindingCacheService RuntimeBindingCacheService { get; }
        bool InMyHome { get; }
        GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays);
        IDisposable SubscribeSkinPreview(Action<HomeSkinPreviewData> action);
        IDisposable SubscribeHomeChange(Action action);
        IDisposable SubscribeFurnitureSkinChanged(Action<EFurniture, int> action);
        IDisposable SubscribeThemeSkinChanged(Action<HomeThemeType, int> action);
        IDisposable SubscribeEnvironmentSkinChanged(Action<HomeEnvironmentType, int> action);
        HomeEntityItemInfo GetItemInfo(int itemId);
        FurnitureSkinInfo GetFurnitureSkin(int itemId);
        HomeThemeSkinInfo GetThemeSkin(int itemId);
        HomeEntityEnvironmentSkinInfo GetEnvironmentSkin(int itemId);
        void SetShowTipDot(bool value);
        void SetHomeSceneUIActive(bool value);
    }
    public sealed class HomeEntityRuntimeLifetime : IDisposable
    {
        readonly List<IDisposable> values = new List<IDisposable>();
        public void Add(IDisposable value) => values.Add(value);
        public void Dispose()
        {
            foreach (var value in values) value?.Dispose();
            values.Clear();
        }
    }
}
