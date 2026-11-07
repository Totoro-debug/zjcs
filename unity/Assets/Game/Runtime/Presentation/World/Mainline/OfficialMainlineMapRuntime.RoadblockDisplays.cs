using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14EntityAssets;
using Zjcs.Offline.World.NativeMap14EntityLifecycle;
using Zjcs.Offline.World.NativeMap14HeroStatues;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Pos2d = Zjcs.Official.World.NativeTerrain.Common.Pos2d;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private RoadblockDisplayServices roadblockDisplayServices;
        private NativeMap14EntitySpriteLibrary roadblockSprites;
        private readonly Dictionary<int, RoadblockDisplay> roadblockDisplays = new Dictionary<int, RoadblockDisplay>();
        private sealed class RoadblockDisplay
        {
            public NativeMap14SharedViewEntity Data;
            public NativeMyEntityBinding Binding;
            public GameObject View;
        }

        private void InitializeRoadblockDisplays()
        {
            if (roadblockDisplayServices != null) throw new InvalidOperationException("Roadblock displays are already bound.");
            if (!mainlineStoryPlayableScope)
            {
                mainlineStoryPlayableScope = mapRoot.gameObject.AddComponent<Zjcs.Offline.World.MainlineStoryPlayables.MainlineStoryPlayableScope>();
                mainlineStoryPlayableScope.Initialize();
            }
            roadblockSprites = new NativeMap14EntitySpriteLibrary(Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog"));
            roadblockDisplayServices = new RoadblockDisplayServices(this);
            roadblockDisplayServices.Bind();
            RefreshRoadblockDisplays();
        }

        private void RefreshRoadblockDisplays()
        {
            if (roadblockDisplayServices == null) return;
            var catalog = Resources.Load<OfficialRoadblockDisplayCatalog>("Map14/Native/roadblock-display-catalog");
            if (!catalog || catalog.manifest != "884_156692" || catalog.entries?.Length != 5)
                throw new InvalidOperationException("Verified roadblock native display catalog is absent.");
            var originals = new Dictionary<int, object>();
            foreach (object original in ((IDictionary)NativeMap14SharedPrefabs.Get(mainlineSharedWorld.Root.Container, "EntityDict")).Values)
            {
                int classId = (int)NativeMap14SharedPrefabs.Get(original, "ClassId");
                if (!catalog.entries.Any(e => e.classId == classId)) continue;
                int placement = (int)mainlineSharedWorld.LoEntityIds.GetType().GetMethod("GetLoEntIdByEntId")
                    .Invoke(mainlineSharedWorld.LoEntityIds, new[] { NativeMap14SharedPrefabs.Get(original, "Id") });
                if (OfficialMainlineRoadblockRules.Find(placement) != null) originals.Add(placement, original);
            }
            foreach (var existing in roadblockDisplays.ToArray())
                if (!originals.TryGetValue(existing.Key, out var value) || !ReferenceEquals(existing.Value.Data.Original, value))
                { ReleaseRoadblockDisplay(existing.Value); roadblockDisplays.Remove(existing.Key); }
            foreach (var pair in originals)
            {
                if (roadblockDisplays.ContainsKey(pair.Key)) continue;
                var host = entityView.FindEntityObject(pair.Key);
                if (!host) throw new InvalidOperationException("Roadblock map placement has no input host.");
                bool active = host.activeSelf;
                host.SetActive(false);
                var container = new GameObject("OriginalRoadblockEntity");
                container.transform.SetParent(host.transform, false);
                container.SetActive(false);
                var owner = container.AddComponent<Zjcs.Official.EntityLifecycle.MyEntity>();
                var binding = container.AddComponent<NativeMyEntityBinding>();
                var data = new NativeMap14SharedViewEntity(mainlineSharedWorld.Assemblies, pair.Value);
                var entry = catalog.entries.Single(e => e.classId == data.ClassId);
                if (!entry.prefab) throw new InvalidOperationException("Roadblock display prefab is unbound.");
                var view = Instantiate(entry.prefab, container.transform, false);
                foreach (var sprites in view.GetComponentsInChildren<NativeMap14HeroStatueSprites>(true)) sprites.Bind(roadblockSprites);
                foreach (var driver in view.GetComponentsInChildren<ECViewDriver>(true))
                {
                    driver.ServiceProvider = roadblockDisplayServices;
                    // Map activity/fog belongs to the existing placement host.
                    driver.ClientVisibilitySupporterFactory = new ConstantClientVisibilitySupporterFactory();
                }
                foreach (var linker in view.GetComponentsInChildren<MapEntityTipDotNodeLinker>(true))
                { linker.Controller = roadblockDisplayServices; linker.TipDotSystem = roadblockDisplayServices; }
                var display = new RoadblockDisplay { Data = data, Binding = binding, View = container };
                roadblockDisplays.Add(pair.Key, display);
                host.SetActive(true); // original Awake/OnEnable precede Bind/Start.
                container.SetActive(true);
                owner.Bind(null, data, false);
                owner.MyStart();
                host.SetActive(active);
            }
            roadblockDisplayServices.Refresh();
        }

        private static void ReleaseRoadblockDisplay(RoadblockDisplay display)
        {
            display.Binding.Owner.End(null);
            display.Binding.Owner.Remove(null);
            display.Binding.Owner.OnGameObjectPoolReset();
            display.View.SetActive(false);
            UnityEngine.Object.Destroy(display.View);
            display.Data.Dispose();
        }

        private void DisposeRoadblockDisplays()
        {
            foreach (var display in roadblockDisplays.Values) ReleaseRoadblockDisplay(display);
            roadblockDisplays.Clear();
            roadblockDisplayServices?.Dispose(); roadblockDisplayServices = null;
            roadblockSprites?.Dispose(); roadblockSprites = null;
        }

        private sealed class RoadblockDisplayServices : IServiceProvider, IECViewInjection,
            IECViewMapRootLifetime, IEcEntityBufferEventHandleModule, IEntityTipDotQueries,
            IProfessionTipDotSystem, IProfessionTipDotSystemController, IProfessionSoundHost, IDisposable
        {
            private readonly OfficialMainlineMapRuntime runtime;
            private IEntityTipDotQueries previous;
            private IProfessionSoundHost previousSound;
            private uint nextPlayingId;
            private readonly Dictionary<TipDotNodeKey, Action<TipDotStateInfo>> nodes = new Dictionary<TipDotNodeKey, Action<TipDotStateInfo>>();
            private readonly Dictionary<TipDotNodeKey, RoadblockTipGraphRuntime> graphs = new Dictionary<TipDotNodeKey, RoadblockTipGraphRuntime>();
            public RoadblockDisplayServices(OfficialMainlineMapRuntime runtime) { this.runtime = runtime; }
            public void Bind() { previous = EntityTipDotServices.Queries; EntityTipDotServices.Queries = this; previousSound = OfficialWwiseEvent.Host; OfficialWwiseEvent.Host = this; }
            public void Dispose() { if (ReferenceEquals(EntityTipDotServices.Queries, this)) EntityTipDotServices.Queries = previous; if (ReferenceEquals(OfficialWwiseEvent.Host, this)) OfficialWwiseEvent.Host = previousSound; foreach (var graph in graphs.Values) graph.Dispose(); graphs.Clear(); nodes.Clear(); }
            public uint PostEvent(uint eventId, GameObject emitter)
            {
                if (eventId != 3132674326u) return previousSound?.PostEvent(eventId, emitter)
                    ?? throw new InvalidOperationException("Unbound map sound event: " + eventId);
                var clip = Resources.Load<OfficialRoadblockDisplayCatalog>("Map14/Native/roadblock-display-catalog").activationSound;
                if (!clip) throw new InvalidOperationException("Original gate activation sound is absent.");
                Map14HandInUiAudio.Post(clip, emitter); return ++nextPlayingId;
            }
            public bool Destroying => false;
            public object GetService(Type type) => type == typeof(IECViewMapRootLifetime) ? this
                : throw new InvalidOperationException("Unbound original roadblock service: " + type);
            public void Inject(object value)
            {
                if (value is ECWorldStageView stage) stage.EcEntityBufferEventHandleModule = this;
                if (value is ECPlayableSupporterView playable)
                { playable.MyPlayableManager = runtime.mainlineStoryPlayableScope.Manager; playable.ServiceProvider = this; }
            }
            public IDisposable Subscribe<T>(ECViewEntityData entity, Action<T> callback) where T : IEcEntityBufferEventContext =>
                ((NativeMap14SharedViewEntity)entity).Stage.Subscribe((Action<EntityWorldStageUpdateEventMessage>)(object)callback);
            public bool IsLeader => true;
            public long MainPlayerId => Convert.ToInt64(NativeMap14SharedPrefabs.Get(runtime.mainlineSharedPlayer, "Id"));
            public ECViewEntityData GetEntity(EcEntityIdentifier id) => runtime.roadblockDisplays.Values.Select(v => v.Data).FirstOrDefault(v => v.Id == id);
            public int GetContainerClassId(ECViewEntityData entity) => 14;
            public Pos2d GetPosition(ECViewEntityData entity) => ((NativeMap14SharedViewEntity)entity).Grid.Pos;
            public string GetEntityTag(ECViewEntityData entity)
            {
                var placement = runtime.roadblockDisplays.Single(v => ReferenceEquals(v.Value.Data, entity)).Key;
                return runtime.mapLogicTags.TryGetValue(placement, out var tag) ? tag.Tag : string.Empty;
            }
            private object Extend(int classId)
            {
                var parser = runtime.mainlineSharedWorld.Configuration.Manager.GetParser(runtime.mainlineSharedWorld.Assemblies.CommonType("EntityExtendParameterInfoParser"));
                return parser.GetType().GetMethod("Get", new[] { typeof(int) }).Invoke(parser, new object[] { classId });
            }
            public EntityTipDotExtendInfo GetExtendInfo(int classId)
            {
                var source = Extend(classId);
                return source == null ? null : new EntityTipDotExtendInfo {
                    InteractionUnique = (bool)NativeMap14SharedPrefabs.Get(source, "InteractionUnique"),
                    ReddotGraphName = (string)NativeMap14SharedPrefabs.Get(source, "ReddotGraphName") };
            }
            public EntityTipDotMapInfo GetMapInfo(int mapClassId)
            {
                var parser = runtime.mainlineSharedWorld.Configuration.Manager.GetParser(runtime.mainlineSharedWorld.Assemblies.CommonType("MapInfoParser"));
                var map = parser.GetType().GetMethod("Get", new[] { typeof(int) }).Invoke(parser, new object[] { mapClassId });
                return new EntityTipDotMapInfo { MapGroupId = (int)NativeMap14SharedPrefabs.Get(map, "MapGroupId") };
            }
            public string GetReddotGraphName(ECViewEntityData entity, int groupId, int mapClassId, string tag) => GetExtendInfo(entity.ClassId).ReddotGraphName;
            public bool IsShowTipDot => true;
            public bool TempLimitShowTip => false;
            public Action OnTipDotShowChanged { get; set; }
            public SafeAction OnTipDotSystemInited { get; set; }
            public SafeAction OnTipDotSystemPreClear { get; set; }
            public TipDotGraphType? GetGraphType(string graphName) => graphName == "ElementDoor" ? TipDotGraphType.Dynamic : (TipDotGraphType?)null;
            public bool LinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> callback)
            {
                if (key.GraphName != "ElementDoor" || key.NodeName != "0") throw new InvalidOperationException("Unmigrated map tip graph: " + key);
                var entity = (NativeMap14SharedViewEntity)((MapEntityNodeInstanceInfo)key.InstanceInfo).Entity;
                var rule = OfficialMainlineRoadblockRules.All.Single(r => r.EntityClassId == entity.ClassId);
                var graph = new RoadblockTipGraphRuntime(key,
                    () => (float)runtime.RoadblockPlayerProperty(rule.PropertyType) >= rule.PropertyStandard,
                    () => entity.Stage.State);
                nodes.Add(key, callback); graphs.Add(key, graph); callback(graph.Refresh()); return true;
            }
            public bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> callback)
            {
                if (graphs.TryGetValue(key, out var graph)) { graph.Dispose(); graphs.Remove(key); }
                return nodes.Remove(key);
            }
            public void Refresh()
            {
                foreach (var node in nodes) node.Value(graphs[node.Key].Refresh());
            }
        }
    }
}
