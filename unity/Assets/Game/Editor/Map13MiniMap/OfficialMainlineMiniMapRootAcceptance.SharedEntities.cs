using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.MainlineMiniMapEntities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class SharedEntityQueries : IMiniMapMarkDataQueries
        {
            internal MainlineMiniMapWorldData World;
            internal MapRegionStatus Status;
            internal int LastTagMap, LastTagPlacement;
            public bool HasMainPlayer { get; set; } = true;
            public bool HasMainMapContainer { get; set; } = true;
            public int MainMapClassId => 14;
            public int CurrentMeMapId => 14;
            public int GridToArea(int map, Pos2d position) => World.GridToArea(map, position);
            public string GetAreaTag(int map, int area) => World.GetAreaTag(map, area);
            public MiniMapMarkMapInfo GetMapInfo(int map) => World.GetMapInfo(map);
            public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map, int id) => World.GetContainerEntityInfo(map, id);
            public string GetEntityTag(int map, int id)
            { LastTagMap = map; LastTagPlacement = id; return World.GetEntityTag(map, id); }
            public int GridToRegion(int map, Pos2d position) => World.GridToRegion(map, position);
            public int QueryRegion(Pos2d position) => GridToRegion(14, position);
            public MapRegionStatus GetMapRegionStatus(int map, int region) => Status;
            public bool RegionIsActived(int map, int region) => Status == MapRegionStatus.Public;
            public Vector3 MapGridToPos(int map, Pos2d position)
            { World.TryGetMapLiteData(map, out var lite); return lite.GridToPos(position); }
            public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map, int id) => World.GetMapTransportTargetInfo(map, id);
            public EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entity, int group, int map, string area, string tag) => new EntityMapUIDisplayInfo();
            public MiniMapMarkExtendInfo GetEntityExtend(int entity) => new MiniMapMarkExtendInfo { CompleteState = 2 };
            public IMiniMapMarkEntity GetCommonPrefab(int entity) => throw new NotSupportedException();
            public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity) => throw new NotSupportedException();
            public void DoEnterMap(int map, int id) => throw new NotSupportedException();
        }
        private sealed class SharedStageReceiver : IEcEntityWorldStageComponentStateUpdateEventReceiver
        {
            internal IMiniMapManagedStage Expected;
            internal object Param;
            internal int Previous, Count;
            public void WorldStageComponent_OnStateUpdate(IMiniMapManagedStage stage, int previous, object param)
            {
                Require(ReferenceEquals(stage, Expected) && previous == Previous && ReferenceEquals(param, Param),
                    "Original stage owner, previous state and parameter identity");
                Count++;
            }
        }
        private static void VerifySharedEntities(MainlineMiniMapWorldData world)
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            Require(catalog, "Existing original shared assembly catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            using (var utilities = new NativeMap14SharedEntityUtilities(shared))
            {
                object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
                void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
                object New(string name) => Activator.CreateInstance(shared.CommonType(name));
                object Call(object owner, string name, Type[] types, params object[] values)
                    => owner.GetType().GetMethod(name, types).Invoke(owner, values);
                int ReceiverCount(object original)
                {
                    for (var type = original.GetType(); type != null; type = type.BaseType)
                    {
                        var field = type.GetField("mRecordEventReceivers", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null) return ((ICollection)field.GetValue(original)).Count;
                    }
                    throw new InvalidOperationException("Original receiver storage differs.");
                }
                var prefabs = new NativeMap14SharedPrefabs(shared, catalog);
                var queries = new SharedEntityQueries { World = world };
                var placements = New("MapLoEntityIdManager");
                object OriginalId(EcEntityIdentifier id)
                    => Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), id.ContainerId, id.EntityId);
                int PlacementId(EcEntityIdentifier id) => (int)Call(placements, "GetLoEntIdByEntId",
                    new[] { shared.CommonType("EcEntityIdentifier") }, OriginalId(id));
                void Bind(int placementId, EcEntityIdentifier id) => Call(placements, "SetLoEntId2EntId",
                    new[] { typeof(int), shared.CommonType("EcEntityIdentifier") }, placementId, OriginalId(id));
                var tagged = world.GetMapRegionDict(14).Values.SelectMany(region => world.GetEntityByMapRegion(14, region.Region))
                    .First(row => !string.IsNullOrEmpty(world.GetEntityTag(14, row.LoEntityId)));
                foreach (int classId in new[] { 6570, 6571, 6572, 6573 })
                {
                    var placement = world.GetMapRegionDict(14).Values.SelectMany(region => world.GetEntityByMapRegion(14, region.Region))
                        .Single(row => row.DefSpawnEntityInfo.ClassId == classId);
                    var entity = prefabs.CreateClientPrefab(classId);
                    var container = New("EcContainer"); Set(container, "ClassId", 14);
                    Set(entity, "Container", container);
                    var runtimeId = new EcEntityIdentifier(14L, 100000 + placement.LoEntityId);
                    Set(entity, "Id", OriginalId(runtimeId));
                    Bind(placement.LoEntityId, runtimeId);
                    object Add(string name)
                    {
                        // GetComponent caches absence; construct the complete
                        // fixture before the first original component lookup.
                        var existing = ((IEnumerable)Get(entity, "ComponentList")).Cast<object>()
                            .FirstOrDefault(shared.CommonType(name).IsInstanceOfType);
                        if (existing != null) return existing;
                        var component = New(name);
                        Call(entity, "AddComponent", new[] { shared.CommonType("EcComponent") }, component);
                        return component;
                    }
                    // Explicit component fixtures use original Common classes;
                    // they do not assert that these components belong to a hero.
                    var transport = Add("StageResultTransportComponent");
                    var entry = Add("WorldActiveableEntityComponent");
                    Add("WayPointComponent"); var source = Add("EntitySourceComponent");
                    Add("GridMoverComponent");
                    using (var view = new NativeMap14SharedViewEntity(shared, entity))
                    {
                        var grid = view.Grid.Original;
                        var placed = placement.PosInfo.Pos;
                        Set(grid, "MyPos", Activator.CreateInstance(shared.CommonType("Pos2d"), placed.X, placed.Y));
                        // The isolated host has no global map parser. Supply only
                        // the original ground cache used by coordinate conversion.
                        var ground = New("GridGroundComponent"); var groundInfo = New("GridGroundComponentInfo");
                        Set(groundInfo, "Size", Activator.CreateInstance(shared.CommonType("Pos2d"), 280, 280));
                        Set(groundInfo, "OriginPos", new Vector3(-139.5f, -139.5f, 0));
                        NativeMap14SharedPrefabs.Call(groundInfo, "Init"); Set(ground, "_Info", groundInfo);
                        grid.GetType().GetField("mGridGround", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, ground);
                        var originalStage = view.Stage.Original;
                        int before = ReceiverCount(Get(originalStage, "StateUpdateEvent"));
                        var borrowed = view.Stage.Subscribe(_ => { });
                        var adapter = new MainlineMiniMapSharedEntity(shared, view, queries, PlacementId);
                        try
                        {
                            Require(ReferenceEquals(adapter.Original, entity) && adapter.ClassId == classId
                                && adapter.ContainerClassId == 14 && adapter.Id.EntityId == runtimeId.EntityId
                                && adapter.Id.EntityId != placement.LoEntityId
                                && adapter.GetEntityTag() == (world.GetEntityTag(14, placement.LoEntityId) ?? string.Empty)
                                && queries.LastTagMap == 14 && queries.LastTagPlacement == placement.LoEntityId,
                                "Original runtime identity resolves its distinct placement for logic tags");
                            // An unmapped runtime ID deliberately collides with an
                            // actual tagged placement. Raw-ID fallback would leak its tag.
                            var collisionId = new EcEntityIdentifier(14L, tagged.LoEntityId);
                            Set(entity, "Id", OriginalId(collisionId));
                            Require(adapter.GetEntityTag() == string.Empty && queries.LastTagPlacement == -1,
                                "Missing original placement mapping keeps -1 without a raw runtime-ID fallback");
                            Bind(tagged.LoEntityId, collisionId);
                            Require(adapter.GetEntityTag() == world.GetEntityTag(14, tagged.LoEntityId)
                                && queries.LastTagPlacement == tagged.LoEntityId,
                                "Existing adapter reads the latest original placement mapping");
                            Set(entity, "Id", OriginalId(runtimeId));
                            var body = adapter.GridTransform;
                            Require(body.GetBodyRange().SequenceEqual(view.Grid.Info.GetBodyRange())
                                && body.Info.GetBodyRange().SequenceEqual(body.GetBodyRange())
                                && body.Info.RealCenter == view.Grid.Info.RealCenter && body.GetBodyRange().Count == 2,
                                "Actual two-cell hero body and real center");
                            var marker = new MiniMapEntityMark(14, queries.QueryRegion(body.Pos), adapter, queries);
                            Vector3 expected = body.GridToPosition(body.Pos) - (Vector3)body.Info.RealCenter
                                + new Vector3((float)body.GetBodyRange().Average(p => p.X), (float)body.GetBodyRange().Average(p => p.Y));
                            Require((marker.LocalPosition - expected).sqrMagnitude < .000001f, "Original marker consumes the native body's centroid");
                            var stage = adapter.GetComponent<IMiniMapManagedStage>();
                            Require(ReferenceEquals(stage, adapter.GetComponent<IMiniMapMarkStage>())
                                && ReferenceEquals(stage.Entity, adapter), "One stage projection serves original marker and manager");
                            var receiver = new SharedStageReceiver { Expected = stage, Param = New("EntityOwnershipData") };
                            stage.StateUpdateEvent += receiver;
                            foreach (int state in new[] { 2, 1, -1, 0, 3, 0 })
                            {
                                receiver.Previous = stage.State;
                                Call(originalStage, "UpdateState", new[] { typeof(int), shared.CommonType("MySerializable") }, state, receiver.Param);
                                Require(stage.StateEqual(state) && marker.EntityActive == (state == 2)
                                    && marker.InteractPossible() == (state >= 0 && state < 3), "Actual native stage validity differs from completion");
                            }
                            Require(receiver.Count == 6, "One original state event per update");
                            var gate = New("WorldStageActiveComponent");
                            Call(originalStage, "RegisterWorldStageActive", new[] { shared.CommonType("IEntityWorldStageActive") }, gate);
                            Require(!stage.IsActive() && !marker.InteractPossible(), "Registered original active checker can reject a valid state");
                            Set(gate, "Actived", true); Require(stage.IsActive(), "Actual active checker state is read live");
                            Call(originalStage, "UnregisterWorldStageActive", new[] { shared.CommonType("IEntityWorldStageActive") }, gate);
                            var target = adapter.GetComponent<IMiniMapMarkStageTransport>();
                            Set(transport, "TargetLoEntityId", 4504); Require(target.TargetLoEntityId == 4504, "Actual transport target");
                            Set(transport, "TargetLoEntityId", -1); Require(target.TargetLoEntityId == -1, "Nonpositive transport target is preserved");
                            var entryData = adapter.GetComponent<IMiniMapMarkWorldEntry>();
                            Set(entry, "Active", true); Require(entryData.Active && ReferenceEquals(entryData.GridTransform, body), "Actual world entry state and grid");
                            Set(entry, "Active", false); Require(!entryData.Active, "World entry changes remain live");
                            var waypoint = adapter.GetComponent<IMiniMapMarkWayPoint>();
                            foreach (MapRegionStatus status in Enum.GetValues(typeof(MapRegionStatus)))
                            { queries.Status = status; Require(waypoint.IsUsable() == (status == MapRegionStatus.Public), "Original waypoint public-region rule"); }
                            queries.HasMainPlayer = false; Require(!waypoint.IsUsable(), "Waypoint requires a player"); queries.HasMainPlayer = true;
                            queries.HasMainMapContainer = false; Require(!waypoint.IsUsable(), "Waypoint requires the main container"); queries.HasMainMapContainer = true;
                            var sourceData = adapter.GetComponent<IMiniMapManagedEntitySource>();
                            Require(adapter.GetComponent<IMiniMapManagedGridMover>() != null, "Actual grid mover presence");
                            Set(source, "Ownership", null); Require(sourceData.Ownership == null, "Original absent ownership");
                            var ownership = New("EntityOwnershipData"); Set(ownership, "Type", Enum.ToObject(shared.CommonType("EntityOwnershipType"), 1));
                            Set(ownership, "OwnerId", 9876543210L); Set(source, "Ownership", ownership);
                            Require(sourceData.Ownership.Type == EntityOwnershipType.Player && sourceData.Ownership.OwnerId == 9876543210L,
                                "Original ownership type and 64-bit identity");
                            var index = new MainlineMiniMapEntityIndex(world, 14); index.Add(adapter);
                            Require(ReferenceEquals(index.GetEntity(adapter.Id), adapter)
                                && body.GetBodyRange().All(offset => index.GetPosData(body.Pos + offset).Entitys.Contains(adapter)), "Actual native body feeds the shared occupancy index");
                            index.Remove(adapter.Id);
                        }
                        finally { adapter.Dispose(); adapter.Dispose(); }
                        Require(ReceiverCount(Get(originalStage, "StateUpdateEvent")) == before + 1, "Adapter releases only its own original receiver");
                        borrowed.Dispose(); Require(ReceiverCount(Get(originalStage, "StateUpdateEvent")) == before, "Borrowed original view remains usable");
                    }
                    Call(placements, "Clear", Type.EmptyTypes);
                }
                using (var empty = new NativeMap14SharedViewEntity(shared, New("EcEntity")))
                using (var adapter = new MainlineMiniMapSharedEntity(shared, empty, queries, PlacementId))
                    Require(adapter.GridTransform == null && adapter.GetComponent<IMiniMapManagedStage>() == null
                        && adapter.GetComponent<IMiniMapMarkWorldEntry>() == null && adapter.GetComponent<IMiniMapManagedGridMover>() == null,
                        "Absent original components are not fabricated");
                VerifySharedWorld(shared, prefabs, queries);
            }
            Debug.Log("MAINLINE_MINIMAP_SHARED_ENTITY_PASS: four actual two-cell hero prefabs; distinct runtime/placement IDs, original missing/live mapping and tagged-ID collision; original stage activity/checkers/events, centroid, transport, entry, waypoint, ownership, occupancy and owned receiver teardown. Additional component and tag fixtures do not assert hero configuration.");
        }
    }
}
