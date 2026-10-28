using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Pos2d = Zjcs.Official.World.NativeTerrain.Common.Pos2d;
using Direction = Zjcs.Official.World.NativeTerrain.Common.Direction;

namespace Zjcs.Offline.World.NativeMap14SharedViews
{
    // Read/event adapter over an existing original entity. The host must create
    // and restore that entity; this adapter never supplies server state/rules.
    public sealed class NativeMap14SharedViewEntity : ECViewEntityData, IDisposable
    {
        private readonly NativeMap14SharedAssemblies assemblies;
        public object Original { get; }
        public GridData Grid { get; }
        public StageData Stage { get; }
        public override int ClassId => (int)Get(Original, "ClassId");
        public override EcEntityIdentifier Id
        {
            get { var id = Get(Original, "Id"); return new EcEntityIdentifier((long)Get(id, "ContainerId"), (int)Get(id, "EntityId")); }
        }

        public NativeMap14SharedViewEntity(NativeMap14SharedAssemblies assemblies, object entity)
        {
            this.assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
            Original = entity ?? throw new ArgumentNullException(nameof(entity));
            if (entity.GetType() != assemblies.CommonType("EcEntity")) throw new ArgumentException("Original EcEntity is required.");
            var grid = Component("GridTransformComponent");
            var stage = Component("WorldStageComponent");
            if (grid != null) Grid = new GridData(this, grid);
            if (stage != null) Stage = new StageData(this, stage);
        }

        public override T GetComponent<T>()
        {
            if (Grid is T grid) return grid;
            if (Stage is T stage) return stage;
            return default;
        }

        public void Dispose() => Grid?.Dispose();
        private object Component(string type) => Invoke(Original, "GetComponent", new[] { typeof(Type) }, assemblies.CommonType(type));
        private object Position(Pos2d pos) => Activator.CreateInstance(assemblies.CommonType("Pos2d"), pos.X, pos.Y);
        private static Pos2d Position(object pos) => new Pos2d((int)Get(pos, "X"), (int)Get(pos, "Y"));
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static object Invoke(object owner, string name, Type[] types, params object[] values) => owner.GetType().GetMethod(name, types).Invoke(owner, values);

        public sealed class GridData : ECGridTransformData, IDisposable
        {
            private readonly NativeMap14SharedViewEntity owner;
            public object Original { get; }
            private readonly List<KeyValuePair<IGridTransformPosUpdatedEventReceiver, IDisposable>> receivers =
                new List<KeyValuePair<IGridTransformPosUpdatedEventReceiver, IDisposable>>();
            public int ReceiverCount => receivers.Count;
            public override Pos2d Pos => Position(Get(Original, "Pos"));
            public override Direction Direction => (Direction)Convert.ToInt32(Get(Original, "Direction"));
            public override bool FlipX => (bool)Get(Original, "FlipX");
            public override ECGridTransformInfo Info { get; }
            internal GridData(NativeMap14SharedViewEntity owner, object original)
            { this.owner = owner; Original = original; Info = new GridInfo(Get(original, "Info")); }
            public override Vector3 GridToPosition(Pos2d grid) => (Vector3)Invoke(Original, "GridToPosition",
                new[] { owner.assemblies.CommonType("Pos2d") }, owner.Position(grid));
            public override Pos2d PositionToGrid(Vector3 pos) => Position(Invoke(Original, "PositionToGrid", new[] { typeof(Vector3) }, pos));
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver)
            {
                if (receiver == null) throw new ArgumentNullException(nameof(receiver));
                var subscription = new NativeMap14SharedEventSubscription(Get(Original, "PosUpdatedEvent"),
                    owner.assemblies.CommonType("IGridTransformPosUpdatedEventReceiver"), args =>
                    {
                        if (!ReferenceEquals(args[0], Original)) throw new InvalidOperationException("Original grid event owner differs.");
                        receiver.GridTransform_OnPosUpdated(this, Position(args[1]));
                    });
                receivers.Add(new KeyValuePair<IGridTransformPosUpdatedEventReceiver, IDisposable>(receiver, subscription));
            }
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver)
            {
                var index = receivers.FindIndex(pair => ReferenceEquals(pair.Key, receiver));
                if (index < 0) return;
                receivers[index].Value.Dispose();
                receivers.RemoveAt(index);
            }
            public void Dispose() { foreach (var receiver in receivers) receiver.Value.Dispose(); receivers.Clear(); }
        }

        private sealed class GridInfo : ECGridTransformInfo
        {
            private readonly object original;
            public GridInfo(object original) { this.original = original; }
            public override Vector2 RealCenter => (Vector2)Get(original, "RealCenter");
            public override List<Pos2d> GetBodyRange()
            {
                var result = new List<Pos2d>();
                foreach (var pos in (IEnumerable)NativeMap14SharedPrefabs.Call(original, "GetBodyRange")) result.Add(Position(pos));
                return result;
            }
        }

        public sealed class StageData : LegacyWorldStageComponentData, IWorldStageOperationState
        {
            private readonly NativeMap14SharedViewEntity owner;
            private readonly Dictionary<object, OfficialStageStateInfo> infos = new Dictionary<object, OfficialStageStateInfo>();
            public object Original { get; }
            public override int State => (int)Get(Original, "State");
            public override ECViewEntityData Entity => owner;
            public override LegacyWorldStageComponentData InteractionRedirectStage
            {
                get
                {
                    var redirect = Get(Original, "InteractionRedirectStage");
                    if (redirect == null) return null;
                    if (ReferenceEquals(redirect, Original)) return this;
                    throw new InvalidOperationException("Original redirect entity lookup is not bound.");
                }
            }
            internal StageData(NativeMap14SharedViewEntity owner, object original) { this.owner = owner; Original = original; }
            public bool IsActive() => (bool)NativeMap14SharedPrefabs.Call(Original, "IsActive");
            public override int GetStateComponentIndex(StageComponentType type, int subStateIndex = 0) => (int)Invoke(Original,
                "GetStateComponentIndex", new[] { owner.assemblies.CommonType("StageComponentType"), typeof(int) },
                Enum.ToObject(owner.assemblies.CommonType("StageComponentType"), (int)type), subStateIndex);
            public override OfficialStageStateInfo GetCurStateInfo()
            {
                var original = NativeMap14SharedPrefabs.Call(Original, "GetCurStateInfo");
                if (original == null) return null;
                if (infos.TryGetValue(original, out var cached)) return cached;
                var info = new OfficialStageStateInfo { EnterResults = Configs(Get(original, "EnterResults")), SubStateList = null };
                var substates = Get(original, "SubStateList") as IEnumerable;
                if (substates != null)
                {
                    info.SubStateList = new List<OfficialSubStageStateInfo>();
                    foreach (var substate in substates)
                    {
                        // Original client Select is null. The legacy display DTO
                        // has an int slot, which is not consumed as a shared rule.
                        if (Get(substate, "Select") != null) throw new InvalidOperationException("Original client Select representation differs.");
                        info.SubStateList.Add(new OfficialSubStageStateInfo { Conditions = Configs(Get(substate, "Conditions")),
                            Behaviour = Config(Get(substate, "Behaviour")), Results = Configs(Get(substate, "Results")) });
                    }
                }
                infos.Add(original, info);
                return info;
            }
            public IDisposable Subscribe(Action<EntityWorldStageUpdateEventMessage> callback)
            {
                if (callback == null) throw new ArgumentNullException(nameof(callback));
                return new NativeMap14SharedEventSubscription(Get(Original, "StateUpdateEvent"),
                    owner.assemblies.CommonType("IEcEntityWorldStageComponentStateUpdateEventReceiver"), args =>
                    {
                        if (!ReferenceEquals(args[0], Original)) throw new InvalidOperationException("Original stage event owner differs.");
                        callback(new EntityWorldStageUpdateEventMessage { StageComp = this, PreState = (int)args[1], Param = args[2] });
                    });
            }
            private static OfficialStageCompCfg Config(object value) => value == null ? null : new OfficialStageCompCfg
                { CompType = Convert.ToInt32(Get(value, "CompType")), CompIndex = (int)Get(value, "CompIndex") };
            private static List<OfficialStageCompCfg> Configs(object values)
            {
                if (values == null) return null;
                var result = new List<OfficialStageCompCfg>();
                foreach (var value in (IEnumerable)values) result.Add(Config(value));
                return result;
            }
        }
    }
}
