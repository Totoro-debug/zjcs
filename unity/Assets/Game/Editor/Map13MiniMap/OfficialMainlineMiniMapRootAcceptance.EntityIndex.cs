using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class IndexedEntity : IMiniMapSpawnEntity, IMiniMapMarkGrid
        {
            internal Pos2d Position;
            internal List<Pos2d> Body = new List<Pos2d> { Pos2d.Zero, new Pos2d(1, 0) };
            internal MainlineMiniMapWorldData World;
            public object Original => this;
            public int ClassId { get; set; }
            public int ContainerClassId { get; set; }
            public EcEntityIdentifier Id { get; set; }
            public IMiniMapMarkGrid GridTransform => this;
            public T GetComponent<T>() where T : class => null;
            public string GetEntityTag() => World.GetEntityTag(ContainerClassId, Id.EntityId);
            public IMiniMapMarkEntity Entity => this;
            public GridTransformComponentInfo Info => null;
            public Pos2d Pos => Position;
            public List<Pos2d> GetBodyRange() => Body;
            public Vector3 GridToPosition(Pos2d position)
            { World.TryGetMapLiteData(ContainerClassId, out var lite); return lite.GridToPos(position); }
        }

        private sealed class IndexEvents : IEcContainerAddEntityEventReceiver, IEcContainerRemoveEntityEventReceiver,
            IEcContainerReplaceEntityEventReceiver
        {
            internal MainlineMiniMapEntityIndex Index;
            internal int Added, Removed, Replaced;
            internal object ExpectedData;
            public void Container_OnEntityAdd(IMiniMapSpawnEntity entity)
            {
                Require(ReferenceEquals(Index.GetEntity(entity.Id), entity)
                    && Index.GetPosData(entity.GridTransform.Pos).Entitys.Contains(entity), "Add publishes the indexed entity");
                Added++;
            }
            public void Container_OnEntityRemove(IMiniMapSpawnEntity entity, object data)
            {
                Require(Index.GetEntity(entity.Id) == null && ReferenceEquals(data, ExpectedData), "Remove publishes after removal and preserves data");
                Removed++;
            }
            public void Container_OnEntityReplace(IMiniMapSpawnEntity entity, EcEntityIdentifier previous)
            {
                Require(ReferenceEquals(Index.GetEntity(entity.Id), entity)
                    && (previous == entity.Id || Index.GetEntity(previous) == null), "Replace publishes the new identity");
                Replaced++;
            }
        }

        private static void VerifyEntityIndex(MainlineMiniMapWorldData world, IEnumerable<OfficialMapDefinition> definitions)
        {
            int count = 0;
            foreach (var definition in definitions)
            {
                int map = definition.ClassId;
                var index = new MainlineMiniMapEntityIndex(world, map);
                var events = new IndexEvents { Index = index };
                index.Events.AddEntityEvent += (IEcContainerAddEntityEventReceiver)events;
                index.Events.RemoveEntityEvent += (IEcContainerRemoveEntityEventReceiver)events;
                index.Events.ReplaceEntityEvent += (IEcContainerReplaceEntityEventReceiver)events;
                var entities = definition.Entities.Select(row => new IndexedEntity
                {
                    World = world, ClassId = row.EntityId, ContainerClassId = map,
                    Id = new EcEntityIdentifier(map, row.Id), Position = new Pos2d(row.Grid[0], row.Grid[1])
                }).ToArray();
                foreach (var entity in entities) index.Add(entity);
                Require(events.Added == entities.Length, "All six maps use original add delegates");
                foreach (var entity in entities)
                {
                    int area = world.GridToArea(map, entity.Pos);
                    Require(ReferenceEquals(index.GetEntity(entity.Id), entity)
                        && index.GetAreaEntity(area)[entity.Id] == entity
                        && index.GetPosData(entity.Pos).Entitys.Contains(entity), "Placement identity shared by area, ground and direct queries");
                    var areaCopy = index.GetAreaEntity(area); areaCopy.Clear();
                    Require(index.GetAreaEntity(area).ContainsKey(entity.Id), "Area consumers cannot erase the live index");
                    count++;
                }
                var moving = entities.First(); var old = moving.Pos;
                int oldArea = world.GridToArea(map, old);
                var destination = entities.First(value => world.GridToArea(map, value.Pos) != oldArea).Pos;
                moving.Position = destination;
                index.RefreshPosition(moving.Id);
                Require(!(index.GetAreaEntity(oldArea)?.ContainsKey(moving.Id) ?? false)
                    && index.GetAreaEntity(world.GridToArea(map, destination))[moving.Id] == moving
                    && !(index.GetPosData(old)?.Entitys.Contains(moving) ?? false)
                    && index.GetPosData(destination).Entitys.Contains(moving)
                    && events.Added == entities.Length && events.Removed == 0 && events.Replaced == 0,
                    "Cross-area movement refreshes body cells without changing entity lifecycle");
                moving.Position = new Pos2d(-1, -1);
                bool rejected = false;
                try { index.RefreshPosition(moving.Id); } catch (System.IO.InvalidDataException) { rejected = true; }
                Require(rejected && index.GetPosData(destination).Entitys.Contains(moving), "Invalid move retains the previous index");
                moving.Position = destination;
                var replacement = new IndexedEntity { World = world, ClassId = moving.ClassId,
                    ContainerClassId = map, Id = new EcEntityIdentifier(map + 1000, moving.Id.EntityId), Position = old };
                index.Replace(moving.Id, replacement);
                Require(events.Replaced == 1 && index.GetEntity(moving.Id) == null
                    && index.GetPosData(old).Entitys.Contains(replacement), "Replacement updates container identity and occupied cells");
                events.ExpectedData = new object();
                Require(index.Remove(replacement.Id, events.ExpectedData) && !index.Remove(replacement.Id)
                    && events.Removed == 1, "Repeated removal does not publish twice");
                foreach (var entity in entities.Skip(1)) index.Remove(entity.Id, events.ExpectedData);
                Require(entities.All(entity => index.GetEntity(entity.Id) == null
                    && !(index.GetPosData(entity.Pos)?.ExistEntitys() ?? false)), "No stale occupied cells after map teardown");
                index.Events.AddEntityEvent -= (IEcContainerAddEntityEventReceiver)events;
                index.Events.RemoveEntityEvent -= (IEcContainerRemoveEntityEventReceiver)events;
                index.Events.ReplaceEntityEvent -= (IEcContainerReplaceEntityEventReceiver)events;
            }
            Require(count == 11148, "Complete six-map live index placement coverage");
            Debug.Log("MAINLINE_MINIMAP_LIVE_INDEX_PASS: 11148 placements; shared identity, body overlap, cross-area movement, replacement, removal and original event ordering.");
        }
    }
}
