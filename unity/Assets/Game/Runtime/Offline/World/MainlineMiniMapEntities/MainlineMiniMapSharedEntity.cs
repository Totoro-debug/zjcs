using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.MainlineMiniMapEntities
{
    // Borrows the map host's original entity/view. Disposal releases only this
    // adapter's stage receiver; creation, state restoration and replacement belong
    // to the host. An in-place original replacement keeps this adapter; replacing
    // the original object requires a new adapter after component construction.
    public sealed class MainlineMiniMapSharedEntity : IMiniMapSpawnEntity, IDisposable
    {
        private readonly NativeMap14SharedViewEntity view;
        private readonly IMiniMapMarkDataQueries queries;
        private readonly Grid grid;
        private readonly Stage stage;
        private readonly Transport transport;
        private readonly Entry entry;
        private readonly WayPoint wayPoint;
        private readonly Source source;
        private readonly Mover mover;

        public MainlineMiniMapSharedEntity(NativeMap14SharedAssemblies shared,
            NativeMap14SharedViewEntity view, IMiniMapMarkDataQueries queries)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
            if (view.Original.GetType() != shared.CommonType("EcEntity"))
                throw new ArgumentException("The original entity and assembly scope differ.", nameof(view));
            object Component(string name) => view.Original.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                .Invoke(view.Original, new object[] { shared.CommonType(name) });
            if (view.Grid != null) grid = new Grid(this, view.Grid);
            var native = Component("StageResultTransportComponent");
            if (native != null) transport = new Transport(native);
            native = Component("WorldActiveableEntityComponent");
            if (native != null) entry = new Entry(this, native);
            if (Component("WayPointComponent") != null) wayPoint = new WayPoint(this);
            native = Component("EntitySourceComponent");
            if (native != null) source = new Source(this, native);
            if (Component("GridMoverComponent") != null) mover = new Mover();
            // Subscribe last so a failed constructor cannot leave a receiver.
            if (view.Stage != null) stage = new Stage(this, view.Stage);
        }

        public object Original => view.Original;
        public int ClassId => view.ClassId;
        public EcEntityIdentifier Id => view.Id;
        public int ContainerClassId
        {
            get { var container = Get(Original, "Container"); return container == null ? 0 : (int)Get(container, "ClassId"); }
        }
        public IMiniMapMarkGrid GridTransform => grid;
        public string GetEntityTag() => queries.GetEntityTag(ContainerClassId, Id.EntityId) ?? string.Empty;
        public T GetComponent<T>() where T : class
        {
            if (grid is T g) return g;
            if (stage is T s) return s;
            if (transport is T t) return t;
            if (entry is T e) return e;
            if (wayPoint is T w) return w;
            if (source is T o) return o;
            if (mover is T m) return m;
            return view.GetComponent<T>();
        }
        public void Dispose() => stage?.Dispose();
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static Pos2d Position(object original) => new Pos2d((int)Get(original, "X"), (int)Get(original, "Y"));

        private sealed class Grid : IMiniMapMarkGrid
        {
            private readonly NativeMap14SharedViewEntity.GridData native;
            private object originalInfo;
            private GridTransformComponentInfo info;
            public IMiniMapMarkEntity Entity { get; }
            internal Grid(MainlineMiniMapSharedEntity owner, NativeMap14SharedViewEntity.GridData native)
            { Entity = owner; this.native = native; }
            public Pos2d Pos => native.Pos;
            public Vector3 GridToPosition(Pos2d position) => native.GridToPosition(position);
            public List<Pos2d> GetBodyRange()
            {
                var body = new List<Pos2d>();
                foreach (var pos in (IEnumerable)NativeMap14SharedPrefabs.Call(Get(native.Original, "Info"), "GetBodyRange"))
                    body.Add(Position(pos));
                return body;
            }
            public GridTransformComponentInfo Info
            {
                get
                {
                    var original = Get(native.Original, "Info");
                    if (ReferenceEquals(original, originalInfo)) return info;
                    var projected = new GridTransformComponentInfo
                    {
                        RealCenter = (Vector2)Get(original, "RealCenter"),
                        UnlockWholeRequestCount = (int)Get(original, "UnlockWholeRequestCount"),
                        IsRigidBody = (bool)Get(original, "IsRigidBody"),
                        BodyRangeInfos = new List<GridTransformComponentBodyPosInfo>()
                    };
                    foreach (var row in (IEnumerable)Get(original, "BodyRangeInfos"))
                        projected.BodyRangeInfos.Add(new GridTransformComponentBodyPosInfo
                        {
                            Pos = Position(Get(row, "Pos")), GroundType = (GroundType)Convert.ToInt32(Get(row, "GroundType")),
                            IgnoreClick = (bool)Get(row, "IgnoreClick"), OverrideGroundType = (bool)Get(row, "OverrideGroundType")
                        });
                    var crosses = (IEnumerable)Get(original, "BodyCrossBlockedInfos");
                    if (crosses != null)
                    {
                        projected.BodyCrossBlockedInfos = new List<GridTransformComponentBodyCrossBlockedInfo>();
                        foreach (var row in crosses) projected.BodyCrossBlockedInfos.Add(new GridTransformComponentBodyCrossBlockedInfo
                        { PosOne = Position(Get(row, "PosOne")), PosTwo = Position(Get(row, "PosTwo")) });
                    }
                    projected.Init();
                    originalInfo = original; info = projected;
                    return info;
                }
            }
        }

        private sealed class Stage : IMiniMapManagedStage, IDisposable
        {
            private readonly NativeMap14SharedViewEntity.StageData native;
            private IDisposable subscription;
            public IMiniMapSpawnEntity Entity { get; }
            public EcEntityWorldStageComponentStateUpdateEventDelegate StateUpdateEvent { get; set; }
                = new EcEntityWorldStageComponentStateUpdateEventDelegate();
            internal Stage(MainlineMiniMapSharedEntity owner, NativeMap14SharedViewEntity.StageData native)
            {
                Entity = owner; this.native = native;
                subscription = native.Subscribe(message => StateUpdateEvent.FireEvent(this, message.PreState, message.Param));
            }
            public int State => native.State;
            public bool StateEqual(int state) => State == state;
            public bool IsActive() => native.IsActive();
            public void Dispose()
            { subscription?.Dispose(); subscription = null; StateUpdateEvent?.Clear(); }
        }

        private sealed class Transport : IMiniMapMarkStageTransport
        {
            private readonly object native;
            internal Transport(object native) { this.native = native; }
            public int TargetLoEntityId => (int)Get(native, "TargetLoEntityId");
        }
        private sealed class Entry : IMiniMapMarkWorldEntry
        {
            private readonly object native;
            public IMiniMapMarkEntity Entity { get; }
            public IMiniMapMarkGrid GridTransform => Entity.GridTransform;
            internal Entry(MainlineMiniMapSharedEntity owner, object native) { Entity = owner; this.native = native; }
            public bool Active => (bool)Get(native, "Active");
        }
        private sealed class WayPoint : IMiniMapMarkWayPoint
        {
            private readonly MainlineMiniMapSharedEntity owner;
            internal WayPoint(MainlineMiniMapSharedEntity owner) { this.owner = owner; }
            public IMiniMapMarkEntity Entity => owner;
            public bool IsUsable() => MiniMapMarkWayPointRules.IsEntityUsable(owner.queries, owner.GridTransform.Pos);
        }
        private sealed class Source : IMiniMapManagedEntitySource
        {
            private readonly object native;
            public IMiniMapSpawnEntity Entity { get; }
            internal Source(MainlineMiniMapSharedEntity owner, object native) { Entity = owner; this.native = native; }
            public EntityOwnershipData Ownership
            {
                get
                {
                    var ownership = Get(native, "Ownership");
                    return ownership == null ? null : new EntityOwnershipData
                    { Type = (EntityOwnershipType)Convert.ToInt32(Get(ownership, "Type")), OwnerId = (long)Get(ownership, "OwnerId") };
                }
            }
        }
        private sealed class Mover : IMiniMapManagedGridMover { }
    }
}
