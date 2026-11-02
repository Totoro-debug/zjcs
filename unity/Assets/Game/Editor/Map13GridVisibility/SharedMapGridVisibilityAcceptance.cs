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
