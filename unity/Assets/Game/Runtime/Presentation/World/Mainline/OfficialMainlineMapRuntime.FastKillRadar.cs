using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14EntityAssets;
using Zjcs.Offline.World.NativeMap14EntityRadar;
using Zjcs.Offline.World.NativeMap14HeroStatues;
using Zjcs.Offline.World.NativeMap14RadarFastKill;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.EntityRadarCombined;
using Zjcs.Official.EntityRadarCost;
using Zjcs.Official.EntityRadarFastKill;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Pos2d = Zjcs.Official.World.NativeTerrain.Common.Pos2d;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private MainlineFastKillRadar fastKillRadar;
        internal bool IsFastKillRadarReady => fastKillRadar != null;

        // The local map keeps its existing input/rendering hosts. The original
        // radar consumes the same shared entities, body anchors and native tips.
        private sealed class MainlineFastKillRadar : IDisposable, IRadarFastKillServices,
            IRadarFastKillRoot, IRadarFastKillPosition, IRadarFastKillQueries, IRadarStageCostQueries
        {
            private readonly OfficialMainlineMapRuntime runtime;
            private readonly OfficialMainlineFastKillTipCatalog catalog;
            private readonly GameObject templates;
            private readonly MapControllerEntityRadarTipManager manager;
            private readonly MapControllerEntityRadarTipCombinate prefab;
            private readonly NativeMap14EntitySpriteLibrary sprites;
            private readonly bool ownsSprites;
            private readonly NativeMap14RadarWorldQueries world;
            private readonly NativeMap14RadarFastKillQueries queries;
            private readonly Func<Transform, IStoryPlayableInstanceFactory> previousFactory;
            private readonly Func<Transform, IStoryPlayableInstanceFactory> factory;
            private readonly Dictionary<object, NativeMap14SharedViewEntity> views = new Dictionary<object, NativeMap14SharedViewEntity>();
            private readonly Dictionary<EcEntityIdentifier, Owner> owners = new Dictionary<EcEntityIdentifier, Owner>();
            private readonly Dictionary<EcEntityIdentifier, Tip> tips = new Dictionary<EcEntityIdentifier, Tip>();
            private readonly List<Pos2d> offsets = new List<Pos2d>();
            private float refreshAt;
            private bool disposed;
            private sealed class Owner { public MyEntity Entity; public int Placement; public Transform Slot; }
            private sealed class Tip { public MapControllerEntityRadarTipBase View; public bool Hiding; }

            public MainlineFastKillRadar(OfficialMainlineMapRuntime runtime)
            {
                this.runtime = runtime;
                catalog = Resources.Load<OfficialMainlineFastKillTipCatalog>("World/Mainline/fast-kill-tip");
                if (!catalog || catalog.Manifest != "884_156692" || !catalog.Prefab || !catalog.FastKillPrefab)
                    throw new InvalidOperationException("Original mainline fast-kill tip catalog is absent.");
                if (!runtime.mainlineStoryPlayableScope)
                {
                    runtime.mainlineStoryPlayableScope = runtime.mapRoot.gameObject.AddComponent<Zjcs.Offline.World.MainlineStoryPlayables.MainlineStoryPlayableScope>();
                    runtime.mainlineStoryPlayableScope.Initialize();
                }
                templates = new GameObject("OriginalRadarTemplates");
                templates.transform.SetParent(runtime.mapRoot, false); templates.SetActive(false);
                var pool = new GameObject("OriginalRadarPool"); pool.transform.SetParent(runtime.mapRoot, false);
                manager = pool.AddComponent<MapControllerEntityRadarTipManager>();
                previousFactory = StoryPlayableInstantiation.Resolve;
                factory = scope => scope && (scope == manager.transform || scope.IsChildOf(manager.transform))
                    ? runtime.mainlineStoryPlayableScope : previousFactory?.Invoke(scope);
                StoryPlayableInstantiation.Resolve = factory;
                ownsSprites = runtime.roadblockSprites == null;
                sprites = runtime.roadblockSprites ?? new NativeMap14EntitySpriteLibrary(
                    Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog"));
                var fast = Instantiate(catalog.FastKillPrefab, templates.transform, false).GetComponent<MapControllerEntityRadarTipFastKill>();
                fast.name = catalog.FastKillPrefab.name; fast.Services = this;
                foreach (var binding in fast.GetComponentsInChildren<NativeMap14HeroStatueSprites>(true)) binding.Bind(sprites);
                prefab = Instantiate(catalog.Prefab, templates.transform, false).GetComponent<MapControllerEntityRadarTipCombinate>();
                prefab.name = catalog.Prefab.name;
                var field = typeof(MapControllerEntityRadarTipCombinate).GetField("mMapControllerEntityRadarTipPrefabs", BindingFlags.Instance | BindingFlags.NonPublic);
                var children = (List<MapControllerEntityRadarTipBase>)field.GetValue(prefab);
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is MapControllerEntityRadarTipFastKill) children[i] = fast;
                    else if (children[i] is MapControllerEntityRadarTipStageCost cost)
                    {
                        var local = Instantiate(cost, templates.transform, false); local.name = cost.name;
                        local.CostQueries = this; children[i] = local;
                    }
                    else throw new InvalidOperationException("Unbound original combined radar tip.");
                }
                var shared = runtime.mainlineSharedWorld;
                world = new NativeMap14RadarWorldQueries(shared.Assemblies, shared.Configuration,
                    shared.Root.Ground, () => shared.IsReady, Wrap);
                queries = new NativeMap14RadarFastKillQueries(shared.Assemblies,
                    Activator.CreateInstance(shared.Assemblies.CommonType("EntityHandler")));
                if (!TryReadFastStageSetting(runtime.progression.EquipmentCatalog.GameSettings,
                    "MapStageCostShowDistance", out int distance))
                    throw new InvalidOperationException("Original radar distance is absent.");
                // MapControllerEntityRadar.Init: scan the original nonzero
                // Manhattan offsets; FindGamePosData consumes exact body cells.
                for (int x = -distance; x <= distance; x++)
                    for (int y = -distance; y <= distance; y++)
                        if (Math.Abs(x) + Math.Abs(y) != 0 && Math.Abs(x) + Math.Abs(y) <= distance)
                            offsets.Add(new Pos2d(x, y));
            }

            private NativeMap14SharedViewEntity Wrap(object original)
            {
                if (views.TryGetValue(original, out var view)) return view;
                view = new NativeMap14SharedViewEntity(runtime.mainlineSharedWorld.Assemblies, original, Wrap);
                views.Add(original, view);
                NativeMap14RadarWorldQueries.BindBody(view);
                NativeMap14RadarFastKillQueries.BindStageFast(runtime.mainlineSharedWorld.Assemblies, view);
                return view;
            }

            private Owner GetOwner(NativeMap14SharedViewEntity view)
            {
                if (owners.TryGetValue(view.Id, out var owner)) return owner;
                int placement = (int)runtime.mainlineSharedWorld.LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId")
                    .Invoke(runtime.mainlineSharedWorld.LoEntityIds, new[] { NativeMap14SharedPrefabs.Get(view.Original, "Id") });
                var host = runtime.entityView.FindEntityObject(placement);
                if (!host) return null;
                var go = new GameObject("OriginalRadarEntity"); go.transform.SetParent(host.transform, false);
                var entity = go.AddComponent<MyEntity>(); entity.Entity = view;
                if (view.Grid != null) go.AddComponent<MyGridTransform>().Bind();
                owner = new Owner { Entity = entity, Placement = placement };
                owners.Add(view.Id, owner);
                var slot = catalog.Slots.SingleOrDefault(s => s.ModelName == view.ClassId.ToString(CultureInfo.InvariantCulture));
                if (slot != null && slot.Transforms.Length > 0)
                {
                    Transform parent = go.transform;
                    foreach (var node in slot.Transforms)
                    {
                        var transform = new GameObject("OriginalRadarEffectSlot").transform;
                        transform.SetParent(parent, false); transform.localPosition = node.Position;
                        transform.localRotation = node.Rotation; transform.localScale = node.Scale; parent = transform;
                    }
                    owner.Slot = parent;
                }
                return owner;
            }

            public void Refresh()
            {
                if (disposed || Time.unscaledTime < refreshAt) return;
                refreshAt = Time.unscaledTime + .1f;
                var position = new Pos2d(runtime.navigation.currentGrid.X, runtime.navigation.currentGrid.Y);
                var inRange = new Dictionary<EcEntityIdentifier, NativeMap14SharedViewEntity>();
                // MapControllerEntityRadar.RefreshRadar: union original entities
                // occupying the scanned cells, then apply enter/leave changes.
                foreach (var offset in offsets)
                {
                    var cell = world.FindGamePosData(position + offset);
                    if (cell == null || !cell.ExistEntitys()) continue;
                    foreach (NativeMap14SharedViewEntity view in cell.Entitys) inRange[view.Id] = view;
                }
                var eligible = new HashSet<EcEntityIdentifier>();
                foreach (var pair in inRange)
                {
                    var view = pair.Value;
                    if (view.Stage == null) continue;
                    var owner = GetOwner(view);
                    if (owner == null || !owner.Entity.gameObject.activeInHierarchy
                        || !runtime.mapEntities.TryGetValue(owner.Placement, out var entity)
                        || entity.TypeId < 101 || entity.TypeId > 104
                        || OfficialMainlineStaminaModule.IsStaminaPoint(entity)
                        || !owner.Slot || !prefab.CanShowTip(owner.Entity)) continue;
                    eligible.Add(pair.Key);
                    if (tips.TryGetValue(pair.Key, out var previous))
                    {
                        if (!previous.Hiding) continue;
                        previous.Hiding = false; previous.View.Init(owner.Entity); previous.View.Show();
                        continue;
                    }
                    var tip = manager.GetTip(owner.Slot, prefab); tip.Init(owner.Entity); tip.Show();
                    tips.Add(pair.Key, new Tip { View = tip });
                }
                foreach (var pair in tips.ToArray())
                {
                    if (eligible.Contains(pair.Key) || pair.Value.Hiding) continue;
                    pair.Value.Hiding = true;
                    var record = pair.Value;
                    bool immediate = !record.View.MyEntity || !record.View.MyEntity.gameObject.activeInHierarchy;
                    record.View.Hide(immediate, tip =>
                    {
                        if (disposed) return;
                        tips.Remove(pair.Key); manager.RecycleTip(tip);
                    });
                }
            }

            public IRadarFastKillRoot ResolveRoot(MyEntity entity) => this;
            public IRadarFastKillPosition ResolvePosition(MyEntity entity) => this;
            public IRadarFastKillQueries Queries => this;
            public int CurrentMapClassId => runtime.mapClassId;
            public RadarFastKillPlayer MainPlayer => new RadarFastKillPlayer { Level = runtime.progression.Player.Level,
                SubRank = runtime.progression.Player.SubRank,
                CombatRating = runtime.progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating };
            public bool TryGetCampOwnerEntity(ECViewEntityData entity, out RadarFastKillCampOwner owner) => queries.TryGetCampOwnerEntity(entity, out owner);
            public bool IsFastGame(int map, int region, int type, int level, int rank, long combat) =>
                map == runtime.mapClassId && runtime.TryResolveRegionGameFastType(type, region, level, rank, combat, out int fast) && fast != 0;
            public EcEntityIdentifier GetEntIdByLoEntId(int id)
            {
                var managerType = runtime.mainlineSharedWorld.Assemblies.Shared.GetType("MeManager", true);
                var manager = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
                var map = NativeMap14SharedPrefabs.Get(manager, "MapData");
                var original = map.GetType().GetMethod("GetEntIdByLoEntId", new[] { typeof(int) }).Invoke(map, new object[] { id });
                return new EcEntityIdentifier((long)NativeMap14SharedPrefabs.Get(original, "ContainerId"), (int)NativeMap14SharedPrefabs.Get(original, "EntityId"));
            }
            public MyEntity GetEntity(EcEntityIdentifier id)
            {
                if (owners.TryGetValue(id, out var owner)) return owner.Entity.gameObject.activeInHierarchy ? owner.Entity : null;
                foreach (object original in ((IDictionary)NativeMap14SharedPrefabs.Get(runtime.mainlineSharedWorld.Root.Container, "EntityDict")).Values)
                {
                    var view = Wrap(original);
                    if (view.Id != id) continue;
                    owner = GetOwner(view); return owner?.Entity.gameObject.activeInHierarchy == true ? owner.Entity : null;
                }
                return null;
            }
            public int QueryRegion(Pos2d position)
            {
                var shared = runtime.mainlineSharedWorld;
                return (int)shared.Root.AreaAgent.GetType().GetMethod("GridToRegion").Invoke(shared.Root.AreaAgent,
                    new[] { (object)runtime.mapClassId, Activator.CreateInstance(shared.Assemblies.CommonType("Pos2d"), position.X, position.Y) });
            }
            // Scope is confirmed ordinary monsters only, with no stamina cost.
            public Dictionary<int, long> GetStageCost(ECViewEntityData entity) => null;

            public void Dispose()
            {
                disposed = true;
                if (ReferenceEquals(StoryPlayableInstantiation.Resolve, factory)) StoryPlayableInstantiation.Resolve = previousFactory;
                foreach (var tip in tips.Values) tip.View.Hide(true);
                tips.Clear();
                if (manager) Destroy(manager.gameObject);
                if (templates) Destroy(templates);
                foreach (var owner in owners.Values) if (owner.Entity) Destroy(owner.Entity.gameObject);
                owners.Clear();
                foreach (var view in views.Values) view.Dispose(); views.Clear();
                if (ownsSprites) sprites.Dispose();
            }
        }
    }
}
