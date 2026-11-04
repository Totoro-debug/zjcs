using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Read-only map configuration and asset projection for the original marker
    // rules. Player/entity state remains with the owning map's state Adapter.
    public sealed class MainlineMiniMapMarkerCatalog
    {
        private const string Prefix = "UI/Map/MiniMap/MiniMapMark/";
        private readonly Dictionary<int, Map> maps;
        private readonly Dictionary<int, Extension> extensions;
        private readonly EntityMapUIDisplayInfoParser displays = new EntityMapUIDisplayInfoParser();
        private readonly NativeMap13AssetCatalog assets;

        public MainlineMiniMapMarkerCatalog(TextAsset configuration, NativeMap13AssetCatalog assets)
        {
            if (!configuration || !assets) throw new ArgumentNullException("Original marker data and native assets are required.");
            var source = JsonUtility.FromJson<Data>(configuration.text);
            if (source?.Manifest != "884_156692" || source.Maps == null || source.Extensions == null || source.Displays == null
                || source.Maps.Any(row => row == null || row.MapClassId <= 0 || row.EntityClasses == null)
                || source.Extensions.Any(row => row == null || row.EntityClassId <= 0)
                || source.Displays.Any(row => row == null))
                throw new InvalidDataException("Original marker query configuration is malformed.");
            this.assets = assets;
            maps = source.Maps.ToDictionary(row => row.MapClassId);
            extensions = source.Extensions.ToDictionary(row => row.EntityClassId);
            // JsonUtility creates instance-owned rows. The official parser adds
            // its marker prefix exactly once without mutating a shared resource.
            displays.Load(source.Displays);
            foreach (var row in extensions.Values)
                if (!string.IsNullOrEmpty(row.MiniMapMark)) row.MiniMapMark = Prefix + row.MiniMapMark;
        }

        public MiniMapMarkExtendInfo GetExtend(int entityClassId)
        {
            if (!extensions.TryGetValue(entityClassId, out var row)) return null;
            return new MiniMapMarkExtendInfo { CompleteState = row.CompleteState, DialogIconPrefab = row.DialogIconPrefab,
                MiniMapMark = row.MiniMapMark, MiniMapMarkHideInEnd = row.MiniMapMarkHideInEnd,
                MiniMapMarkShowInPublic = row.MiniMapMarkShowInPublic, MiniMapMarkShowInOffline = row.MiniMapMarkShowInOffline,
                InteractionUnique = row.InteractionUnique };
        }

        public EntityMapUIDisplayInfo GetDisplay(int entityClassId, int map, string areaTag, string entityTag)
        {
            if (!maps.TryGetValue(map, out var info)) throw new InvalidDataException("Original marker map configuration is not installed: " + map);
            return displays.Get(entityClassId, info.MapGroupId, map, areaTag, entityTag);
        }

        // Original MapUtils.GetMapEntityMiniMapMark: an empty UI override falls
        // back to the extension, after the original parser's lookup completes.
        public string GetMarker(int entityClassId, int map, string areaTag, string entityTag)
        {
            var display = GetDisplay(entityClassId, map, areaTag, entityTag);
            if (!string.IsNullOrEmpty(display?.MiniMapMark)) return display.MiniMapMark;
            return extensions.TryGetValue(entityClassId, out var row) ? row.MiniMapMark : null;
        }

        public GameObject LoadPrefab(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath) || !originalPath.StartsWith(Prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Expected the original marker prefab address.");
            return assets.Load<GameObject>(originalPath.EndsWith(".prefab", StringComparison.Ordinal) ? originalPath : originalPath + ".prefab");
        }

        [Serializable] private sealed class Data
        { public string Manifest; public Map[] Maps; public Extension[] Extensions; public EntityMapUIDisplayInfo[] Displays; }
        [Serializable] private sealed class Map
        { public int MapClassId, MapGroupId; public int[] EntityClasses; }
        [Serializable] private sealed class Extension
        {
            public int EntityClassId, CompleteState; public string DialogIconPrefab, MiniMapMark;
            public bool MiniMapMarkHideInEnd, MiniMapMarkShowInPublic, MiniMapMarkShowInOffline, InteractionUnique;
        }
    }
}
