using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Confirmed fresh-save initialization. Original code still clones templates,
    // allocates identifiers, indexes Lo IDs and runs the container lifecycle.
    internal sealed class MainlineSharedEntityCreation
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14SharedData data;
        private readonly NativeMap14SharedRoot root;
        private readonly MainlineSharedMapConfiguration messages;
        public object LoEntityIds { get; }

        public MainlineSharedEntityCreation(NativeMap14SharedAssemblies shared,
            NativeMap14SharedData data, NativeMap14SharedRoot root, MainlineSharedMapConfiguration messages = null)
        {
            this.shared = shared;
            this.data = data;
            this.root = root;
            this.messages = messages;
            LoEntityIds = Activator.CreateInstance(shared.CommonType("MapLoEntityIdManager"));
        }

        public void Populate(Func<int, int> regionStatus, Func<int, long> statusContainerId = null)
        {
            foreach (DictionaryEntry region in (IDictionary)Get(data.Placement, "RegionInfos"))
            {
                int status = regionStatus((int)region.Key);
                foreach (var placement in (IEnumerable)region.Value)
                    Spawn(placement, status, statusContainerId?.Invoke(status));
            }
        }

        private void Spawn(object placement, int regionStatus, long? statusContainerId)
        {
            var entity = CreateSnapshot(placement, regionStatus, statusContainerId);
            if (entity == null) return;
            Register((int)Get(placement, "LoEntityId"), entity);
            if (!(bool)root.Container.GetType().GetMethod("AddEntity").Invoke(root.Container, new[] { entity }))
                throw new InvalidOperationException("Original fresh map entity registration failed.");
        }

        private object CreateSnapshot(object placement, int regionStatus, long? statusContainerId)
        {
            if (regionStatus < 0 || regionStatus > 2) throw new ArgumentOutOfRangeException(nameof(regionStatus));
            int placementId = (int)Get(placement, "LoEntityId");
            var status = Enum.ToObject(shared.CommonType("MapRegionStatus"), regionStatus);
            var source = data.GetContainerPrefab(placementId);
            var lo = Component(source, "LoEntityComponent");
            if (lo == null) throw new InvalidOperationException("Original placement spawn configuration is absent.");
            var info = Get(lo, "Info");
            if (!(bool)placement.GetType().GetMethod("RegionStatusSupported").Invoke(placement, new[] { status })
                || !(bool)info.GetType().GetMethod("RegionStatusSupported").Invoke(info, new[] { status })
                || (bool)info.GetType().GetMethod("BlockDefaultSpawnInMap").Invoke(info, new[] { status }))
                return null;

            var container = root.Container;
            int classId = (int)lo.GetType().GetMethod("GetDefSpawnEntityClassId").Invoke(lo, null);
            var entity = root.Instantiate(data.GetCommonPrefab(classId));
            // Register the complete initial state before the original container
            // performs its Awake-all, then Start-all passes.
            Copy(source, entity, "LoGridTransformComponent", "GridTransformComponent", "Pos", "MyPos");
            Copy(source, entity, "LoGridTransformComponent", "GridTransformComponent", "Dir", "Direction");
            Copy(source, entity, "LoGridTransformComponent", "GridTransformComponent", "FlipX", "FlipX");
            Copy(source, entity, "LoWorldStageComponent", "WorldStageComponent", "State", "State");
            Copy(source, entity, "LoWorldActiveableEntityComponent", "WorldActiveableEntityComponent", "Active", "Active");
            var camp = Component(source, "LoEntityCampComponent");
            var runtimeCamp = Component(entity, "EntityCampComponent");
            if (camp != null && runtimeCamp != null)
                NativeMap14SharedPrefabs.Set(runtimeCamp, "CampMonsterEntities",
                    new System.Collections.Generic.List<int>((System.Collections.Generic.List<int>)
                        Get(Get(camp, "Info"), "CampMonsterEntities")));

            int index = (int)container.GetType().GetMethod("NewEntityIndex").Invoke(container, null);
            var identifier = Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"),
                statusContainerId ?? Get(container, "Id"), index);
            NativeMap14SharedPrefabs.Set(entity, "Id", identifier);
            return entity;
        }

        // Original remove/add handlers select in-place replacement and retain their
        // own event ordering. The local host supplies only the next saved snapshot.
        public void TransitionRegion(int region, int status, long containerId,
            IReadOnlyDictionary<int, MainlineSharedEntityState> states, object player)
        {
            var regions = (IDictionary)Get(data.Placement, "RegionInfos");
            var targets = new List<KeyValuePair<int, object>>();
            foreach (object placement in (IEnumerable)regions[region])
            {
                int id = (int)Get(placement, "LoEntityId");
                var entity = CreateSnapshot(placement, status, containerId);
                if (entity == null) continue;
                if (states.TryGetValue(id, out var state)) ApplyState(entity, state, false);
                ApplyActivation(entity, id, player);
                targets.Add(new KeyValuePair<int, object>(id, entity));
            }
            var placementOf = LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId");
            var ids = new HashSet<int>(((IEnumerable)regions[region]).Cast<object>().Select(row => (int)Get(row, "LoEntityId")));
            var previous = ((IDictionary)Get(root.Container, "EntityDict")).Values.Cast<object>()
                .Where(entity => ids.Contains((int)placementOf.Invoke(LoEntityIds, new[] { Get(entity, "Id") }))).ToArray();
            var statusValue = Enum.ToObject(shared.CommonType("MapRegionStatus"), status);
            player.GetType().GetMethod("TryFireOnMapStatusPrepareUpdate")
                .Invoke(player, new object[] { data.MapClassId, region, statusValue });
            foreach (var entity in previous)
            {
                var message = Activator.CreateInstance(shared.CommonType("MapEntityRemoveMsg"));
                NativeMap14SharedPrefabs.Set(message, "ContainerId", Get(Get(entity, "Id"), "ContainerId"));
                NativeMap14SharedPrefabs.Set(message, "EntityId", Get(entity, "Id"));
                Message("MsgMapEntityRemove", message);
            }
            foreach (var target in targets)
            {
                var message = Activator.CreateInstance(shared.CommonType("MapEntityAddMsg"));
                NativeMap14SharedPrefabs.Set(message, "ContainerId", containerId);
                NativeMap14SharedPrefabs.Set(message, "Entity", target.Value);
                NativeMap14SharedPrefabs.Set(message, "LoEntityId", target.Key);
                Message("MsgMapEntityAdd", message);
            }
            var update = Activator.CreateInstance(shared.CommonType("MapRegionStatusUpdateMsg"));
            NativeMap14SharedPrefabs.Set(update, "MapClassId", data.MapClassId);
            NativeMap14SharedPrefabs.Set(update, "Region", region);
            NativeMap14SharedPrefabs.Set(update, "RegionStatus", statusValue);
            Message("MsgMapRegionStatusUpdate", update);
        }

        private void Message(string name, object value) => messages.Dispatch(name, value);

        private void Register(int placement, object entity) => LoEntityIds.GetType().GetMethod("SetLoEntId2EntId")
            .Invoke(LoEntityIds, new[] { (object)placement, Get(entity, "Id") });

        private void ApplyState(object entity, MainlineSharedEntityState state, bool started)
        {
            if ((int)Get(entity, "ClassId") != state.EntityClassId)
                throw new InvalidOperationException("Local and original placed entity classes differ.");
            var grid = Component(entity, "GridTransformComponent");
            if (grid != null)
            {
                var position = Get(grid, "MyPos");
                if ((int)Get(position, "X") != state.X || (int)Get(position, "Y") != state.Y)
                    NativeMap14SharedPrefabs.Set(grid, started ? "Pos" : "MyPos",
                        Activator.CreateInstance(shared.CommonType("Pos2d"), state.X, state.Y));
            }
            var stage = Component(entity, "WorldStageComponent");
            if (stage == null || (int)Get(stage, "State") == state.State) return;
            if (started)
                stage.GetType().GetMethod("UpdateState", new[] { typeof(int), shared.CommonType("MySerializable") })
                    .Invoke(stage, new object[] { state.State, null });
            else NativeMap14SharedPrefabs.Set(stage, "State", state.State);
        }

        public void Synchronize(IReadOnlyDictionary<int, MainlineSharedEntityState> states, bool started)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            var placementOf = LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId");
            foreach (object entity in ((IDictionary)Get(root.Container, "EntityDict")).Values.Cast<object>().ToArray())
            {
                int placement = (int)placementOf.Invoke(LoEntityIds, new[] { Get(entity, "Id") });
                if (placement < 0 || !states.TryGetValue(placement, out var state)) continue;
                ApplyState(entity, state, started);
            }
        }

        public void SynchronizeActivations(object player)
        {
            var placementOf = LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId");
            foreach (object entity in ((IDictionary)Get(root.Container, "EntityDict")).Values.Cast<object>().ToArray())
                ApplyActivation(entity, (int)placementOf.Invoke(LoEntityIds, new[] { Get(entity, "Id") }), player);
        }

        private void ApplyActivation(object entity, int placement, object player)
        {
            var maps = (IDictionary)Get(player, "ActiveableEntityDataDict");
            if (!maps.Contains(data.MapClassId)) return;
            var component = Component(entity, "WorldActiveableEntityComponent");
            if (component == null) return;
            var activated = (IDictionary)maps[data.MapClassId];
            var kind = Get(Get(component, "Info"), "ActiveableEntityType");
            // Records only add activations; preserve unrecorded original defaults.
            if (activated.Contains(kind) && ((List<int>)activated[kind]).Contains(placement))
                NativeMap14SharedPrefabs.Set(component, "Active", true);
        }

        public void Clear() => LoEntityIds.GetType().GetMethod("Clear").Invoke(LoEntityIds, null);

        private void Copy(object source, object entity, string sourceType, string targetType, string input, string output)
        {
            var from = Component(source, sourceType);
            var to = Component(entity, targetType);
            if (from != null && to != null)
                NativeMap14SharedPrefabs.Set(to, output, Get(Get(from, "Info"), input));
        }

        private object Component(object entity, string type) => entity.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
            .Invoke(entity, new object[] { shared.CommonType(type) });
        private static object Get(object value, string name) => NativeMap14SharedPrefabs.Get(value, name);
    }
}
