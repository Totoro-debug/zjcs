using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13Entities;

namespace Zjcs.Offline.World.Map13Entities
{
    // Explicit local data boundary. The three selectors and EC path/cache rules
    // are migrated official code; this class supplies current local inputs.
    public sealed class OfflineMap13EntityPresentation : IDisposable
    {
        [Serializable] private sealed class DirectoryData { public string Directory; public int[] Ids; }
        [Serializable] private sealed class AreaData { public int Area; public string AreaTag; }
        [Serializable] private sealed class Data
        {
            public string manifest;
            public string MapSourceInputsSha256;
            public int mapClassId;
            public EntityMapDisplayInfo[] Displays;
            public EntityMapUIDisplayInfo[] UiDisplays;
            public EntityMapDisplayActionInfo[] Actions;
            public DirectoryData[] Directories;
            public AreaData[] Areas;
        }
        [Serializable] private sealed class Source { public string manifest, path, sha256; public int mapClassId; }

        public sealed class Selection
        {
            public string EntityPath;
            public int Area, BaseGroundType;
            public string AreaTag;
            public EntityMapDisplayInfo Display;
            public EntityMapUIDisplayInfo Ui;
            public EntityMapDisplayActionInfo Action;
        }

        public EntityMapDisplayInfoParser Displays { get; }
        public EntityMapUIDisplayInfoParser UiDisplays { get; }
        public EntityMapDisplayActionInfoParser Actions { get; }
        public MyEntityIndexHandler Index { get; }
        private readonly Dictionary<int, string> areaTags = new Dictionary<int, string>();
        private readonly string mapSourceInputsSha256;

        public static OfflineMap13EntityPresentation LoadDefault()
        {
            var data = Resources.Load<TextAsset>("Map13/entity-presentation");
            var source = Resources.Load<TextAsset>("Map13/entity-presentation-source");
            if (data == null || source == null) throw new FileNotFoundException("Current Map13 entity presentation data is not packaged.");
            return new OfflineMap13EntityPresentation(data.text, source.text);
        }

        public OfflineMap13EntityPresentation(string json, string sourceJson)
        {
            var source = JsonUtility.FromJson<Source>(sourceJson);
            using (var hash = SHA256.Create())
                if (source == null || source.manifest != "871_156598" || source.mapClassId != 13
                    || source.path != "unity/Assets/Game/Resources/Map13/entity-presentation.json"
                    || BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant() != source.sha256)
                    throw new InvalidDataException("Current Map13 entity presentation source hash differs.");
            var data = JsonUtility.FromJson<Data>(json);
            if (data.manifest != source.manifest || data.mapClassId != 13 || data.Displays.Length != 1253
                || data.UiDisplays.Length != 757 || data.Actions.Length != 43 || data.Directories.Length != 3860 || data.Areas.Length != 111)
                throw new InvalidDataException("Current Map13 entity presentation counts differ.");
            var directories = new Dictionary<string, int[]>();
            mapSourceInputsSha256 = data.MapSourceInputsSha256;
            foreach (var item in data.Directories) directories.Add(item.Directory, item.Ids);
            Index = new MyEntityIndexHandler(directories);
            foreach (var item in data.Areas) areaTags.Add(item.Area, item.AreaTag);
            Displays = new EntityMapDisplayInfoParser(); Displays.Load(data.Displays);
            UiDisplays = new EntityMapUIDisplayInfoParser(); UiDisplays.Load(data.UiDisplays);
            Actions = new EntityMapDisplayActionInfoParser(); Actions.Load(data.Actions);
        }

        // MyMapDisplay queries the entity's current grid position, not its
        // placement area, and reads the original base ground, not overlays.
        // Current state and tag must be supplied by the actual world entity.
        public Selection Resolve(OfficialMapDefinition map, int entityClassId, int gridX, int gridY, int state, string tag)
        {
            if (map == null || map.ClassId != 13 || map.MapGroupId != 13
                || map.SourceManifest != "Assets/Official/World/Map/13/source-manifest.json"
                || map.SourceManifestSha256 != mapSourceInputsSha256)
                throw new InvalidDataException("Map13 entity presentation requires the selected current map.");
            if (!OfficialMapNavigationView.TryGetCell(map.Navigation, gridX, gridY, out int area, out int ground)
                || !areaTags.TryGetValue(area, out string areaTag))
                throw new InvalidDataException("Map13 entity position has no original area/base-ground mapping.");
            return new Selection
            {
                EntityPath = Index.GetEntityPath(entityClassId), Area = area, AreaTag = areaTag, BaseGroundType = ground,
                Display = Displays.Get(entityClassId, map.MapGroupId, map.ClassId, areaTag, (GroundType)ground, tag),
                Ui = UiDisplays.Get(entityClassId, map.MapGroupId, map.ClassId, areaTag, tag),
                Action = Actions.Get(entityClassId, map.MapGroupId, map.ClassId, areaTag, (GroundType)ground, state, tag)
            };
        }

        public void Dispose() => Index.Dispose();
    }
}
