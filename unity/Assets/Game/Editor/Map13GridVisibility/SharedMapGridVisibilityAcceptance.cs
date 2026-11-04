using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.MapGridVisibility
{
    public static class SharedMapGridVisibilityAcceptance
    {
        public static void RunSourceMaps()
        {
            Run();
            foreach (int mapId in new[] { 10, 11, 12, 14, 15, 16 })
            {
                string root = "World/Mainline/Visibility/Map" + mapId + "/";
                var configuration = JsonUtility.FromJson<OfflineMapGridVisibility.Configuration>(Resources.Load<TextAsset>(root + "visibility").text);
                var bodies = JsonUtility.FromJson<OfflineMapGridVisibility.BodyConfiguration>(Resources.Load<TextAsset>(root + "bodies").text);
                var start = new Pos2d(configuration.size[0] / 2, configuration.size[1] / 2);
                // Explicit rule fixture: all original placed entities are active,
                // region lookup returns 0, movement does not test navigation.
                // Actual scene/region adapters must be verified by their hosts.
                using var map = new OfflineMapGridVisibility(configuration, bodies, start, null, Array.Empty<int>(), _ => 0, _ => true);
                using var isolated = new OfflineMapGridVisibility(configuration, bodies, start, null, Array.Empty<int>(), _ => 0, _ => true);
                byte[] baseline = isolated.Snapshot();
                Require(map.IsVisible(start), "Original configured player cannot see its own cell.");
                map.MovePlayer(new Pos2d(start.X + 1, start.Y));
                map.MovePlayer(new Pos2d(start.X + 2, start.Y));
                Require(map.IsVisible(new Pos2d(start.X + 2, start.Y)), "Original view/body inputs did not reveal the moved player.");
                foreach (var region in configuration.regions) map.UnlockRegion(region.region);
                foreach (var region in configuration.regions)
                    foreach (var point in map.RegionPositions(region.region))
                        if (point.X >= 0 && point.Y >= 0 && point.X < configuration.size[0] && point.Y < configuration.size[1])
                            Require(map.IsVisible(point), "Original activation/extension cell remains hidden.");
                Require(baseline.SequenceEqual(isolated.Snapshot()), "Different map instances mutated each other's original payload.");
                byte[] snapshot = map.Snapshot();
                using var restored = new OfflineMapGridVisibility(configuration, bodies, new Pos2d(start.X + 2, start.Y), snapshot,
                    Array.Empty<int>(), _ => 0, _ => true);
                Require(snapshot.SequenceEqual(restored.Snapshot()), "Actual source payload changed bytes on cold restore.");
                for (int y = 0; y < configuration.size[1]; y++) for (int x = 0; x < configuration.size[0]; x++)
                    Require(map.IsVisible(new Pos2d(x, y)) == restored.IsVisible(new Pos2d(x, y)), "Actual source cell differs on cold restore.");
                Debug.Log($"Original shared visibility input passed: Map{mapId}, {configuration.size[0]}x{configuration.size[1]}, " +
                    $"{bodies.templates.Length} body templates, {bodies.entities.Length} placements, {configuration.regions.Length} regions; isolated rule fixture, exact all-cell cold restore.");
            }
        }

        public static void Run()
        {
            RunExploredEntities();
            // First exercise the original 57,600-cell payload and its persisted
            // bytes through the same interface used by the formal map host.
            Map13GridVisibility.OfficialMap13LocalVisibilityAcceptance.Run();
            var original = JsonUtility.FromJson<OfflineMapGridVisibility.BodyConfiguration>(
                Resources.Load<TextAsset>("Map13/grid-entities").text);
            var body = original.templates.Single(t => t.classId == 3001).body;
            foreach (int mapId in new[] { 11, 12, 15, 16 })
            {
                // Deliberately small rectangular fixtures; these are interface
                // tests, not substitutes for the corresponding official maps.
                var configuration = new OfflineMapGridVisibility.Configuration
                {
                    manifest = "interface-fixture", mapClassId = mapId,
                    size = new[] { 17, 9 }, viewRange = 1, characterClassId = 4500,
                    regions = new[] { new OfflineMapGridVisibility.Region
                    {
                        region = 7, RegionActivedGrids = new[] { new Pos2d(14, 5) },
                        RegionExtendVisiableGrids = new[] { new Pos2d(15, 5) }
                    } }
                };
                var bodies = new OfflineMapGridVisibility.BodyConfiguration
                {
                    manifest = configuration.manifest, mapClassId = mapId,
                    templates = new[] { new OfflineMapGridVisibility.BodyTemplate { classId = 4500, body = body } },
                    entities = Array.Empty<OfflineMapGridVisibility.BodyPlacement>()
                };
                using var first = new OfflineMapGridVisibility(configuration, bodies, new Pos2d(2, 2), null,
                    Array.Empty<int>(), _ => 7, _ => false);
                using var second = new OfflineMapGridVisibility(configuration, bodies, new Pos2d(2, 2), null,
                    Array.Empty<int>(), _ => 7, _ => false);
                Require(first.IsVisible(new Pos2d(2, 2)) && !first.IsVisible(new Pos2d(14, 5)), "Configured sight differs.");
                first.MovePlayer(new Pos2d(3, 2));
                Require(first.IsVisible(new Pos2d(4, 2)) && !second.IsVisible(new Pos2d(4, 2)), "Map instances share visibility.");
                first.UnlockRegion(7);
                Require(first.IsVisible(new Pos2d(14, 5)) && first.IsVisible(new Pos2d(15, 5))
                    && !second.IsVisible(new Pos2d(14, 5)), "Configured region unlock or instance isolation differs.");
                byte[] snapshot = first.Snapshot();
                Require(BitConverter.ToInt32(snapshot, 0) == 9 && BitConverter.ToInt32(snapshot, 4) == 17,
                    "Rectangular maps must retain the original height/width header order.");
                using var restored = new OfflineMapGridVisibility(configuration, bodies, new Pos2d(3, 2), snapshot,
                    Array.Empty<int>(), _ => 7, _ => false);
                Require(snapshot.SequenceEqual(restored.Snapshot()), "Shared interface changed saved visibility bytes.");
                var corrupt = snapshot.Concat(BitConverter.GetBytes(152)).Concat(BitConverter.GetBytes(2)).ToArray();
                Require(!OfflineMapGridVisibility.IsValidSnapshot(corrupt, 17, 9), "Out-of-map saved runs were accepted.");
                bool rejected = false;
                try
                {
                    using var invalid = new OfflineMapGridVisibility(configuration, bodies, new Pos2d(3, 2), corrupt,
                        Array.Empty<int>(), _ => 7, _ => false);
                }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Corrupt snapshots must fail before the official recovering decoder starts.");
                using var seeded = new OfflineMapGridVisibility(configuration, bodies, new Pos2d(2, 2), null,
                    new[] { 7 }, _ => 7, _ => false);
                Require(seeded.IsVisible(new Pos2d(14, 5)) && seeded.IsVisible(new Pos2d(15, 5)),
                    "An already public region did not seed its configured activation cells.");
            }
            Debug.Log("Shared map visibility passed: original Map13 payload/save plus four independent rectangular-map fixtures, configurable character/sight/regions, exact cold restore and corrupt-run rejection.");
        }

        private static void RunExploredEntities()
        {
            var source = JsonUtility.FromJson<OfflineMapGridVisibility.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);
            foreach (int mapId in new[] { 11, 16 })
            {
                var config = new OfflineMapGridVisibility.Configuration
                {
                    manifest = "exploration-interface-fixture", mapClassId = mapId, size = new[] { 17, 9 },
                    viewRange = 1, characterClassId = 4500,
                    regions = new[] { new OfflineMapGridVisibility.Region { region = 7,
                        RegionActivedGrids = new[] { new Pos2d(14, 5) }, RegionExtendVisiableGrids = Array.Empty<Pos2d>() } }
                };
                // Non-body marks isolate the anchor-cell discovery rule from the
                // separately tested official whole-body/camp reveal calculation.
                var bodies = new OfflineMapGridVisibility.BodyConfiguration
                {
                    manifest = config.manifest, mapClassId = mapId,
                    templates = new[] { new OfflineMapGridVisibility.BodyTemplate { classId = 4500,
                        body = source.templates.Single(t => t.classId == 3001).body } },
                    entities = new[] { Placement(101, 2, 2), Placement(102, 14, 5), Placement(103, 2, 3),
                        Placement(104, 14, 6), Placement(105, 2, 3) }
                };
                var inactive = new System.Collections.Generic.HashSet<int> { 103 };
                Pos2d Position(int id) => id == 104 ? new Pos2d(2, 3) : id == 105 ? new Pos2d(14, 6)
                    : bodies.entities.Single(e => e.placementId == id).transform.Pos;
                using var map = new OfflineMapGridVisibility(config, bodies, new Pos2d(2, 2), null,
                    Array.Empty<int>(), _ => 7, id => !inactive.Contains(id), null, Position);
                using var isolated = new OfflineMapGridVisibility(config, bodies, new Pos2d(2, 2), null,
                    Array.Empty<int>(), _ => 7, id => !inactive.Contains(id), null, Position);
                Require(map.ExploredEntityIds.SequenceEqual(new[] { 101, 104 }), "Hidden/inactive or default-position entities leaked into discovery.");
                int[] copy = map.ExploredEntityIds; copy[0] = 999;
                map.SetEntityPosition(101, new Pos2d(14, 7));
                map.SetEntityPosition(105, new Pos2d(2, 3));
                inactive.Remove(103);
                map.MovePlayer(new Pos2d(3, 2));
                Require(map.ExploredEntityIds.SequenceEqual(new[] { 101, 103, 104, 105 }), "Movement, activation or defensive exploration snapshots differ.");
                Require(!isolated.ExploredEntityIds.Contains(105), "Independent map instances share moved entities or exploration.");
                map.UnlockRegion(7);
                Require(map.ExploredEntityIds.SequenceEqual(new[] { 101, 102, 103, 104, 105 }), "Region reveal failed to discover its entity.");
                inactive.UnionWith(new[] { 101, 102, 103, 104, 105 });
                var saved = new ExplorationSave { mapId = mapId, visibility = Convert.ToBase64String(map.Snapshot()), entities = map.ExploredEntityIds };
                string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")
                    ?? throw new InvalidDataException("Explicit validation directory required."), "entity-exploration-" + mapId + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(saved));
                var read = JsonUtility.FromJson<ExplorationSave>(File.ReadAllText(path));
                Require(read.mapId == mapId, "Exploration save map identity differs.");
                using var restored = new OfflineMapGridVisibility(config, bodies, new Pos2d(3, 2), Convert.FromBase64String(read.visibility),
                    Array.Empty<int>(), _ => 7, _ => false, read.entities, Position);
                Require(restored.ExploredEntityIds.SequenceEqual(saved.entities)
                    && restored.Snapshot().SequenceEqual(map.Snapshot()), "Inactive/moved discoveries or original RLE changed on disk cold restore.");
                foreach (int[] invalid in new[] { new[] { 101, 101 }, new[] { -1 }, new[] { 999 } })
                {
                    bool rejected = false;
                    try { using var broken = new OfflineMapGridVisibility(config, bodies, new Pos2d(2, 2), null,
                        Array.Empty<int>(), _ => 7, _ => true, invalid); }
                    catch (InvalidDataException) { rejected = true; }
                    Require(rejected, "Invalid/foreign exploration placement IDs were accepted.");
                }
            }
            Debug.Log("MAINLINE_ENTITY_EXPLORATION_PASS: two map configurations; fog/inactive exclusion, current positions, movement, region reveal, isolation, defensive copies, disk cold restore and invalid save rejection.");
        }

        private static OfflineMapGridVisibility.BodyPlacement Placement(int id, int x, int y) => new OfflineMapGridVisibility.BodyPlacement
        { placementId = id, classId = 9101, transform = new OfflineMapGridVisibility.PlacementTransform { Pos = new Pos2d(x, y) } };

        [Serializable] private sealed class ExplorationSave
        { public int mapId; public string visibility; public int[] entities; }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
