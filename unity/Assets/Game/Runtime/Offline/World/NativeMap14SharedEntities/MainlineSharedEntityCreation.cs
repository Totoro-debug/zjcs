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
        public object LoEntityIds { get; }

        public MainlineSharedEntityCreation(NativeMap14SharedAssemblies shared,
            NativeMap14SharedData data, NativeMap14SharedRoot root)
        {
            this.shared = shared;
            this.data = data;
            this.root = root;
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
                return;

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
            // Make the original placement lookup available to Add/Start receivers.
            LoEntityIds.GetType().GetMethod("SetLoEntId2EntId").Invoke(LoEntityIds, new[] { (object)placementId, identifier });
            if (!(bool)container.GetType().GetMethod("AddEntity").Invoke(container, new[] { entity }))
                throw new InvalidOperationException("Original fresh map entity registration failed: " + placementId);
        }

        public void Synchronize(IReadOnlyDictionary<int, MainlineSharedEntityState> states, bool started)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            var placementOf = LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId");
            foreach (object entity in ((IDictionary)Get(root.Container, "EntityDict")).Values.Cast<object>().ToArray())
            {
                int placement = (int)placementOf.Invoke(LoEntityIds, new[] { Get(entity, "Id") });
                if (placement < 0 || !states.TryGetValue(placement, out var state)) continue;
                if ((int)Get(entity, "ClassId") != state.EntityClassId)
                    throw new InvalidOperationException("Local and original placed entity classes differ: " + placement);
                var grid = Component(entity, "GridTransformComponent");
                if (grid != null)
                {
                    var position = Get(grid, "MyPos");
                    if ((int)Get(position, "X") != state.X || (int)Get(position, "Y") != state.Y)
                        NativeMap14SharedPrefabs.Set(grid, started ? "Pos" : "MyPos",
                            Activator.CreateInstance(shared.CommonType("Pos2d"), state.X, state.Y));
                }
                var stage = Component(entity, "WorldStageComponent");
                if (stage == null || (int)Get(stage, "State") == state.State) continue;
                if (started)
                    stage.GetType().GetMethod("UpdateState", new[] { typeof(int), shared.CommonType("MySerializable") })
                        .Invoke(stage, new object[] { state.State, null });
                else NativeMap14SharedPrefabs.Set(stage, "State", state.State);
            }
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
