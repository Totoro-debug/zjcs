using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // One projection of the installed official world for all original minimap
    // consumers. Each instance owns its mutable original DTOs and parser rows.
    public sealed class MainlineMiniMapWorldData
    {
        private readonly Dictionary<int, World> worlds = new Dictionary<int, World>();

        public MainlineMiniMapWorldData(IEnumerable<OfficialMapDefinition> definitions, TextAsset regionDisplays)
        {
            if (definitions == null || !regionDisplays) throw new ArgumentNullException("Original worlds and region displays are required.");
            var displayData = JsonUtility.FromJson<DisplayData>(regionDisplays.text);
            if (displayData?.Manifest != "884_156692" || displayData.Displays == null)
                throw new InvalidDataException("Original minimap region-display configuration is missing.");
            foreach (var definition in definitions)
            {
                if (definition == null || definition.ClassId <= 0) throw new InvalidDataException("Original world identity is missing.");
                worlds.Add(definition.ClassId, new World(definition));
            }
            if (worlds.Count == 0) throw new InvalidDataException("At least one original world is required.");
            foreach (var row in displayData.Displays)
            {
                if (row == null) throw new InvalidDataException("Original minimap region-display row is missing.");
                if (!worlds.TryGetValue(row.MapClassId, out var world)) continue;
                if (!world.Regions.ContainsKey(row.Region)) throw new InvalidDataException("Minimap display references another region.");
                world.Displays.Add(row.Region, new MiniMapRegionDisplayInfo { CenterOffsetX = row.CenterOffsetX,
                    CenterOffsetY = row.CenterOffsetY, HighlightScaleX = row.HighlightScaleX, HighlightScaleY = row.HighlightScaleY,
                    LinePrefabPath = row.LinePrefabPath, HighlightPrefabPath = row.HighlightPrefabPath, NameFormat = row.NameFormat });
            }
        }

        public bool TryGetMapLiteData(int map, out MapRootEntityLiteSingleInfo info)
        { if (worlds.TryGetValue(map, out var world)) { info = world.Lite; return true; } info = default; return false; }
        public IReadOnlyDictionary<int, MiniMapRegionInfo> GetMapRegionDict(int map)
            => worlds.TryGetValue(map, out var world) ? world.Regions : null;
        public MiniMapRegionInfo GetRegion(int map, int region)
            => worlds.TryGetValue(map, out var world) && world.Regions.TryGetValue(region, out var value) ? value : null;
        public int AreaToRegion(int map, int area)
            => worlds.TryGetValue(map, out var world) && world.AreaRegions.TryGetValue(area, out var value) ? value : -1;
        public int GridToRegion(int map, Pos2d position)
            => worlds.TryGetValue(map, out var world) ? AreaToRegion(map, world.Lite.GridToArea(position)) : -1;
        public MiniMapRegionDisplayInfo GetRegionDisplay(int map, int region)
            => worlds.TryGetValue(map, out var world) && world.Displays.TryGetValue(region, out var value) ? value : null;
        public IMiniMapGridMover GetSpawnGridMover(int map) => worlds.TryGetValue(map, out var world) ? world.Mover : null;
        public int GetMinimapConfigId(int map) => worlds.TryGetValue(map, out var world) ? world.Info.MinimapConfigId : 0;

        private sealed class World
        {
            public readonly MapRootEntityLiteSingleInfo Lite;
            public readonly Dictionary<int, MiniMapRegionInfo> Regions = new Dictionary<int, MiniMapRegionInfo>();
            public readonly Dictionary<int, int> AreaRegions = new Dictionary<int, int>();
            public readonly Dictionary<int, MiniMapRegionDisplayInfo> Displays = new Dictionary<int, MiniMapRegionDisplayInfo>();
            public readonly IMiniMapGridMover Mover;
            public readonly MapInfo Info;

            public World(OfficialMapDefinition source)
            {
                var nav = source.Navigation;
                if (nav == null || nav.Width <= 0 || nav.Height <= 0 || nav.Rows?.Length != nav.Height
                    || nav.Rows.Any(row => row?.Cells?.Length != nav.Width || row.Cells.Any(cell => cell?.RawValues?.Length != 2))
                    || nav.AreaMappings == null || nav.AreaMappings.Any(row => row?.Cell?.Length != 2)
                    || source.CharacterMover?.CharacterClassId != nav.CharacterId || source.CharacterMover.WalkableGroundTypes == null
                    || source.CharacterMover.ServerOverrideGroundTypes == null || source.Configs == null)
                    throw new InvalidDataException("Original minimap world navigation is incomplete.");
                Lite = new MapRootEntityLiteSingleInfo { MapClassId = source.ClassId, CharacterClassId = nav.CharacterId,
                    MemberClassId = nav.MemberId, MapGroundSize = new Pos2d(nav.Width, nav.Height),
                    MapGroundOriginPos = new Vector3(nav.Origin.X, nav.Origin.Y, nav.Origin.Z), MaxMapFindPathStep = nav.MaxSteps,
                    PosDataArray = nav.Rows.Select(row => row.Cells.Select(cell => new MapRootEntityLiteGridInfo
                        { Area = cell.RawValues[0], GroundType = (GroundType)cell.RawValues[1] }).ToArray()).ToArray(),
                    AreaInfos = nav.AreaMappings.Select(row => new MapRootEntityLiteAreaInfo
                        { TotalPos = new Pos2d(row.Cell[0], row.Cell[1]), TotalCount = row.Value }).ToArray() };
                Mover = new GridMover(source.CharacterMover);
                var map = source.Configs.Single(row => row.TableName == "TbMapInfo");
                Info = JsonUtility.FromJson<MapInfo>(map.Json);
                if (Info.ClassId != source.ClassId || Info.MinimapConfigId <= 0)
                    throw new InvalidDataException("Original world/minimap configuration identity differs.");
                foreach (var config in source.Configs.Where(row => row.TableName == "TbMapRegionInfo"))
                {
                    var label = JsonUtility.FromJson<MapRegionInfo>(config.Json);
                    var geometry = JsonUtility.FromJson<RegionGeometry>(config.Json);
                    if (label.ClassId != source.ClassId || geometry.Areas == null)
                        throw new InvalidDataException("Original minimap region identity is incomplete.");
                    Regions.Add(label.Region, new MiniMapRegionInfo { LabelInfo = label, Areas = geometry.Areas, RegionTag = geometry.RegionTag });
                    foreach (int area in geometry.Areas) AreaRegions.Add(area, label.Region);
                }
                if (Regions.Count == 0 || source.MaxRegionProgress == null
                    || !Regions.Keys.OrderBy(id => id).SequenceEqual(source.MaxRegionProgress.Select(row => row.Region).OrderBy(id => id)))
                    throw new InvalidDataException("Original world/minimap regions differ.");
            }
        }

        private sealed class GridMover : IMiniMapGridMover
        {
            private readonly HashSet<GroundType> normal, serverOverride;
            public GridMover(OfficialMapCharacterMover source)
            { normal = new HashSet<GroundType>(source.WalkableGroundTypes.Select(type => (GroundType)type));
                serverOverride = new HashSet<GroundType>(source.ServerOverrideGroundTypes.Select(type => (GroundType)type)); }
            // Original GridMoverComponentInfo.Walkable, using the decoded key sets.
            public bool Walkable(GroundType groundType) => normal.Contains(groundType) || serverOverride.Contains(groundType);
        }
        [Serializable] private sealed class MapInfo { public int ClassId, MinimapConfigId; }
        [Serializable] private sealed class RegionGeometry { public List<int> Areas; public MapRegionType RegionTag; }
        [Serializable] private sealed class DisplayData { public string Manifest; public Display[] Displays; }
        [Serializable] private sealed class Display
        {
            public int MapClassId, Region;
            public float CenterOffsetX, CenterOffsetY, HighlightScaleX, HighlightScaleY;
            public string LinePrefabPath, HighlightPrefabPath, NameFormat;
        }
    }
}
