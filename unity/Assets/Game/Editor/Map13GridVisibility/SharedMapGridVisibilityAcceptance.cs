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
            foreach (int mapId in new[] { 11, 12, 15, 16 })
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
