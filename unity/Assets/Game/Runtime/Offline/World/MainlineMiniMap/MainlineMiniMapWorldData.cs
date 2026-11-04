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
        private readonly Dictionary<int, Dictionary<int, Transport>> transports = new Dictionary<int, Dictionary<int, Transport>>();

        public MainlineMiniMapWorldData(IEnumerable<OfficialMapDefinition> definitions, TextAsset regionDisplays, TextAsset entityData)
        {
            if (definitions == null || !regionDisplays || !entityData)
                throw new ArgumentNullException("Original worlds, region displays and marker entity inputs are required.");
            var displayData = JsonUtility.FromJson<DisplayData>(regionDisplays.text);
            if (displayData?.Manifest != "884_156692" || displayData.Displays == null)
                throw new InvalidDataException("Original minimap region-display configuration is missing.");
            foreach (var definition in definitions)
            {
                if (definition == null || definition.ClassId <= 0) throw new InvalidDataException("Original world identity is missing.");
                worlds.Add(definition.ClassId, new World(definition));
            }
            if (worlds.Count == 0) throw new InvalidDataException("At least one original world is required.");
            var transportData = JsonUtility.FromJson<EntityData>(entityData.text);
            if (transportData?.Manifest != "884_156692" || transportData.Transports == null
                || transportData.Areas == null || transportData.EntityTags == null)
                throw new InvalidDataException("Original minimap marker entity configuration is missing.");
            foreach (var row in transportData.Areas)
            {
                if (row == null) throw new InvalidDataException("Original minimap area-tag row is missing.");
                if (!worlds.TryGetValue(row.MapClassId, out var world)) continue;
                if (!world.AreaRegions.ContainsKey(row.Area) || row.AreaTag == null)
                    throw new InvalidDataException("Original minimap area-tag identity differs.");
                world.AreaTags.Add(row.Area, row.AreaTag);
            }
            foreach (var world in worlds.Values)
                if (!world.AreaTags.Keys.OrderBy(id => id).SequenceEqual(world.AreaRegions.Keys.OrderBy(id => id)))
                    throw new InvalidDataException("Original minimap area-tag coverage differs.");
            foreach (var row in transportData.EntityTags)
            {
                if (row == null || row.Tag == null) throw new InvalidDataException("Original minimap entity-tag row is missing.");
                if (worlds.TryGetValue(row.MapClass, out var world)) world.EntityTags.Add(row.LogicId, row.Tag);
            }
            foreach (var row in transportData.Transports)
            {
                if (row == null) throw new InvalidDataException("Original minimap transport row is missing.");
                if (!worlds.TryGetValue(row.MapClassId, out var world)) continue;
                if (!world.Placements.ContainsKey(row.LoEntityId) || row.TargetLoEntityId <= 0
                    || row.TargetMapClassId <= 0 || row.TargetRegion < 0)
                    throw new InvalidDataException("Original minimap transport placement identity differs.");
                if (worlds.TryGetValue(row.TargetMapClassId, out var target)
                    && (!target.Placements.TryGetValue(row.TargetLoEntityId, out var placement)
                        || placement.PosInfo.Pos != row.TargetPos || placement.PosInfo.Region != row.TargetRegion))
                    throw new InvalidDataException("Original minimap transport destination differs.");
                if (!transports.TryGetValue(row.MapClassId, out var entries))
                    transports.Add(row.MapClassId, entries = new Dictionary<int, Transport>());
                entries.Add(row.LoEntityId, row);
            }
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
        public MiniMapMarkMapInfo GetMapInfo(int map) => worlds.TryGetValue(map, out var world)
            ? new MiniMapMarkMapInfo { MapGroupId = world.Info.MapGroupId } : null;
        public int GridToArea(int map, Pos2d position) => worlds.TryGetValue(map, out var world) ? world.Lite.GridToArea(position) : -1;
        public string GetAreaTag(int map, int area) => worlds.TryGetValue(map, out var world)
            && world.AreaTags.TryGetValue(area, out var tag) ? tag : null;
        public string GetEntityTag(int map, int placement) => worlds.TryGetValue(map, out var world)
            && world.EntityTags.TryGetValue(placement, out var tag) ? tag : null;
        public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map, int placement)
            => worlds.TryGetValue(map, out var world) && world.Placements.TryGetValue(placement, out var source)
                ? CopyPlacement(source) : null;
        public IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map, int region)
            => worlds.TryGetValue(map, out var world)
                ? world.Placements.Values.Where(row => row.PosInfo.Region == region).Select(CopyPlacement)
                : Enumerable.Empty<MiniMapMarkLoEntityInfo>();
        public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map, int placement)
            => transports.TryGetValue(map, out var entries) && entries.TryGetValue(placement, out var row)
                ? new MiniMapMarkTransportTargetInfo { TargetMapClassId = row.TargetMapClassId, TargetRegion = row.TargetRegion } : null;

        // Original DTOs are mutable. A view must never change another map view's
        // default placement while projecting its latest local position or class.
        private static MiniMapMarkLoEntityInfo CopyPlacement(MiniMapMarkLoEntityInfo row) => new MiniMapMarkLoEntityInfo
        {
            LoEntityId = row.LoEntityId,
            PosInfo = new MiniMapMarkPositionInfo { Pos = row.PosInfo.Pos, Area = row.PosInfo.Area, Region = row.PosInfo.Region },
            DefSpawnEntityInfo = new MiniMapMarkSpawnEntityInfo { ClassId = row.DefSpawnEntityInfo.ClassId }
        };

        private sealed class World
        {
            public readonly MapRootEntityLiteSingleInfo Lite;
            public readonly Dictionary<int, MiniMapRegionInfo> Regions = new Dictionary<int, MiniMapRegionInfo>();
            public readonly Dictionary<int, int> AreaRegions = new Dictionary<int, int>();
            public readonly Dictionary<int, MiniMapRegionDisplayInfo> Displays = new Dictionary<int, MiniMapRegionDisplayInfo>();
            public readonly Dictionary<int, string> AreaTags = new Dictionary<int, string>();
            public readonly Dictionary<int, string> EntityTags = new Dictionary<int, string>();
            public readonly Dictionary<int, MiniMapMarkLoEntityInfo> Placements = new Dictionary<int, MiniMapMarkLoEntityInfo>();
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
                if (source.Entities == null)
                    throw new InvalidDataException("Original minimap placements are missing.");
                foreach (var placement in source.Entities)
                {
                    if (placement.Grid?.Length != 2 || !Regions.ContainsKey(placement.Region))
                        throw new InvalidDataException("Original minimap placed entity identity differs.");
                    Placements.Add(placement.Id, new MiniMapMarkLoEntityInfo
                    {
                        LoEntityId = placement.Id,
                        PosInfo = new MiniMapMarkPositionInfo { Pos = new Pos2d(placement.Grid[0], placement.Grid[1]),
                            Area = placement.Area, Region = placement.Region },
                        DefSpawnEntityInfo = new MiniMapMarkSpawnEntityInfo { ClassId = placement.EntityId }
                    });
                }
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
        [Serializable] private sealed class MapInfo { public int ClassId, MinimapConfigId, MapGroupId; }
        [Serializable] private sealed class AreaInfo { public int MapClassId, Area; public string AreaTag; }
        [Serializable] private sealed class EntityTag { public int MapClass, LogicId; public string Tag; }
        [Serializable] private sealed class EntityData
        { public string Manifest; public Transport[] Transports; public AreaInfo[] Areas; public EntityTag[] EntityTags; }
        [Serializable] private sealed class Transport
        {
            public int MapClassId, LoEntityId, TargetLoEntityId, TargetMapClassId, TargetRegion;
            public Pos2d TargetPos;
        }
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
