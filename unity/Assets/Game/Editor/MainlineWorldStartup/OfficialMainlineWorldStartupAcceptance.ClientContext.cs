using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using Zjcs.Offline.World.MainlinePlayerState;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private static IEnumerator VerifyClientContext()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            int maps = 0, mixed = 0, entities = 0, lookups = 0;
            using (var cancelled = new MainlineSharedWorld(catalog, 11, 8990))
            {
                var shared = cancelled.Assemblies;
                var scope = Assembly.Load("Framework.FirstPass.Runtime").GetType("Framework.DependencyInjection.ApplicationScope", true)
                    .GetProperty("Current");
                var previous = scope.GetValue(null);
                var formula = shared.CommonType("Formula").GetField("Instance");
                var priorFormula = formula.GetValue(null);
                var clock = CaptureClientClock(shared);
                var load = cancelled.LoadClient(_ => 1, 8991);
                var reader = cancelled.Configuration.Manager.GetParser(shared.CommonType("LevelPropParser"));
                if (!load.MoveNext()) throw new InvalidDataException("Client context could not be cancelled during parsing.");
                ((IDisposable)load).Dispose();
                VerifyClientClockRestored(clock);
                if (cancelled.IsLoaded || cancelled.IsReady || !ReferenceEquals(scope.GetValue(null), previous)
                    || !ReferenceEquals(formula.GetValue(null), priorFormula)
                    || reader.GetType().GetField("mBinaryReader", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader) != null)
                    throw new InvalidDataException("Cancelled client context retained its original dependencies or file reader.");
            }
            foreach (int mapId in new[] { 10, 11, 12, 13, 14, 15, 16 })
            {
                long privateId = 9000 + mapId * 10, publicId = privateId + 1;
                using (var world = new MainlineSharedWorld(catalog, mapId, privateId))
                {
                    var shared = world.Assemblies;
                    var metadata = CaptureMapMetadata(shared);
                    var scope = Assembly.Load("Framework.FirstPass.Runtime").GetType("Framework.DependencyInjection.ApplicationScope", true)
                        .GetProperty("Current");
                    var previousScope = scope.GetValue(null);
                    var clock = CaptureClientClock(shared);
                    var calls = new Dictionary<int, int>();
                    var regionStates = new Dictionary<int, int>();
                    var load = world.LoadClient(region =>
                    {
                        calls[region] = calls.TryGetValue(region, out int count) ? count + 1 : 1;
                        var type = shared.CommonType("MapInfoManager");
                        var part = type.GetMethod("GetMapPartInfo").Invoke(type.GetProperty("Instance").GetValue(null), new object[] { mapId });
                        bool both = (bool)NativeMap14SharedPrefabs.Get(part, "SupportPrivateRegion")
                            && (bool)NativeMap14SharedPrefabs.Get(part, "SupportPublicRegion");
                        int status = both ? region % 2 + 1 : Convert.ToInt32(NativeMap14SharedPrefabs.Get(part, "MapDefaultRegionStatus"));
                        regionStates[region] = status;
                        return status;
                    }, publicId);
                    var manager = shared.Shared.GetType("MeManager", true).GetProperty("Instance").GetValue(null);
                    var playerProperty = manager.GetType().GetProperty("Player");
                    var mapProperty = manager.GetType().GetProperty("MapData");
                    var previousMap = mapProperty.GetValue(manager);
                    if (playerProperty.GetValue(manager) != null) throw new InvalidDataException("Fresh fixture has an active player.");
                    while (load.MoveNext()) yield return load.Current;
                    VerifyCrossMapMetadata(world);
                    var time = shared.CommonType("TimeManager").GetProperty("Instance").GetValue(null);
                    var now = (DateTime)time.GetType().GetMethod("GetDateTime").Invoke(time, new object[] { DateTimeKind.Utc });
                    if (!(bool)time.GetType().GetField("TimeSynced").GetValue(time)
                        || Math.Abs((DateTime.UtcNow - now).TotalSeconds) > 5)
                        throw new InvalidDataException("Original client clock did not use the local UTC clock.");
                    if (calls.Count == 0 || calls.Values.Any(value => value != 1))
                        throw new InvalidDataException("Client snapshot did not read each region exactly once.");
                    var player = ProjectLocalPlayer(world);
                    world.StartClient(player);
                    var map = mapProperty.GetValue(manager);
                    if (!world.IsReady || !ReferenceEquals(playerProperty.GetValue(manager), player)
                        || !ReferenceEquals(NativeMap14SharedPrefabs.Get(map, "MapContainer"), world.Root.Container))
                        throw new InvalidDataException("Public client startup did not bind the real player and map.");
                    var statusType = shared.CommonType("MapRegionStatus");
                    var metadataType = shared.CommonType("MapInfoManager");
                    var info = metadataType.GetMethod("GetMapPartInfo").Invoke(metadataType.GetProperty("Instance").GetValue(null), new object[] { mapId });
                    bool supportPrivate = (bool)NativeMap14SharedPrefabs.Get(info, "SupportPrivateRegion");
                    bool supportPublic = (bool)NativeMap14SharedPrefabs.Get(info, "SupportPublicRegion");
                    if ((bool)map.GetType().GetMethod("IsMixMap").Invoke(map, null) != (supportPrivate && supportPublic))
                        throw new InvalidDataException("Original mixed-map state differs from its official configuration.");
                    if (supportPrivate && supportPublic) mixed++;
                    foreach (int status in new[] { 1, 2 })
                    {
                        bool supported = status == 1 ? supportPrivate : supportPublic;
                        var args = new[] { Enum.ToObject(statusType, status), (object)0L };
                        bool found = (bool)map.GetType().GetMethod("TryGetContainerIdByStatus").Invoke(map, args);
                        long expected = status == 2 && supportPrivate ? publicId : privateId;
                        if (found != supported || (found && (long)args[1] != expected))
                            throw new InvalidDataException("Original status-to-container lookup differs.");
                        if (found)
                        {
                            var inverse = new[] { (object)expected, Enum.ToObject(statusType, 0) };
                            if (!(bool)map.GetType().GetMethod("TryGetStatusByContainerId").Invoke(map, inverse)
                                || Convert.ToInt32(inverse[1]) != status)
                                throw new InvalidDataException("Original container-to-status lookup differs.");
                        }
                    }
                    if ((int)map.GetType().GetMethod("GetMapLineIndex", Type.EmptyTypes).Invoke(map, null) != 1)
                        throw new InvalidDataException("Local map did not retain exactly one original channel.");
                    var getByLo = map.GetType().GetMethod("GetEntIdByLoEntId", new[] { typeof(int) });
                    var getByContainer = map.GetType().GetMethod("GetEntIdByLoEntId", new[] { typeof(long), typeof(int) });
                    var originals = ((IDictionary)NativeMap14SharedPrefabs.Get(world.Root.Container, "EntityDict")).Values.Cast<object>().ToArray();
                    foreach (var entity in originals)
                    {
                        if (!(bool)NativeMap14SharedPrefabs.Get(entity, "Awaked") || !(bool)NativeMap14SharedPrefabs.Get(entity, "Started"))
                            throw new InvalidDataException("Public client startup skipped an original entity lifecycle.");
                        var id = NativeMap14SharedPrefabs.Get(entity, "Id");
                        int lo = (int)map.GetType().GetMethod("GetLoEntIdByEntId").Invoke(map, new[] { id });
                        if (lo < 0) continue;
                        long owner = (long)NativeMap14SharedPrefabs.Get(id, "ContainerId");
                        if (!Equals(getByContainer.Invoke(map, new object[] { owner, lo }), id))
                            throw new InvalidDataException("Original direct Lo lookup lost a merged entity.");
                        // The official region lookup falls back to the map's
                        // default status on maps that support only one status.
                        var placement = shared.CommonType("MapRegionEntityInfoManager").GetProperty("Instance").GetValue(null);
                        var original = placement.GetType().GetMethod("GetContainerEntityInfo").Invoke(placement, new object[] { mapId, lo });
                        int region = (int)NativeMap14SharedPrefabs.Get(NativeMap14SharedPrefabs.Get(original, "PosInfo"), "Region");
                        int status = regionStates[region];
                        bool supported = status == 1 ? supportPrivate : supportPublic;
                        int targetStatus = supported ? status : Convert.ToInt32(NativeMap14SharedPrefabs.Get(info, "MapDefaultRegionStatus"));
                        long target = targetStatus == 2 && supportPrivate ? publicId : privateId;
                        if (owner != target || !Equals(getByLo.Invoke(map, new object[] { lo }), id))
                            throw new InvalidDataException("Original region Lo lookup lost a merged entity.");
                        lookups++;
                    }
                    if (supportPrivate && supportPublic) VerifyRegionTransition(world, player, regionStates, publicId);
                    entities += originals.Length;
                    if (errors != 0) throw new InvalidDataException("Original merged-map startup logged errors.");
                    yield return null;
                    var later = (DateTime)time.GetType().GetMethod("GetDateTime").Invoke(time, new object[] { DateTimeKind.Utc });
                    if (later <= now) throw new InvalidDataException("Original client clock did not advance across frames.");
                    world.Dispose();
                    VerifyClientClockRestored(clock);
                    if (playerProperty.GetValue(manager) != null || !ReferenceEquals(mapProperty.GetValue(manager), previousMap))
                        throw new InvalidDataException("Client map teardown retained a player or changed the previous map.");
                    if (!ReferenceEquals(scope.GetValue(null), previousScope))
                        throw new InvalidDataException("Client map teardown retained the original application scope.");
                    foreach (var field in metadata)
                        if (!ReferenceEquals(field.Item2.GetValue(field.Item1), field.Item3))
                            throw new InvalidDataException("Client map teardown retained original map metadata.");
                    Debug.Log($"MAINLINE_CLIENT_CONTEXT_PASS: map={mapId}, mixed={supportPrivate && supportPublic}, entities={originals.Length}");
                    maps++;
                }
                yield return null;
            }
            if (errors != 0) throw new InvalidDataException("Original client teardown logged errors.");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "client-context.json"),
                $"{{\"maps\":{maps},\"mixed_maps\":{mixed},\"entities\":{entities},\"lo_lookups\":{lookups},\"original_start\":true,\"context_restored\":true,\"local_clock\":true,\"clock_restored\":true,\"cancelled_dependencies_restored\":true,\"play_mode\":true}}");
        }

        private static void VerifyRegionTransition(MainlineSharedWorld world, object player,
            Dictionary<int, int> regionStates, long publicId)
        {
            object Get(object value, string name) => NativeMap14SharedPrefabs.Get(value, name);
            var shared = world.Assemblies;
            int mapId = world.Data.MapClassId;
            int region = regionStates.First(pair => pair.Value == 1).Key;
            var maximum = (IDictionary)Get(world.Data.Placement, "MaxRegionProgressDict");
            // Explicit completed-region fixture; original replacement eligibility
            // must consume actual raw points, not the normalized save percentage.
            MainlineSharedPlayerProjection.SynchronizeRegionProgress(player, mapId,
                maximum.Keys.Cast<int>().ToDictionary(id => id, id => (int)maximum[id]));
            var container = world.Root.Container;
            var placementOf = world.LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId");
            Dictionary<int, object> Current() => ((IDictionary)Get(container, "EntityDict")).Values.Cast<object>()
                .Select(entity => new { Entity = entity, Placement = (int)placementOf.Invoke(world.LoEntityIds, new[] { Get(entity, "Id") }) })
                .Where(pair => pair.Placement >= 0).ToDictionary(pair => pair.Placement, pair => pair.Entity);
            var before = Current();
            var regionPlacements = new HashSet<int>(((IEnumerable)((IDictionary)Get(world.Data.Placement, "RegionInfos"))[region])
                .Cast<object>().Select(row => (int)Get(row, "LoEntityId")));
            var states = before.ToDictionary(pair => pair.Key, pair =>
            {
                var grid = MapComponent(pair.Value, shared.CommonType("GridTransformComponent"));
                var stage = MapComponent(pair.Value, shared.CommonType("WorldStageComponent"));
                var position = grid == null ? null : Get(grid, "Pos");
                return new MainlineSharedEntityState((int)Get(pair.Value, "ClassId"), stage == null ? 0 : (int)Get(stage, "State"),
                    position == null ? 0 : (int)Get(position, "X"), position == null ? 0 : (int)Get(position, "Y"));
            });
            int prepares = 0, updates = 0, additions = 0, removals = 0, replacements = 0;
            var statusQuery = player.GetType().GetMethod("GetMapRegionStatus");
            int Status() => Convert.ToInt32(statusQuery.Invoke(player, new object[] { mapId, region }));
            var listeners = new List<Action>();
            try
            {
                listeners.Add(TrackPlayerRegionEvent(player, "OnMapStatusPrepareUpdate", args =>
                {
                    if ((int)args[0] != mapId || (int)args[1] != region || Status() != 1 || updates != 0)
                        throw new InvalidDataException("Original region preparation changed the status too early.");
                    prepares++;
                }));
                listeners.Add(TrackPlayerRegionEvent(player, "OnMapStatusUpdate", args =>
                {
                    if ((int)args[0] != mapId || (int)args[1] != region || Status() != 2 || prepares != 1)
                        throw new InvalidDataException("Original public-region event has the wrong ordering.");
                    updates++;
                }));
                using (var add = new NativeMap14SharedEventSubscription(Get(container, "AddEntityEvent"), shared.CommonType("IEcContainerAddEntityEventReceiver"), _ => additions++))
                using (var remove = new NativeMap14SharedEventSubscription(Get(container, "RemoveEntityEvent"), shared.CommonType("IEcContainerRemoveEntityEventReceiver"), _ => removals++))
                using (var replace = new NativeMap14SharedEventSubscription(Get(container, "ReplaceEntityEvent"), shared.CommonType("IEcContainerReplaceEntityEventReceiver"), _ => replacements++))
                {
                    bool rejected = false;
                    try { world.SynchronizeClientState(states, id => id == region ? -1 : regionStates[id]); }
                    catch (InvalidOperationException) { rejected = true; }
                    if (!rejected || prepares + updates + additions + removals + replacements != 0
                        || Current().Any(pair => !ReferenceEquals(pair.Value, before[pair.Key])))
                        throw new InvalidDataException("Invalid region snapshot mutated the original container.");
                    world.SynchronizeClientState(states, id => id == region ? 2 : regionStates[id]);
                    regionStates[region] = 2;
                    var after = Current();
                    foreach (var pair in before.Where(pair => !regionPlacements.Contains(pair.Key)))
                        if (!after.TryGetValue(pair.Key, out var unchanged) || !ReferenceEquals(unchanged, pair.Value))
                            throw new InvalidDataException("Region transition replaced another region's original entity.");
                    var rows = (IEnumerable)((IDictionary)Get(world.Data.Placement, "RegionInfos"))[region];
                    int supported = 0;
                    foreach (object row in rows)
                    {
                        int id = (int)Get(row, "LoEntityId");
                        var info = Get(MapComponent(world.Data.GetContainerPrefab(id), shared.CommonType("LoEntityComponent")), "Info");
                        var status = Enum.ToObject(shared.CommonType("MapRegionStatus"), 2);
                        bool expected = (bool)row.GetType().GetMethod("RegionStatusSupported").Invoke(row, new[] { status })
                            && (bool)info.GetType().GetMethod("RegionStatusSupported").Invoke(info, new[] { status })
                            && !(bool)info.GetType().GetMethod("BlockDefaultSpawnInMap").Invoke(info, new[] { status });
                        if (expected != after.ContainsKey(id)) throw new InvalidDataException("Public region retained the wrong original placements.");
                        if (!expected) continue;
                        supported++;
                        var entity = after[id];
                        var entityId = Get(entity, "Id");
                        if ((long)Get(entityId, "ContainerId") != publicId || !(bool)Get(entity, "Started")
                            || !(bool)Get(entity, "Awaked") || !ReferenceEquals(Get(entity, "Container"), container))
                            throw new InvalidDataException("Public region did not retain its original started container.");
                    }
                    int priorEvents = additions + removals + replacements;
                    world.SynchronizeClientState(states, id => regionStates[id]);
                    if (Status() != 2 || prepares != 1 || updates != 1 || priorEvents != additions + removals + replacements || replacements == 0)
                        throw new InvalidDataException("Region transition events repeated or skipped original in-place replacement.");
                    Debug.Log($"MAINLINE_LIVE_REGION_TRANSITION_PASS map={mapId} region={region} public={supported} add={additions} remove={removals} replace={replacements} duplicate=0");
                }
            }
            finally { foreach (var remove in listeners) remove(); }
        }

        private static Action TrackPlayerRegionEvent(object player, string name, Action<object[]> callback)
        {
            var info = player.GetType().GetEvent(name);
            var parameters = info.EventHandlerType.GetMethod("Invoke").GetParameters()
                .Select(value => Expression.Parameter(value.ParameterType, value.Name)).ToArray();
            var listener = Expression.Lambda(info.EventHandlerType,
                Expression.Invoke(Expression.Constant(callback), Expression.NewArrayInit(typeof(object),
                    parameters.Select(value => Expression.Convert(value, typeof(object))))), parameters).Compile();
            info.AddEventHandler(player, listener);
            return () => info.RemoveEventHandler(player, listener);
        }

        private static (object Clock, FieldInfo Field, object Previous)[] CaptureClientClock(NativeMap14SharedAssemblies shared)
        {
            var type = shared.CommonType("TimeManager");
            var clock = type.GetProperty("Instance").GetValue(null);
            return new[] { "TimeSynced", "TimeOffset", "PreTime", "TotalEnvTime", "LastEnvTime" }.Select(name =>
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return (clock, field, field.GetValue(clock));
            }).ToArray();
        }

        private static void VerifyClientClockRestored((object Clock, FieldInfo Field, object Previous)[] fields)
        {
            foreach (var state in fields)
                if (!Equals(state.Field.GetValue(state.Clock), state.Previous))
                    throw new InvalidDataException("Client teardown changed the previous clock state: " + state.Field.Name);
        }
    }
}
