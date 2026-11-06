using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.MainlineMiniMapEntities
{
    // Borrows an initialized original map root/container. Its own area agent and
    // ground retain membership, occupancy and lifecycle timing. The view lookup
    // belongs to the host; this scope owns only adapters and event receivers.
    public sealed class MainlineMiniMapSharedWorld : Zjcs.Offline.World.MainlineMiniMap.IMainlineMiniMapEntities, IDisposable
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly object container, area, ground;
        private readonly Func<object, NativeMap14SharedViewEntity> viewLookup;
        private readonly IMiniMapMarkDataQueries queries;
        private readonly Func<EcEntityIdentifier, int> placementLookup;
        private readonly Dictionary<object, MainlineMiniMapSharedEntity> entities
            = new Dictionary<object, MainlineMiniMapSharedEntity>(IdentityComparer.Instance);
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private NativeMap14SharedViewCache ownedViews;
        private bool disposed;
        public MiniMapManagedEvents Events { get; } = new MiniMapManagedEvents();

        // The map owns its original root, player, LoID mapping and lifetime.
        // This entry point owns only the minimap's views and event receivers.
        public static MainlineMiniMapSharedWorld Create(MainlineSharedWorld world, IMiniMapMarkDataQueries queries)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (queries == null) throw new ArgumentNullException(nameof(queries));
            if (!world.IsReady || world.LoEntityIds == null)
                throw new InvalidOperationException("Start the original client world before binding its minimap.");
            var views = new NativeMap14SharedViewCache(world.Assemblies);
            try
            {
                var mapping = world.LoEntityIds;
                var lookup = mapping.GetType().GetMethod("GetLoEntIdByEntId")
                    ?? throw new MissingMethodException(mapping.GetType().FullName, "GetLoEntIdByEntId");
                var identifier = world.Assemblies.CommonType("EcEntityIdentifier");
                var result = new MainlineMiniMapSharedWorld(world.Assemblies, world.Root.Container,
                    views.Wrap, queries, id => (int)lookup.Invoke(mapping,
                        new[] { Activator.CreateInstance(identifier, id.ContainerId, id.EntityId) }));
                result.ownedViews = views;
                return result;
            }
            catch { views.Dispose(); throw; }
        }

        public MainlineMiniMapSharedWorld(NativeMap14SharedAssemblies shared, object container,
            Func<object, NativeMap14SharedViewEntity> viewLookup, IMiniMapMarkDataQueries queries,
            Func<EcEntityIdentifier, int> placementLookup)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.viewLookup = viewLookup ?? throw new ArgumentNullException(nameof(viewLookup));
            this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
            this.placementLookup = placementLookup ?? throw new ArgumentNullException(nameof(placementLookup));
            if (!shared.CommonType("EcContainer").IsInstanceOfType(container))
                throw new ArgumentException("The original map container is required.", nameof(container));
            if (!(bool)Get(container, "Started"))
                throw new InvalidOperationException("Start the original map container before binding its minimap.");
            var root = Get(container, "RootEntity") ?? throw new InvalidOperationException("The original map root is not installed.");
            object Component(string name) => Call(root, "GetComponent", new[] { typeof(Type) }, shared.CommonType(name));
            area = Component("AreaAgentComponent") ?? throw new InvalidOperationException("The original map area agent is not installed.");
            ground = Component("GridGroundComponent") ?? throw new InvalidOperationException("The original map ground is not installed.");
            try
            {
                Subscribe("AddEntity", args => Events.AddEntityEvent.FireEvent(Wrap(args[0])));
                Subscribe("RemoveEntity", args => Events.RemoveEntityEvent.FireEvent(Wrap(args[0]), args[1]));
                // Removal is published before original End/Destroy. Keep the
                // same adapter while the entity still occupies its native cells.
                Subscribe("DestroyEntity", args =>
                { if (entities.Remove(args[0], out var entity)) entity.Dispose(); });
                // Original ReplaceEntity mutates the source entity in place.
                // Keep its adapter/stage identity for existing marks/listeners.
                Subscribe("ReplaceEntity", args => Events.ReplaceEntityEvent.FireEvent(Wrap(args[0]), Identifier(args[1])));
            }
            catch { Dispose(); throw; }
        }

        public IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id)
        {
            RequireActive();
            var original = Call(container, "GetEntity", new[] { shared.CommonType("EcEntityIdentifier") },
                Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), id.ContainerId, id.EntityId));
            return original == null ? null : Wrap(original);
        }
        public Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity> GetAreaEntity(int id)
        {
            RequireActive();
            var originals = (IDictionary)Call(area, "GetAreaEntity", new[] { typeof(int) }, id);
            if (originals == null) return null;
            var result = new Dictionary<EcEntityIdentifier, IMiniMapSpawnEntity>();
            foreach (DictionaryEntry pair in originals) result.Add(Identifier(pair.Key), Wrap(pair.Value));
            return result;
        }
        public int GridToRegion(int map, Pos2d position)
        { RequireActive(); return queries.GridToRegion(map, position); }
        public IMiniMapMarkCell GetPosData(Pos2d position)
        {
            RequireActive();
            var original = Call(ground, "GetPosData", new[] { shared.CommonType("Pos2d") },
                Activator.CreateInstance(shared.CommonType("Pos2d"), position.X, position.Y));
            return original == null ? null : new Cell(this, original);
        }
        private MainlineMiniMapSharedEntity Wrap(object original)
        {
            RequireActive();
            if (entities.TryGetValue(original, out var entity)) return entity;
            var view = viewLookup(original);
            if (view == null || !ReferenceEquals(view.Original, original))
                throw new InvalidOperationException("The host returned a different original entity view.");
            entity = new MainlineMiniMapSharedEntity(shared, view, queries, placementLookup);
            entities.Add(original, entity);
            return entity;
        }
        private void Subscribe(string name, Action<object[]> callback) => subscriptions.Add(new NativeMap14SharedEventSubscription(
            Get(container, name + "Event"), shared.CommonType("IEcContainer" + name + "EventReceiver"), callback));
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var subscription in subscriptions) subscription.Dispose();
            subscriptions.Clear();
            foreach (var entity in entities.Values) entity.Dispose();
            entities.Clear();
            ownedViews?.Dispose();
            ownedViews = null;
        }
        private void RequireActive()
        { if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMapSharedWorld)); }
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static object Call(object owner, string name, Type[] types, params object[] values)
            => owner.GetType().GetMethod(name, types).Invoke(owner, values);
        private static EcEntityIdentifier Identifier(object value)
            => new EcEntityIdentifier((long)Get(value, "ContainerId"), (int)Get(value, "EntityId"));
        private sealed class Cell : IMiniMapMarkCell
        {
            private readonly MainlineMiniMapSharedWorld owner;
            private readonly object original;
            internal Cell(MainlineMiniMapSharedWorld owner, object original) { this.owner = owner; this.original = original; }
            public bool ExistEntitys()
            { owner.RequireActive(); return (bool)NativeMap14SharedPrefabs.Call(original, "ExistEntitys"); }
            public IEnumerable<IMiniMapSpawnEntity> Entitys
            {
                get
                {
                    owner.RequireActive();
                    return ((IEnumerable)Get(original, "Entitys"))?.Cast<object>().Select(owner.Wrap).ToArray();
                }
            }
        }
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            internal static readonly IdentityComparer Instance = new IdentityComparer();
            public new bool Equals(object first, object second) => ReferenceEquals(first, second);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
