using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMapEntities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private sealed class CacheProbe : IDisposable
        {
            internal int Disposals;
            public void Dispose() { Disposals++; }
        }

        private static void VerifyLiveViewCache(MainlineSharedWorld world)
        {
            var original = world.ClientCharacter;
            int binds = 0;
            bool fail = true;
            var probes = new List<CacheProbe>();
            NativeMap14SharedViewCache cache = null;
            using (cache = new NativeMap14SharedViewCache(world.Assemblies, view =>
            {
                binds++;
                if (!ReferenceEquals(cache.Wrap(original), view))
                    throw new InvalidDataException("Recursive view binding did not use the same cache.");
                var probe = new CacheProbe(); probes.Add(probe); view.BindAdditionalComponent(probe);
                if (fail) throw new InvalidOperationException("Explicit binding failure fixture");
            }))
            {
                bool failed = false;
                try { cache.Wrap(original); } catch (InvalidOperationException) { failed = true; }
                if (!failed || probes.Count != 1 || probes[0].Disposals != 1)
                    throw new InvalidDataException("Failed view binding retained its owned projection.");
                fail = false;
                var view = cache.Wrap(original);
                using (var foreign = new NativeMap14SharedViewEntity(world.Assemblies, original))
                    if (binds != 2 || !ReferenceEquals(view, cache.Wrap(original)) || !cache.Owns(view)
                        || cache.Owns(foreign) || cache.Wrap(null) != null)
                        throw new InvalidDataException("Shared view cache identity or ownership differs.");
            }
            cache.Dispose();
            bool rejected = false;
            try { cache.Wrap(original); } catch (ObjectDisposedException) { rejected = true; }
            if (!rejected || probes.Any(probe => probe.Disposals != 1)
                || !(bool)NativeMap14SharedPrefabs.Get(original, "Started"))
                throw new InvalidDataException("View cache teardown retained projections or ended its borrowed original entity.");
        }

        private static int[] LiveMiniMapReceiverCounts(MainlineSharedWorld world)
            => new[] { "AddEntity", "RemoveEntity", "ReplaceEntity", "DestroyEntity" }.Select(name =>
            {
                var dispatcher = NativeMap14SharedPrefabs.Get(world.Root.Container, name + "Event");
                for (var type = dispatcher.GetType(); type != null; type = type.BaseType)
                {
                    var field = type.GetField("mRecordEventReceivers", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) return ((ICollection)field.GetValue(dispatcher)).Count;
                }
                throw new InvalidDataException("Original receiver storage was not found.");
            }).ToArray();

        private static Dictionary<object, IMiniMapSpawnEntity> VerifyLiveMiniMap(MainlineSharedWorld world,
            MainlineMiniMapSharedWorld minimap, Dictionary<object, IMiniMapSpawnEntity> previous)
        {
            object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
            var result = new Dictionary<object, IMiniMapSpawnEntity>();
            var lookup = world.LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId");
            var tags = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("MapLogicEntityTagInfoParser"));
            foreach (object original in ((IDictionary)Get(world.Root.Container, "EntityDict")).Values)
            {
                var id = Get(original, "Id");
                var key = new EcEntityIdentifier((long)Get(id, "ContainerId"), (int)Get(id, "EntityId"));
                var entity = minimap.GetEntity(key);
                if (entity == null || !ReferenceEquals(entity.Original, original)
                    || !ReferenceEquals(entity, minimap.GetEntity(key))
                    || previous != null && previous.TryGetValue(original, out var retained) && !ReferenceEquals(retained, entity))
                    throw new InvalidDataException("Live minimap lost the original container or stable adapter identity.");
                int placement = (int)lookup.Invoke(world.LoEntityIds, new[] { id });
                string expected = (string)tags.GetType().GetMethod("GetEntityTag").Invoke(tags,
                    new object[] { world.Data.MapClassId, placement });
                if (entity.GetEntityTag() != (expected ?? string.Empty))
                    throw new InvalidDataException("Live minimap used a runtime identifier instead of the latest original placement mapping.");
                var stage = MapComponent(original, world.Assemblies.CommonType("WorldStageComponent"));
                if (stage != null && entity.GetComponent<IMiniMapManagedStage>()?.State != (int)Get(stage, "State"))
                    throw new InvalidDataException("Live minimap did not read the latest original stage.");
                result.Add(original, entity);
            }
            Debug.Log($"MAINLINE_LIVE_MINIMAP_ENTITIES_PASS map={world.Data.MapClassId} entities={result.Count} after_transition={previous != null}");
            return result;
        }

        // This fixture exercises entity/tag state only. Unused UI and command
        // ports fail explicitly; no replacement interaction rules are supplied.
        private sealed class WorldTagQueries : IMiniMapMarkDataQueries
        {
            private readonly object tags;
            internal WorldTagQueries(MainlineSharedWorld world)
            { tags = world.Configuration.Manager.GetParser(world.Assemblies.CommonType("MapLogicEntityTagInfoParser")); }
            public string GetEntityTag(int map, int placement) => (string)tags.GetType().GetMethod("GetEntityTag")
                .Invoke(tags, new object[] { map, placement });
            public int GridToArea(int map, Pos2d position) => throw new NotSupportedException();
            public string GetAreaTag(int map, int area) => throw new NotSupportedException();
            public MiniMapMarkMapInfo GetMapInfo(int map) => throw new NotSupportedException();
            public EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entity, int group, int map, string area, string tag) => throw new NotSupportedException();
            public MiniMapMarkExtendInfo GetEntityExtend(int entity) => throw new NotSupportedException();
            public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map, int placement) => throw new NotSupportedException();
            public IMiniMapMarkEntity GetCommonPrefab(int entity) => throw new NotSupportedException();
            public Vector3 MapGridToPos(int map, Pos2d position) => throw new NotSupportedException();
            public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map, int placement) => throw new NotSupportedException();
            public bool RegionIsActived(int map, int region) => throw new NotSupportedException();
            public int QueryRegion(Pos2d position) => throw new NotSupportedException();
            public int CurrentMeMapId => throw new NotSupportedException();
            public bool HasMainPlayer => throw new NotSupportedException();
            public bool HasMainMapContainer => throw new NotSupportedException();
            public int MainMapClassId => throw new NotSupportedException();
            public int GridToRegion(int map, Pos2d position) => throw new NotSupportedException();
            public MapRegionStatus GetMapRegionStatus(int map, int region) => throw new NotSupportedException();
            public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity) => throw new NotSupportedException();
            public void DoEnterMap(int map, int placement) => throw new NotSupportedException();
        }
    }
}
