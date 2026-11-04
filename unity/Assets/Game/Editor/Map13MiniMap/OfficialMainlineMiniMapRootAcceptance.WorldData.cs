using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.NativeTerrain.Common;
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
            var data = new MainlineMiniMapWorldData(definitions, displays);
            var second = new MainlineMiniMapWorldData(definitions, displays);
            int checkedCells = 0, checkedRegions = 0;
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
            Require(checkedCells == 258400 && checkedRegions == 441, "Complete actual six-map world query scope");
            Debug.Log("Shared original minimap world data passed: six maps, 258400 cells, 441 regions, original coordinates, mover and village display; independent instances.");
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
        }
    }
}
