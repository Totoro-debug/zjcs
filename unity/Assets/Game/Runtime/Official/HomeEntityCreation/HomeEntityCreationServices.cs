using System;
using UnityEngine;
using Zjcs.Official.HomeUI;

namespace Zjcs.Official.HomeEntityCreation
{
    public interface IHomeEntityPrefabHandle
    {
        GameObject Result { get; }
        void Release();
    }
    public interface IHomeEntityResourceLoader
    {
        IHomeEntityPrefabHandle LoadPrefab(string path);
    }
    public interface IHomeEntitySkinData
    {
        int GetFurnitureSkinId(EFurniture type);
        int GetThemeSkinId(HomeThemeType type);
        int GetEnvironmentSkinId(HomeEnvironmentType type);
    }
    public interface IHomeFurnitureChangePublisher { void Publish(HomeFurnitureChangeData data); }
    public interface IHomeEntityManagerContext
    {
        IHomeEntityResourceLoader ResourceLoader { get; }
        IHomeFurnitureChangePublisher FurnitureChangePublisher { get; }
        IHomeEntitySkinData PlayerData { get; }
        IHomeEntityPrefabTables Tables { get; }
        HomeEntityPrefabPaths PrefabPaths { get; }
        int PreviewWeatherId { get; }
        GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays);
        void OnEntityWillDestroy(GameObject instance);
        void OnEntityCreated(GameObject instance);
    }
    public interface IHomeEntityPrefabTables
    {
        FurnitureSkinInfo GetFurnitureSkin(int skinItemId);
        HomeThemeSkinInfo GetThemeSkin(int skinItemId);
        HomeSkinWeatherOverridePrefabInfo GetWeatherOverride(int skinItemId, int weatherSkinId);
    }
    [Serializable]
    public sealed class FurnitureSkinInfo
    {
        public int SkinItemId;
        public EFurniture FurnitureType;
        public string PrefabName;
        public bool IsDefault;
        public int Order;
        public string AppearanceBlackboardImagePath;
    }
    [Serializable]
    public sealed class HomeThemeSkinInfo
    {
        public int SkinItemId;
        public HomeThemeType ThemeType;
        public string PrefabName;
        public bool IsDefault;
        public int Order;
        public string AppearanceBlackboardImagePath;
    }
    [Serializable]
    public sealed class HomeSkinWeatherOverridePrefabInfo
    {
        public int SkinId, WeatherSkinId;
        public string PrefabName;
    }
}
