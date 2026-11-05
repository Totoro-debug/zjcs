using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        [Serializable] private sealed class SelectedExpected
        { public SelectedWorld[] World; public SelectedEntity[] Entities; }
        [Serializable] private sealed class SelectedWorld
        { public int ClassId; public bool SupportFastClear, EnterNavigateKeyIsNull; public string EnterNavigateKey; }
        [Serializable] private sealed class SelectedEntity
        { public int EntityClassId; public bool IgnoreFastClear; }

        private static IEnumerator VerifyClientMaps()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            int states = 0, entities = 0, camps = 0, selectedFields = 0;
            using (var cancelled = new MainlineSharedWorld(catalog, 11, 6999))
            using (var services = new MainlineSharedClientServices(cancelled.Assemblies))
            {
                var prior = CaptureMapMetadata(cancelled.Assemblies);
                var load = cancelled.LoadClient(_ => 1);
                var binary = cancelled.Configuration.Manager.GetParser(cancelled.Assemblies.CommonType("LevelPropParser"));
                if (!load.MoveNext()) throw new InvalidDataException("Client map load could not be cancelled during parsing.");
                ((IDisposable)load).Dispose();
                if (cancelled.IsLoaded || cancelled.IsReady
                    || binary.GetType().GetField("mBinaryReader", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binary) != null)
                    throw new InvalidDataException("Cancelled client map retained its original file reader.");
                foreach (var field in prior)
                    if (!ReferenceEquals(field.Item2.GetValue(field.Item1), field.Item3))
                        throw new InvalidDataException("Cancelled client map changed original metadata.");
            }
            foreach (int map in new[] { 10, 11, 12, 13, 14, 15, 16 })
            foreach (int status in new[] { 1, 2 })
            {
                using (var world = new MainlineSharedWorld(catalog, map, 7000 + map * 10 + status))
                using (var services = new MainlineSharedClientServices(world.Assemblies))
                {
                    var shared = world.Assemblies;
                    var prior = CaptureMapMetadata(shared);
                    var originalLoader = world.Configuration.Manager.FileLoader;
                    var load = world.LoadClient(_ => status);
                    if (!ReferenceEquals(originalLoader, world.Configuration.Manager.FileLoader))
                        throw new InvalidDataException("Client tables replaced the map's EC loader.");
                    while (load.MoveNext()) yield return load.Current;
                    selectedFields += VerifySelectedFields(world);
                    var manager = services.Manager;
                    var mapProperty = manager.GetType().GetProperty("MapData");
                    var playerProperty = manager.GetType().GetProperty("Player");
                    var previousMap = mapProperty.GetValue(manager);
                    if (playerProperty.GetValue(manager) != null)
                        throw new InvalidDataException("Explicit fresh client fixture found an active player.");
                    var mapData = Activator.CreateInstance(shared.CommonType("PlayerMap"));
                    var player = Activator.CreateInstance(shared.CommonType("Player"));
                    try
                    {
                        // Explicit uniform snapshots exercise both original
                        // container statuses. They are not a formal save adapter.
                        var regions = (IDictionary)NativeMap14SharedPrefabs.Get(world.Data.Placement, "MaxRegionProgressDict");
                        var ids = regions.Keys.Cast<object>().Select(Convert.ToInt32).OrderBy(id => id).ToArray();
                        if (!ids.SequenceEqual(Enumerable.Range(0, ids.Length)))
                            throw new InvalidDataException("Unexpected original region snapshot indexing.");
                        var regionStates = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(shared.CommonType("MapRegionStatus")));
                        foreach (int region in ids) regionStates.Add(Enum.ToObject(shared.CommonType("MapRegionStatus"), status));
                        ((IDictionary)NativeMap14SharedPrefabs.Get(player, "MapRegionStatusDict")).Add(map, regionStates);
                        mapProperty.SetValue(manager, mapData);
                        playerProperty.SetValue(manager, player);
                        mapData.GetType().GetMethod("SetConatainerStatus").Invoke(mapData, new[] {
                            Enum.ToObject(shared.CommonType("MapRegionStatus"), status), NativeMap14SharedPrefabs.Get(world.Root.Container, "Id") });
                        mapData.GetType().GetProperty("mMapLoEntityIdManager", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(mapData, world.LoEntityIds);
                        mapData.GetType().GetProperty("MapContainer").SetValue(mapData, world.Root.Container);
                        var component = MapComponent(world.Root.Root, shared.CommonType("MapComponent"));
                        if (component != null) mapData.GetType().GetProperty("Map").SetValue(mapData, component);
                        var originals = ((IDictionary)NativeMap14SharedPrefabs.Get(world.Root.Container, "EntityDict")).Values.Cast<object>().ToArray();
                        foreach (var entity in originals)
                        {
                            var camp = MapComponent(entity, shared.CommonType("EntityCampComponent"));
                            if (camp == null) continue;
                            int loId = (int)world.LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId")
                                .Invoke(world.LoEntityIds, new[] { NativeMap14SharedPrefabs.Get(entity, "Id") });
                            var placement = world.Data.GetContainerPrefab(loId);
                            var configured = (List<int>)NativeMap14SharedPrefabs.Get(NativeMap14SharedPrefabs.Get(
                                MapComponent(placement, shared.CommonType("LoEntityCampComponent")), "Info"), "CampMonsterEntities");
                            var actual = (List<int>)NativeMap14SharedPrefabs.Get(camp, "CampMonsterEntities");
                            if (ReferenceEquals(configured, actual) || !configured.SequenceEqual(actual))
                                throw new InvalidDataException("Fresh camp did not retain an independent original monster list.");
                            camps++;
                        }
                        world.Start();
                        if (!world.IsReady || originals.Any(entity => !(bool)NativeMap14SharedPrefabs.Get(entity, "Awaked")
                            || !(bool)NativeMap14SharedPrefabs.Get(entity, "Started")))
                            throw new InvalidDataException("Original client did not start every map entity.");
                        if (errors != 0) throw new InvalidDataException("Original map client logged errors: " + errors);
                        states++;
                        entities += originals.Length;
                        Debug.Log($"MAINLINE_CLIENT_MAP_PASS: map={map}, status={status}, entities={originals.Length}");
                        yield return null;
                    }
                    finally
                    {
                        world.Root.Dispose();
                        mapData.GetType().GetProperty("MapContainer").SetValue(mapData, null);
                        playerProperty.SetValue(manager, null);
                        mapProperty.SetValue(manager, previousMap);
                    }
                    world.Dispose();
                    foreach (var field in prior)
                        if (!ReferenceEquals(field.Item2.GetValue(field.Item1), field.Item3))
                            throw new InvalidDataException("Original map metadata scope was not restored: " + field.Item2.Name);
                }
                yield return null;
            }
            if (errors != 0) throw new InvalidDataException("Original map teardown logged errors: " + errors);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "client-maps.json"),
                $"{{\"map_states\":{states},\"entities\":{entities},\"camps\":{camps},\"selected_fields\":{selectedFields},\"original_start\":true,\"metadata_restored\":true,\"cancelled_reader_disposed\":true,\"play_mode\":true}}");
        }

        private static object MapComponent(object entity, Type type) => entity.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
            .Invoke(entity, new object[] { type });

        private static List<Tuple<object, FieldInfo, object>> CaptureMapMetadata(NativeMap14SharedAssemblies shared)
        {
            var result = new List<Tuple<object, FieldInfo, object>>();
            foreach (var name in new[] { "MapInfoManager", "MapRegionEntityInfoManager" })
            {
                var type = shared.CommonType(name);
                var instance = type.GetProperty("Instance").GetValue(null);
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    result.Add(Tuple.Create(instance, field, field.GetValue(instance)));
            }
            return result;
        }

        private static int VerifySelectedFields(MainlineSharedWorld world)
        {
            var expected = JsonUtility.FromJson<SelectedExpected>(Resources.Load<TextAsset>("MainlineSharedWorld/MapConfiguration/expected").text);
            int count = 0;
            foreach (var name in new[] { "MapWorldInfo", "EntityExtendParameterInfo" })
            {
                var parser = world.Configuration.Manager.GetParser(world.Assemblies.CommonType(name + "Parser"));
                string key = name == "MapWorldInfo" ? "ClassId" : "EntityClassId";
                var rows = ((IEnumerable)parser.GetType().GetMethod("GetAll").Invoke(parser, null)).Cast<object>()
                    .ToDictionary(row => (int)NativeMap14SharedPrefabs.Get(row, key));
                if (name == "MapWorldInfo")
                {
                    if (rows.Count != expected.World.Length) throw new InvalidDataException("Selected world table row count differs.");
                    foreach (var row in expected.World)
                    {
                        var actual = rows[row.ClassId];
                        string navigate = (string)NativeMap14SharedPrefabs.Get(actual, "EnterNavigateKey");
                        if ((bool)NativeMap14SharedPrefabs.Get(actual, "SupportFastClear") != row.SupportFastClear
                            || (navigate == null) != row.EnterNavigateKeyIsNull
                            || (navigate != null && navigate != row.EnterNavigateKey))
                            throw new InvalidDataException("Selected world fields differ: " + row.ClassId
                                + " fast=" + NativeMap14SharedPrefabs.Get(actual, "SupportFastClear") + "/" + row.SupportFastClear
                                + " navigate=[" + navigate + "]/[" + row.EnterNavigateKey + "]"
                                + " null=" + (navigate == null) + "/" + row.EnterNavigateKeyIsNull);
                        count += 2;
                    }
                }
                else
                {
                    if (rows.Count != expected.Entities.Length) throw new InvalidDataException("Selected entity table row count differs.");
                    foreach (var row in expected.Entities)
                    {
                        if ((bool)NativeMap14SharedPrefabs.Get(rows[row.EntityClassId], "IgnoreFastClear") != row.IgnoreFastClear)
                            throw new InvalidDataException("Selected entity field differs: " + row.EntityClassId);
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
