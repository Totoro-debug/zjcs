using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;

namespace Zjcs.Offline.World.NativeMap14ClientEntities
{
    public interface INativeMap14ClientMovement
    {
        bool HasMoveAgent(MyEntity entity);
        void AddMoveAgent(MyEntity entity);
    }

    // Identity/state/event adaptation only. Original Console methods still
    // instantiate, initialize, mutate and dispatch the shared entity state.
    public sealed class NativeMap14ClientEntityState : IClientEntityContainer, IClientEntityServices, IEntityLoadFacts, IDisposable
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14SharedRoot root;
        private readonly NativeMap14SharedData data;
        private readonly NativeMap14SharedConfiguration configuration;
        private readonly Func<bool> lowMemoryForbidden;
        private readonly INativeMap14ClientMovement movement;
        private readonly Action<NativeMap14SharedViewEntity> bindComponents;
        private readonly Dictionary<object, NativeMap14SharedViewEntity> views = new Dictionary<object, NativeMap14SharedViewEntity>(IdentityComparer.Instance);
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private bool disposed;
        public long MainPlayerId { get; }
        public long Id => (long)Get(root.Container, "Id");
        public int ClassId => (int)Get(root.Container, "ClassId");
        public bool Destroying => (bool)Get(root.Container, "Destroying");
        public bool IsMapForbidLowMemoryDeal => lowMemoryForbidden();
        public bool UseGameObjectPool => (bool)Get(configuration.Settings, "ECIsUseGameObjectPool");
        public ECViewEntityData RootEntity => Wrap(Get(root.Container, "RootEntity"));
        public int ActiveSubscriptions => subscriptions.Count;
        public IReadOnlyDictionary<EcEntityIdentifier, ECViewEntityData> EntityDict
        {
            get
            {
                RequireActive();
                var result = new Dictionary<EcEntityIdentifier, ECViewEntityData>();
                foreach (DictionaryEntry pair in (IDictionary)Get(root.Container, "EntityDict"))
                {
                    var view = Wrap(pair.Value); result.Add(view.Id, view);
                }
                return result;
            }
        }
        public NativeMap14ClientEntityState(NativeMap14SharedAssemblies shared, NativeMap14SharedRoot root,
            NativeMap14SharedData data, NativeMap14SharedConfiguration configuration, long mainPlayerId,
            Func<bool> lowMemoryForbidden, INativeMap14ClientMovement movement = null,
            Action<NativeMap14SharedViewEntity> bindComponents = null)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.lowMemoryForbidden = lowMemoryForbidden ?? throw new ArgumentNullException(nameof(lowMemoryForbidden));
            this.movement = movement;
            this.bindComponents = bindComponents;
            MainPlayerId = mainPlayerId;
        }
        public NativeMap14SharedViewEntity Wrap(object entity)
        {
            RequireActive();
            if (entity == null) return null;
            if (!views.TryGetValue(entity, out var view))
            {
                view = new NativeMap14SharedViewEntity(shared, entity); views.Add(entity, view);
                try { bindComponents?.Invoke(view); }
                catch { views.Remove(entity); view.Dispose(); throw; }
            }
            return view;
        }
        public ECViewEntityData GetCommonPrefab(int classId) => Wrap(data.GetCommonPrefab(classId));
        public ECViewEntityData Instantiate(ECViewEntityData prefab) => Wrap(root.Instantiate(Original(prefab)));
        public void SetContainer(ECViewEntityData entity, IClientEntityContainer container)
        {
            if (!ReferenceEquals(container, this)) throw new ArgumentException("Original shared container identity differs.", nameof(container));
            Set(Original(entity), "Container", root.Container);
        }
        public void SetIdentifier(ECViewEntityData entity, EcEntityIdentifier identifier) =>
            Set(Original(entity), "Id", Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), identifier.ContainerId, identifier.EntityId));
        public void Awake(ECViewEntityData entity) => Original(entity).GetType().GetMethod("Awake").Invoke(Original(entity), null);
        public bool IsRemoving(ECViewEntityData entity) => (bool)Get(Original(entity), "Removing");
        public bool HasGridGround(ECViewEntityData entity)
        {
            var grid = Component(entity, "GridTransformComponent");
            return grid != null && Get(grid, "GridGround") != null;
        }
        public IClientEntitySource Source(ECViewEntityData entity)
        {
            var component = Component(entity, "EntitySourceComponent");
            return component == null ? null : new SourceState(component);
        }
        public IClientEntityGrid Grid(ECViewEntityData entity)
        {
            var component = Component(entity, "GridTransformComponent");
            return component == null ? null : new GridState(shared, component);
        }
        public bool HasMoveAgent(MyEntity entity) => (movement ??
            throw new InvalidOperationException("Original client movement is not bound.")).HasMoveAgent(entity);
        public void AddMoveAgent(MyEntity entity) => (movement ??
            throw new InvalidOperationException("Original client movement is not bound.")).AddMoveAgent(entity);
        public IDisposable SubscribeStartEntity(Action<ECViewEntityData, bool> callback) =>
            Subscribe("StartEntity", args => callback(Wrap(args[0]), (bool)args[1]));
        public IDisposable SubscribeDestroyEntity(Action<ECViewEntityData, object> callback) =>
            Subscribe("DestroyEntity", args => callback(Wrap(args[0]), args[1]));
        public IDisposable SubscribePrepareReplaceEntity(Action<ECViewEntityData, ECViewEntityData> callback) =>
            Subscribe("PrepareReplaceEntity", args => callback(Wrap(args[0]), Wrap(args[1])));
        public IDisposable SubscribeReplaceEntity(Action<ECViewEntityData, EcEntityIdentifier> callback) =>
            Subscribe("ReplaceEntity", args => callback(Wrap(args[0]), Identifier(args[1])));
        private IDisposable Subscribe(string name, Action<object[]> callback)
        {
            RequireActive();
            var original = new NativeMap14SharedEventSubscription(Get(root.Container, name + "Event"),
                shared.CommonType("IEcContainer" + name + "EventReceiver"), callback);
            IDisposable lease = null;
            lease = new Lease(() => { original.Dispose(); subscriptions.Remove(lease); });
            subscriptions.Add(lease); return lease;
        }
        public void Dispose()
        {
            if (disposed) return;
            foreach (var subscription in subscriptions.ToArray()) subscription.Dispose();
            foreach (var view in views.Values) view.Dispose();
            views.Clear(); disposed = true;
        }
        private void RequireActive()
        { if (disposed) throw new ObjectDisposedException(nameof(NativeMap14ClientEntityState)); }
        private object Original(ECViewEntityData entity)
        {
            RequireActive();
            if (!(entity is NativeMap14SharedViewEntity view) ||
                !views.TryGetValue(view.Original, out var known) || !ReferenceEquals(known, view))
                throw new ArgumentException("The owned original shared entity is required.", nameof(entity));
            return view.Original;
        }
        private object Component(ECViewEntityData entity, string type)
        {
            var original = Original(entity);
            return original.GetType().GetMethod("GetComponent", new[] { typeof(Type) }).Invoke(original, new object[] { shared.CommonType(type) });
        }
        private static object Get(object target, string name) => NativeMap14SharedPrefabs.Get(target, name);
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name);
            if (field != null) field.SetValue(target, value);
            else (target.GetType().GetProperty(name) ?? throw new MissingMemberException(target.GetType().FullName, name)).SetValue(target, value);
        }
        private static EcEntityIdentifier Identifier(object value) =>
            new EcEntityIdentifier((long)Get(value, "ContainerId"), (int)Get(value, "EntityId"));
        private sealed class SourceState : IClientEntitySource
        {
            private readonly object original;
            public SourceState(object original) { this.original = original; }
            public EcEntitySourceType EntitySourceType
            {
                get => (EcEntitySourceType)Convert.ToInt32(Get(original, "EntitySourceType"));
                set => Set(original, "EntitySourceType", Enum.ToObject(original.GetType().GetProperty("EntitySourceType").PropertyType, (int)value));
            }
            public EntityOwnershipData Ownership
            {
                get
                {
                    var value = Get(original, "Ownership");
                    return value == null ? null : new EntityOwnershipData
                    { Type = (EntityOwnershipType)Convert.ToInt32(Get(value, "Type")), OwnerId = (long)Get(value, "OwnerId") };
                }
                set
                {
                    var property = original.GetType().GetProperty("Ownership");
                    object target = value == null ? null : Activator.CreateInstance(property.PropertyType);
                    if (target != null)
                    {
                        Set(target, "Type", Enum.ToObject(target.GetType().GetField("Type").FieldType, (int)value.Type));
                        Set(target, "OwnerId", value.OwnerId);
                    }
                    property.SetValue(original, target);
                }
            }
        }
        private sealed class GridState : IClientEntityGrid
        {
            private readonly NativeMap14SharedAssemblies shared; private readonly object original;
            public GridState(NativeMap14SharedAssemblies shared, object original) { this.shared = shared; this.original = original; }
            public Pos2d Pos
            {
                get { var value = Get(original, "Pos"); return new Pos2d((int)Get(value, "X"), (int)Get(value, "Y")); }
                set => Set(original, "Pos", Activator.CreateInstance(shared.CommonType("Pos2d"), value.X, value.Y));
            }
        }
        private sealed class Lease : IDisposable
        {
            private Action release; public Lease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public static readonly IdentityComparer Instance = new IdentityComparer();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
