using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Official.HomeEntityCreation
{
    // Local read-only projection of the selected original configuration tables.
    // Address selection and weather precedence remain in the migrated methods.
    public sealed class HomeEntityCreationCatalog : IHomeEntityPrefabTables
    {
        [Serializable]
        public sealed class Payload
        {
            public FurnitureSkinInfo[] FurnitureSkins;
            public HomeThemeSkinInfo[] ThemeSkins;
            public HomeSkinWeatherOverridePrefabInfo[] WeatherOverrides;
        }
        readonly Dictionary<int, FurnitureSkinInfo> furniture;
        readonly Dictionary<int, HomeThemeSkinInfo> themes;
        readonly Dictionary<(int, int), HomeSkinWeatherOverridePrefabInfo> weather;
        public HomeEntityCreationCatalog(Payload payload)
        {
            if (payload?.FurnitureSkins == null || payload.ThemeSkins == null || payload.WeatherOverrides == null)
                throw new ArgumentException("Complete local entity creation tables are required", nameof(payload));
            furniture = payload.FurnitureSkins.ToDictionary(row => row.SkinItemId);
            themes = payload.ThemeSkins.ToDictionary(row => row.SkinItemId);
            weather = payload.WeatherOverrides.ToDictionary(row => (row.SkinId, row.WeatherSkinId));
        }
        public static HomeEntityCreationCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Home/EntityCreation/catalog");
            if (!asset) throw new InvalidOperationException("The local official home entity creation catalog is missing");
            return new HomeEntityCreationCatalog(JsonUtility.FromJson<Payload>(asset.text));
        }
        public FurnitureSkinInfo GetFurnitureSkin(int id) => furniture.TryGetValue(id, out var row) ? row : null;
        public HomeThemeSkinInfo GetThemeSkin(int id) => themes.TryGetValue(id, out var row) ? row : null;
        public HomeSkinWeatherOverridePrefabInfo GetWeatherOverride(int skin, int environment)
            => weather.TryGetValue((skin, environment), out var row) ? row : null;
    }
}
