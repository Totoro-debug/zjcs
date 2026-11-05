using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        [Serializable] private sealed class PlacementExpected { public PlacementMapExpected[] Maps; }
        [Serializable] private sealed class PlacementMapExpected
        {
            public int MapClassId, EntityCount;
            public string EntitySha256;
            public PlacementRegionExpected[] Regions;
        }
        [Serializable] private sealed class PlacementRegionExpected { public int Region, Maximum, GoddessId; }

        private static void VerifyCrossMapMetadata(MainlineSharedWorld world)
        {
            var expected = JsonUtility.FromJson<PlacementExpected>(
                Resources.Load<TextAsset>("MainlineSharedWorld/MapConfiguration/placement-expected").text);
            var shared = world.Assemblies;
            var parser = world.Configuration.Manager.GetParser(shared.CommonType("MapInfoWrapParser"));
            var metadataType = shared.CommonType("MapRegionEntityInfoManager");
            var metadata = metadataType.GetProperty("Instance").GetValue(null);
            var lookup = metadataType.GetMethod("GetContainerEntityInfo");
            var goddess = metadataType.GetMethod("GetMapRegionGoddessId");
            var originalEntities = (IDictionary)NativeMap14SharedPrefabs.Get(world.Root.Container, "EntityDict");
            int before = originalEntities.Count;
            foreach (var map in expected.Maps)
            {
                var args = new object[] { map.MapClassId, null };
                if (!(bool)parser.GetType().GetMethod("TryLoadMapInfoWrap").Invoke(parser, args))
                    throw new InvalidDataException("Original cross-map placement is absent: " + map.MapClassId);
                var wrap = args[1];
                if (map.MapClassId == world.Data.MapClassId && !ReferenceEquals(wrap, world.Data.Placement))
                    throw new InvalidDataException("Current-map placement was duplicated.");
                var maximum = (IDictionary)NativeMap14SharedPrefabs.Get(wrap, "MaxRegionProgressDict");
                if (maximum.Count != map.Regions.Length) throw new InvalidDataException("Cross-map region count differs.");
                foreach (var region in map.Regions)
                    if ((int)maximum[region.Region] != region.Maximum
                        || (int)goddess.Invoke(metadata, new object[] { map.MapClassId, region.Region }) != region.GoddessId)
                        throw new InvalidDataException("Original cross-map region maximum or goddess differs.");
                var regions = (IDictionary)NativeMap14SharedPrefabs.Get(wrap, "RegionInfos");
                var placements = regions.Values.Cast<IEnumerable>().SelectMany(values => values.Cast<object>())
                    .OrderBy(value => (int)NativeMap14SharedPrefabs.Get(value, "LoEntityId")).ToArray();
                if (placements.Length != map.EntityCount) throw new InvalidDataException("Cross-map placement count differs.");
                var text = new StringBuilder();
                foreach (var placement in placements)
                {
                    int id = (int)NativeMap14SharedPrefabs.Get(placement, "LoEntityId");
                    if (!ReferenceEquals(lookup.Invoke(metadata, new object[] { map.MapClassId, id }), placement))
                        throw new InvalidDataException("Original cross-map lookup lost its cached placement identity.");
                    var position = NativeMap14SharedPrefabs.Get(placement, "PosInfo");
                    var grid = NativeMap14SharedPrefabs.Get(position, "Pos");
                    var spawn = NativeMap14SharedPrefabs.Get(placement, "DefSpawnEntityInfo");
                    int[] fields = { id, (int)NativeMap14SharedPrefabs.Get(position, "Region"),
                        (int)NativeMap14SharedPrefabs.Get(position, "Area"), (int)NativeMap14SharedPrefabs.Get(grid, "X"),
                        (int)NativeMap14SharedPrefabs.Get(grid, "Y"), spawn == null ? 0 : (int)NativeMap14SharedPrefabs.Get(spawn, "ClassId"),
                        spawn == null ? 0 : Convert.ToInt32(NativeMap14SharedPrefabs.Get(spawn, "StageType")) };
                    text.Append(string.Join(",", fields.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Append('\n');
                }
                using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant()
                        != map.EntitySha256) throw new InvalidDataException("Original cross-map placement fields differ from official bytes.");
            }
            if (originalEntities.Count != before) throw new InvalidDataException("Cross-map metadata spawned another map's entities.");
            Debug.Log("MAINLINE_CROSS_MAP_METADATA_PASS current=" + world.Data.MapClassId + " maps=" + expected.Maps.Length);
        }
    }
}
