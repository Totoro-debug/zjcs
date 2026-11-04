using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMapEntities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class SharedWorldReceiver : IEcContainerAddEntityEventReceiver,
            IEcContainerRemoveEntityEventReceiver, IEcContainerReplaceEntityEventReceiver
        {
            internal Action<IMiniMapSpawnEntity> Add;
            internal Action<IMiniMapSpawnEntity, object> Remove;
            internal Action<IMiniMapSpawnEntity, EcEntityIdentifier> Replace;
            public void Container_OnEntityAdd(IMiniMapSpawnEntity entity) => Add(entity);
            public void Container_OnEntityRemove(IMiniMapSpawnEntity entity, object data) => Remove(entity, data);
            public void Container_OnEntityReplace(IMiniMapSpawnEntity entity, EcEntityIdentifier previous) => Replace(entity, previous);
        }

        private static void VerifySharedWorld(NativeMap14SharedAssemblies shared,
            NativeMap14SharedPrefabs prefabs, SharedEntityQueries queries)
        {
            object New(string name) => Activator.CreateInstance(shared.CommonType(name));
            object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
            void Set(object owner, string name, object value) => NativeMap14SharedPrefabs.Set(owner, name, value);
            object Pos(int x, int y) => Activator.CreateInstance(shared.CommonType("Pos2d"), x, y);
            object Id(long containerId, int id) => Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), containerId, id);
            object Call(object owner, string name, Type[] types, params object[] values)
                => owner.GetType().GetMethod(name, types).Invoke(owner, values);
            void Run(object owner, string name) => NativeMap14SharedPrefabs.Call(owner, name);
            int Receivers(object dispatcher)
            {
                for (var type = dispatcher.GetType(); type != null; type = type.BaseType)
                {
                    var field = type.GetField("mRecordEventReceivers", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) return ((ICollection)field.GetValue(dispatcher)).Count;
                }
                throw new InvalidOperationException("Original receiver storage differs.");
            }
            var container = New("EcContainer"); Set(container, "ClassId", 14); Set(container, "Id", 14L);
            var root = New("EcEntity"); Set(root, "Id", Id(14, 1));
            var ground = New("GridGroundComponent"); var groundInfo = New("GridGroundComponentInfo");
            Set(groundInfo, "Size", Pos(8, 8)); Run(groundInfo, "Init"); Set(ground, "_Info", groundInfo);
            var area = New("AreaAgentComponent"); var areaInfo = New("AreaAgentComponentInfo");
            // Explicit small native-host fixture. These 64 cells test event and
            // occupancy timing; they are not a substitute for any official map.
            for (int group = 0; group < 2; group++)
            {
                var info = New("AreaInfo"); var positions = (IList)Get(info, "PosList");
                for (int y = group * 4; y < group * 4 + 4; y++) for (int x = 0; x < 8; x++) positions.Add(Pos(x, y));
                ((IList)Get(areaInfo, "AreaInfos")).Add(info);
            }
            Run(areaInfo, "Init"); Set(area, "_Info", areaInfo);
            Call(root, "AddComponent", new[] { shared.CommonType("EcComponent") }, ground);
            Call(root, "AddComponent", new[] { shared.CommonType("EcComponent") }, area);
            container.GetType().GetField("mRootEntity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(container, root);
            Require((bool)Call(container, "AddEntity", new[] { shared.CommonType("EcEntity") }, root), "Original root addition");
            var views = new Dictionary<object, NativeMap14SharedViewEntity>();
            NativeMap14SharedViewEntity View(object entity)
            {
                if (!views.TryGetValue(entity, out var view)) views.Add(entity, view = new NativeMap14SharedViewEntity(shared, entity));
                return view;
            }
            object Make(int classId, int id, int x, int y, bool moving = false)
            {
                var entity = prefabs.CreateClientPrefab(classId); Set(entity, "Id", Id(14, id));
                var grid = Call(entity, "GetComponent", new[] { typeof(Type) }, shared.CommonType("GridTransformComponent"));
                Set(grid, "MyPos", Pos(x, y));
                if (moving)
                {
                    var mover = New("GridMoverComponent"); var info = New("GridMoverComponentInfo"); Run(info, "Init");
                    // Explicit non-pathfinding fixture: seed the cached mask so
                    // Awake need not load unrelated global movement tables. This
                    // value does not define any installed map's walking rules.
                    info.GetType().GetField("mMoveMask", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(info, 1);
                    Set(mover, "_Info", info); Call(entity, "AddComponent", new[] { shared.CommonType("EcComponent") }, mover);
                }
                return entity;
            }
            var first = Make(6570, 2, 1, 1);
            Require((bool)Call(container, "AddEntity", new[] { shared.CommonType("EcEntity") }, first), "Original entity before container startup");
            Run(container, "Start");
            var eventNames = new[] { "AddEntity", "RemoveEntity", "ReplaceEntity", "DestroyEntity" };
            var before = eventNames.ToDictionary(name => name, name => Receivers(Get(container, name + "Event")));
            MainlineMiniMapSharedWorld world = null;
            try
            {
                world = new MainlineMiniMapSharedWorld(shared, container, View, queries);
                Require(eventNames.All(name => Receivers(Get(container, name + "Event")) == before[name] + 1), "Four owned original lifecycle receivers");
                var firstId = new EcEntityIdentifier(14, 2);
                var firstData = world.GetEntity(firstId);
                Require(ReferenceEquals(firstData, world.GetAreaEntity(0)[firstId])
                    && world.GetPosData(new Pos2d(1, 1)).Entitys.Contains(firstData), "Existing native entity uses one adapter in container, area and ground");
                var areaCopy = world.GetAreaEntity(0); areaCopy.Clear();
                Require(world.GetAreaEntity(0).ContainsKey(firstId) && world.GetAreaEntity(99) == null
                    && world.GetEntity(new EcEntityIdentifier(0, 0)) == null, "Original absent branches and isolated area consumer dictionary");
                int adds = 0, removes = 0, replacements = 0;
                var receiver = new SharedWorldReceiver
                {
                    Add = entity =>
                    {
                        Require(ReferenceEquals(world.GetEntity(entity.Id), entity), "Original addition is visible in the container");
                        Require(!(world.GetPosData(entity.GridTransform.Pos)?.Entitys?.Contains(entity) ?? false), "Native cells are populated after the add notification");
                        if (entity.GetComponent<IMiniMapManagedGridMover>() == null)
                            Require(world.GetAreaEntity(1).ContainsKey(entity.Id), "Original area receiver precedes map mark receivers");
                        adds++;
                    },
                    Remove = (entity, data) =>
                    {
                        Require(world.GetEntity(entity.Id) == null && ReferenceEquals(data, first), "Original removal preserves its data and removes the container identity first");
                        Require(world.GetPosData(entity.GridTransform.Pos).Entitys.Contains(entity), "Native occupancy remains until original End");
                        removes++;
                    },
                    Replace = (entity, previous) =>
                    {
                        Require(previous == firstId && ReferenceEquals(entity, firstData)
                            && world.GetEntity(previous) == null && ReferenceEquals(world.GetEntity(entity.Id), firstData)
                            && ReferenceEquals(world.GetAreaEntity(1)[entity.Id], firstData), "Original in-place replacement keeps adapter and stage identity after area rekeying");
                        replacements++;
                    }
                };
                world.Events.AddEntityEvent += receiver; world.Events.RemoveEntityEvent += receiver; world.Events.ReplaceEntityEvent += receiver;
                var second = Make(6571, 3, 5, 5);
                Require((bool)Call(container, "AddEntity", new[] { shared.CommonType("EcEntity") }, second), "Actual original add operation");
                var secondData = world.GetEntity(new EcEntityIdentifier(14, 3));
                Require(adds == 1 && world.GetPosData(new Pos2d(5, 5)).Entitys.Contains(secondData), "Original entity Start registers native cells");
                var mover = Make(6572, 4, 6, 5, moving: true);
                Require((bool)Call(container, "AddEntity", new[] { shared.CommonType("EcEntity") }, mover), "Actual original moving entity add");
                var movingData = world.GetEntity(new EcEntityIdentifier(14, 4));
                Require(adds == 2 && !world.GetAreaEntity(1).ContainsKey(movingData.Id)
                    && world.GetPosData(new Pos2d(6, 5)).Entitys.Contains(movingData), "Original area agent excludes grid movers while ground retains them");
                var previousCell = world.GetPosData(new Pos2d(1, 1));
                Set(View(first).Grid.Original, "Pos", Pos(2, 5));
                Require(!previousCell.ExistEntitys() && world.GetPosData(new Pos2d(2, 5)).Entitys.Contains(firstData)
                    && world.GetAreaEntity(0).ContainsKey(firstId), "Original movement changes ground cells without inventing an area update");
                var stage = firstData.GetComponent<IMiniMapManagedStage>();
                var stageEvents = new SharedStageReceiver { Expected = stage, Previous = stage.State };
                stage.StateUpdateEvent += stageEvents;
                var target = prefabs.CreateClientPrefab(6570); Set(target, "Id", Id(141, 2));
                var targetStage = Call(target, "GetComponent", new[] { typeof(Type) }, shared.CommonType("WorldStageComponent")); Set(targetStage, "State", 2);
                Call(container, "ReplaceEntity", new[] { shared.CommonType("EcEntity"), shared.CommonType("EcEntity") }, first, target);
                Require(replacements == 1 && stageEvents.Count == 1 && stage.State == 2, "Original replacement stage event stays attached");
                stageEvents.Previous = 2;
                Call(View(first).Stage.Original, "UpdateState", new[] { typeof(int), shared.CommonType("MySerializable") }, 1, null);
                Require(stageEvents.Count == 2 && ReferenceEquals(stage, firstData.GetComponent<IMiniMapManagedStage>()), "Existing mark stage receiver survives the replacement");
                Call(container, "RemoveEntityImmediate", new[] { shared.CommonType("EcEntity"), shared.CommonType("MySerializable") }, second, first);
                Require(removes == 1 && !(world.GetPosData(new Pos2d(5, 5)).Entitys?.Contains(secondData) ?? false)
                    && Receivers(Get(View(second).Stage.Original, "StateUpdateEvent")) == 0, "Original End clears cells and Destroy releases the removed adapter stage");
                Call(container, "RemoveEntityImmediate", new[] { shared.CommonType("EcEntity"), shared.CommonType("MySerializable") }, second, first);
                Require(removes == 1, "Repeated native removal does not emit an extra map event");
                world.Events.AddEntityEvent -= receiver; world.Events.RemoveEntityEvent -= receiver; world.Events.ReplaceEntityEvent -= receiver;
                using (View(first).Stage.Subscribe(_ => { }))
                {
                    world.Dispose(); world.Dispose();
                    Require(eventNames.All(name => Receivers(Get(container, name + "Event")) == before[name])
                        && Receivers(Get(View(first).Stage.Original, "StateUpdateEvent")) == 1
                        && !(bool)Get(first, "Ended") && (bool)Get(container, "Started"), "Scope teardown retains the original map and borrowed receiver");
                }
                bool rejected = false; try { world.GetEntity(firstData.Id); } catch (ObjectDisposedException) { rejected = true; }
                Require(rejected, "Disposed native world scope cannot create new bindings");
            }
            finally
            {
                world?.Dispose();
                // The original bulk helper also consults global player settings.
                // This explicit fixture has no global configuration: remove its
                // actual entities individually, retaining the root until last.
                foreach (var entity in ((IDictionary)Get(container, "EntityDict")).Values.Cast<object>()
                    .OrderBy(entity => ReferenceEquals(entity, root) ? 1 : 0).ToArray())
                    Call(container, "RemoveEntityImmediate", new[] { shared.CommonType("EcEntity"), shared.CommonType("MySerializable") }, entity, null);
                foreach (var view in views.Values) view.Dispose();
            }
            Debug.Log("MAINLINE_MINIMAP_SHARED_WORLD_PASS: actual Common container Start/Add/Replace/Remove/Destroy; native area and body occupancy timing; mover exclusion; movement; retained stage listeners; borrowed map lifetime. Explicit 64-cell event fixture, not an installed map rendering test.");
        }
    }
}
