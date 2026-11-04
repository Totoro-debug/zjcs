using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Offline.World.MainlineMiniMap;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private static MainlineMiniMapWorldData VerifyWorldData()
        {
            var definitions = new[] { 10, 11, 12, 13, 14, 16 }.Reverse().Select(id =>
                JsonUtility.FromJson<OfficialMapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"Assets/Official/World/Map/{id}/map{id}.json").text)).ToArray();
            var displays = Resources.Load<TextAsset>("MainlineMiniMap/current-region-displays");
            var transports = Resources.Load<TextAsset>("MainlineMiniMap/current-entity-data");
            var data = new MainlineMiniMapWorldData(definitions, displays, transports);
            var second = new MainlineMiniMapWorldData(definitions, displays, transports);
            int checkedCells = 0, checkedRegions = 0, checkedPlacements = 0;
            foreach (var definition in definitions)
            {
                int map = definition.ClassId; var nav = definition.Navigation;
                Require(data.TryGetMapLiteData(map, out var lite) && lite.MapClassId == map
                    && lite.MapGroundSize == new Pos2d(nav.Width, nav.Height) && lite.CharacterClassId == nav.CharacterId
                    && lite.MemberClassId == nav.MemberId && lite.MaxMapFindPathStep == nav.MaxSteps,
                    "Original minimap navigation identity: " + map);
                Require(lite.GridToPos(new Pos2d(0, 0)) == new Vector3(nav.Origin.X, nav.Origin.Y, nav.Origin.Z)
                    && lite.PosToGrid(lite.GridToPos(new Pos2d(nav.Width - 1, nav.Height - 1))) == new Pos2d(nav.Width - 1, nav.Height - 1),
                    "Original world/minimap coordinate methods: " + map);
                for (int y = 0; y < nav.Height; y++) for (int x = 0; x < nav.Width; x++)
                {
                    var original = nav.Rows[y].Cells[x].RawValues; var cell = lite.PosDataArray[y][x];
                    Require(cell.Area == original[0] && (int)cell.GroundType == original[1], "Original navigation cell projection");
                    checkedCells++;
                }
                Require(lite.AreaInfos.Length == nav.AreaMappings.Length, "All original area aggregate rows");
                for (int i = 0; i < lite.AreaInfos.Length; i++)
                    Require(lite.AreaInfos[i].TotalPos == new Pos2d(nav.AreaMappings[i].Cell[0], nav.AreaMappings[i].Cell[1])
                        && lite.AreaInfos[i].TotalCount == nav.AreaMappings[i].Value, "Original area aggregate projection");
                var regions = data.GetMapRegionDict(map);
                Require(regions.Count == definition.MaxRegionProgress.Length, "Original region membership count");
                foreach (var region in regions.Values)
                {
                    Require(region.ClassId == map && ReferenceEquals(region, data.GetRegion(map, region.Region)), "Original region identity");
                    foreach (int area in region.Areas) Require(data.AreaToRegion(map, area) == region.Region, "Original area-to-region lookup");
                    checkedRegions++;
                }
                Require(data.AreaToRegion(map, int.MaxValue) == -1 && data.GetRegion(map, int.MaxValue) == null
                    && data.GetRegionDisplay(map, int.MaxValue) == null, "Original missing region branches");
                Require(data.GetSpawnGridMover(map).Walkable((GroundType)0)
                    && data.GetSpawnGridMover(map).Walkable((GroundType)7)
                    && !data.GetSpawnGridMover(map).Walkable((GroundType)1), "Actual normal and server-override mover sets");
                Require(data.GetMapInfo(map).MapGroupId == definition.MapGroupId, "Original marker map group");
                foreach (var placement in definition.Entities)
                {
                    var row = data.GetContainerEntityInfo(map, placement.Id);
                    Require(row.LoEntityId == placement.Id && row.DefSpawnEntityInfo.ClassId == placement.EntityId
                        && row.PosInfo.Pos == new Pos2d(placement.Grid[0], placement.Grid[1])
                        && row.PosInfo.Area == placement.Area && row.PosInfo.Region == placement.Region,
                        "Marker lookup stays in its installed world: " + map + "/" + placement.Id);
                    checkedPlacements++;
                }
                foreach (var region in regions.Keys)
                    Require(data.GetEntityByMapRegion(map, region).Select(row => row.LoEntityId).OrderBy(id => id)
                        .SequenceEqual(definition.Entities.Where(row => row.Region == region).Select(row => row.Id).OrderBy(id => id)),
                        "Original public-region marker enumeration");
            }
            var village = data.GetRegionDisplay(13, 38);
            Require(village.NameFormat == "<color=#FFE50E>{0}</color>" && village.HighlightScaleX == 1.559999942779541f
                && village.LinePrefabPath == "Map/Common/MiniMap/RegionDisplay/MinimapLineForVillage", "Actual original village display configuration");
            Require(!data.TryGetMapLiteData(15, out _) && data.GetMapRegionDict(15) == null && data.AreaToRegion(15, 0) == -1,
                "No invented independent tower minimap");
            data.TryGetMapLiteData(13, out var firstLite); second.TryGetMapLiteData(13, out var secondLite);
            int savedArea = firstLite.PosDataArray[0][0].Area; firstLite.PosDataArray[0][0].Area = int.MaxValue;
            int savedLevel = data.GetRegion(13, 0).LabelInfo.Level; data.GetRegion(13, 0).LabelInfo.Level = int.MaxValue;
            Require(secondLite.PosDataArray[0][0].Area == savedArea
                && definitions.Single(d => d.ClassId == 13).Navigation.Rows[0].Cells[0].RawValues[0] == savedArea
                && second.GetRegion(13, 0).LabelInfo.Level == savedLevel, "Two map views cannot mutate each other's navigation or labels");
            firstLite.PosDataArray[0][0].Area = savedArea; data.GetRegion(13, 0).LabelInfo.Level = savedLevel;
            var firstPlacement = data.GetContainerEntityInfo(10, 501);
            firstPlacement.PosInfo.Pos = Pos2d.Zero; firstPlacement.DefSpawnEntityInfo.ClassId = int.MaxValue;
            Require(data.GetContainerEntityInfo(10, 501).PosInfo.Pos != Pos2d.Zero
                && data.GetContainerEntityInfo(10, 501).DefSpawnEntityInfo.ClassId != int.MaxValue,
                "Latest local projections cannot mutate original default placements");
            Require(data.GetMapTransportTargetInfo(10, 501).TargetMapClassId == 11
                && data.GetMapTransportTargetInfo(13, 4504).TargetRegion == 45
                && data.GetMapTransportTargetInfo(13, 4701).TargetRegion == 105
                && data.GetMapTransportTargetInfo(14, 9701).TargetMapClassId == 15,
                "Original cross-map destinations and both water-map transport directions");
            Require(data.GetMapTransportTargetInfo(16, 501) == null && data.GetContainerEntityInfo(16, int.MaxValue) == null
                && data.GetEntityTag(10, int.MaxValue) == null, "Missing original placement, transport and logic-tag branches");
            Require(!string.IsNullOrEmpty(data.GetEntityTag(10, 501)) && !string.IsNullOrEmpty(data.GetEntityTag(16, 106)),
                "Current original logic tags remain available when historical map definitions omitted the table");
            Require(checkedCells == 258400 && checkedRegions == 441 && checkedPlacements == 11148,
                "Complete actual six-map world query scope");
            Debug.Log("Shared original minimap world data passed: six maps, 258400 cells, 441 regions, 11148 placements, tags and original transports; independent instances.");
            VerifyEntityIndex(data, definitions);
            VerifySharedEntities(data);
            return data;
        }

        private static void VerifyWorldDataBinding(MainlineNativeMiniMapRoot root, MainlineMiniMapBindings bindings)
        {
            Require(root.TryGetMapLiteData(root.MapClassId, out var bound)
                && bindings.WorldData.TryGetMapLiteData(root.MapClassId, out var expected)
                && ReferenceEquals(bound.PosDataArray, expected.PosDataArray)
                && ReferenceEquals(root.GetMapRegionDict(root.MapClassId), bindings.WorldData.GetMapRegionDict(root.MapClassId))
                && ReferenceEquals(root.GetSpawnGridMover(root.MapClassId), bindings.WorldData.GetSpawnGridMover(root.MapClassId)),
                "Complete original root consumes the shared world projection: " + root.MapClassId);
            Require(ReferenceEquals(root.ManagedEvents, bindings.Entities.Events)
                && ReferenceEquals(root.ManagedAreaAgent, bindings.Entities)
                && ReferenceEquals(root.ManagedGridGround, bindings.Entities),
                "Original mark managers share one injected entity source independent of state fixture ports");
            var parameters = LoEntityDescParamsUtils.GetLoEntityDescParams(root.MarkDataQueries, 10,
                root.MarkDataQueries.GetContainerEntityInfo(10, 501));
            Require(parameters.Single().KeyOrText == "region_11_0", "Original offline marker formats the actual destination region");
            foreach (var spawn in root.GetComponentsInChildren<MiniMapMarkSpawnManager>(true))
                Require(ReferenceEquals(spawn.ServiceProvider.GetService(typeof(IMiniMapMarkDataQueries)), root.MarkDataQueries),
                    "Live marker factories consume the same world queries as their original root");
            foreach (var marks in root.GetComponentsInChildren<OfflineMiniMapMarkManager>(true))
                Require(ReferenceEquals(marks.ServiceProvider.GetService(typeof(IMiniMapMarkDataQueries)), root.MarkDataQueries),
                    "Offline marker factories consume the same world queries as their original root");
        }
    }
}
