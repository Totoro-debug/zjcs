using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.Map13Navigation;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13GridVisibility;
using Entity = Zjcs.Presentation.World.OfficialMainlineMapRuntime.Map10RuntimeEntity;

namespace Zjcs.Presentation.World
{
    /// <summary>Reads visited worlds without mounting a scene or advancing their saves.</summary>
    public sealed class MainlineMapMiniMapSnapshots
    {
        private readonly OfficialWorldMapAssetCatalog assets;
        private readonly ProfileSlotCatalog slots;
        private readonly string slotId;
        private readonly Func<long> now;

        public MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog assets, ProfileSlotCatalog slots,
            string slotId, Func<long> now = null)
        {
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            this.slotId = slotId;
            this.now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public LocalMiniMapSnapshot Read(int mapClassId) => ReadCore(mapClassId, false, out _);

        // Original Player progress is measured in raw entity points. Read the
        // same restored states used by the live host, without requiring that a
        // visited map has already saved its minimap visibility.
        public Dictionary<int, int> ReadRegionPoints(int mapClassId)
        {
            ReadCore(mapClassId, true, out var points);
            return points;
        }

        private LocalMiniMapSnapshot ReadCore(int mapClassId, bool pointsOnly, out Dictionary<int, int> points)
        {
            var progress = OfflineMainlineProgressSession.InspectCharacter(slots, slotId);
            if (!progress.Save.World.Maps.Any(value => value.MapClassId == mapClassId))
                throw new InvalidDataException("Only a previously visited map can supply an offline minimap.");
            var map = OfficialMainlineMapProfile.Get(mapClassId);
            OfficialMapDefinition definition = assets.LoadMap(mapClassId).Definition;
            int width = Mathf.CeilToInt(definition.Navigation.Width), height = Mathf.CeilToInt(definition.Navigation.Height);
            Map10LocalSave local = MainlineMapLocalSaveFile.Read(Path.Combine(slots.GetSlotDirectory(slotId),
                "map-" + mapClassId + "-save.json"), slotId, mapClassId, width, height);
            if (local == null) throw new InvalidDataException("The visited map has no local world save.");
            var entities = OfficialMap10EntityView.ReadEntityInfos(Resources.Load<TextAsset>(map.Resource(
                mapClassId == 12 ? "Entities/native-catalog" : "Entities/catalog")), mapClassId)
                .ToDictionary(value => value.PlacementId, value => new Entity(value));
            long readAt = now();
            var generated = progress.Save.World.Exploration.Entities.Where(value => map.HasNativeUi
                && value.MapClassId == mapClassId && value.Active(readAt)).ToArray();
            if (generated.Length > 0)
            {
                var catalog = OfficialWorldExplorationCatalog.Load();
                foreach (var entity in generated)
                    entities.Add(entity.PlacementId, new Entity(new OfficialMap10EntityInfo(
                        MainlineMapEntityRestore.ExploreRecord(catalog, entity))));
            }
            var explored = new HashSet<int>(local.exploredEntityPlacements);
            if (explored.Any(id => !entities.ContainsKey(id)))
                throw new InvalidDataException("Saved exploration references another map's entity placement.");
            Map10RuntimeStoryData story = ReadStoryData(map);
            var tags = (story?.mapLogicTags ?? Array.Empty<Map10RuntimeLogicTag>()).ToDictionary(value => value.LogicId);
            MapPuzzleRule[] rules = mapClassId == 14 ? Map14PuzzleAssets.LoadDefault().Load().Rules
                : story?.puzzleRules ?? Array.Empty<MapPuzzleRule>();
            OfficialMap12ChestCatalog chests = null;
            if (mapClassId == 12)
            {
                chests = OfficialMap12ChestCatalog.Load();
                MainlineMapEntityRestore.RecoverChests(chests, local, progress.Save);
                MainlineMapEntityRestore.RecoverSteles(OfficialMap12SteleCatalog.Load(), local, progress.Save);
            }
            OfflineMapPuzzleSession puzzles = null;
            var puzzleIds = new HashSet<int>();
            if (map.HasNativeUi)
            {
                puzzleIds = MainlineMapEntityRestore.SelectPuzzles(mapClassId, rules, new HashSet<int>(entities.Keys));
                puzzles = new OfflineMapPuzzleSession(rules, local.puzzleStates,
                    id => entities.TryGetValue(id, out var entity) ? entity.State : 0,
                    id => entities.TryGetValue(id, out var entity) && (local.completedEntityPlacements.Contains(id)
                        || entity.StageIds.Any(local.completedStages.Contains)));
                puzzles.RefreshLocks();
                foreach (int id in puzzleIds)
                {
                    if (!puzzles.Contains(id)) continue;
                    entities[id].State = puzzles.State(id);
                    if (mapClassId == 12) entities[id].Grid = puzzles.Position(id);
                }
            }
            RestoreStatues(mapClassId, entities.Values, local, progress.Save, story);
            if (mapClassId == 12 && puzzles != null)
            {
                MainlineMapEntityRestore.SynchronizeChests(chests, local, entities, puzzles);
                if (progress.Save.World.CompletedEntityTags.Contains("map12:building:3501"))
                {
                    puzzles.SetState(3501, 1);
                    entities[3501].State = 1;
                }
            }
            var water = mapClassId == 13 ? Map13NavigationAssets.LoadDefault().Open(local.map13WaterStates).Water.Save()
                .ToDictionary(value => value.PlacementId) : new Dictionary<int, Map13WaterState>();
            foreach (var value in water.Values)
            {
                if (!entities.TryGetValue(value.PlacementId, out var entity))
                    throw new InvalidDataException("Saved water navigation references another map's entity placement.");
                entity.State = value.State;
                entity.Grid = value.Grid;
                entity.Active = value.Spawned;
            }
            var restoredPoints = MainlineMapEntityRestore.RegionPoints(definition, local, entities.Values, tags);
            points = restoredPoints;
            if (pointsOnly) return null;
            string encoded = mapClassId == 13 ? local.map13GridVisibility : local.gridVisibility;
            if (string.IsNullOrEmpty(encoded))
                throw new InvalidDataException("The visited map has no saved exploration visibility.");
            var policy = new OfficialMainlineRegionPolicy(definition, map);
            var presentation = new MainlineMapEntityPresentation(definition, map, local, progress.Save, tags,
                region => policy.IsAccessible(region, restoredPoints,
                    key => Math.Max(local.storyPlayCounts.FirstOrDefault(value => value.key == key)?.count ?? 0,
                        progress.StoryCount(key)), progress.StoryStatus,
                    tag => progress.Save.World.CompletedEntityTags.Contains(tag)
                        || local.mapLogicStates.Any(value => value.Tag == tag && value.State < 0)), readAt);
            byte[] fog = Convert.FromBase64String(encoded);
            var visibility = GridVisibilityCompressUtils.DoMyUnzip(fog);
            var snapshot = new LocalMiniMapSnapshot
            {
                MapClassId = mapClassId, Width = width, Height = height, Visited = true,
                GridVisibility = fog,
                PublicRegions = progress.Save.World.ActivatedGoddessRegions.Where(value => value.MapClassId == mapClassId)
                    .Select(value => value.Region).ToList()
            };
            foreach (Entity entity in entities.Values)
            {
                bool spawned = water.TryGetValue(entity.PlacementId, out var state) ? state.Spawned
                    : puzzleIds.Contains(entity.PlacementId) && puzzles != null ? puzzles.IsSpawned(entity.PlacementId) : entity.Active;
                var result = presentation.Read(entity, entity.State, spawned);
                // Inactive world objects have no current marker; public-region
                // defaults must not resurrect completed or removed entities.
                if (!result.Active) continue;
                bool visible = entity.Grid.X >= 0 && entity.Grid.X < width && entity.Grid.Y >= 0 && entity.Grid.Y < height
                    && visibility.GetPosValue(entity.Grid.X, entity.Grid.Y);
                snapshot.Entities.Add(new LocalMiniMapEntity
                {
                    PlacementId = entity.PlacementId, EntityClassId = entity.EntityClassId,
                    Region = entity.Region, State = result.State,
                    Explored = explored.Contains(entity.PlacementId) || visible
                });
            }
            return snapshot;
        }

        public static LocalMiniMapDataSource CreateOfflineDataSource(
            OfficialWorldMapAssetCatalog assets, ProfileSlotCatalog slots, string slotId, Func<long> now = null)
        {
            var snapshots = new MainlineMapMiniMapSnapshots(assets, slots, slotId, now);
            return new LocalMiniMapDataSource(snapshots.Read);
        }

        private static Map10RuntimeStoryData ReadStoryData(OfficialMainlineMapProfile map)
        {
            if (map.MapClassId == 15) return null;
            var source = Resources.Load<TextAsset>(map.Resource("stories"));
            var story = source ? JsonUtility.FromJson<Map10RuntimeStoryData>(source.text) : null;
            string manifest = map.MapClassId >= 13 ? "871_156598" : "854_156383";
            if (story == null || story.mapClassId != map.MapClassId || story.manifest != manifest || story.mapLogicTags == null)
                throw new InvalidDataException("Official map state restoration data is absent or differs from its source.");
            return story;
        }

        private static void RestoreStatues(int mapClassId, IEnumerable<Entity> entities, Map10LocalSave local,
            OfflineMainlineSave profile, Map10RuntimeStoryData story)
        {
            foreach (Entity entity in entities)
            {
                if (mapClassId == 11 && entity.EntityClassId == 6620)
                {
                    int index = profile.World.ActivatedWitches.FindIndex(value =>
                        value.MapClassId == mapClassId && value.PlacementId == entity.PlacementId);
                    if (index >= 0 && MainlineMapEntityRestore.WitchNextState(
                        story.witchInfos.Single(value => value.ActiveCount == index), local, entity.PlacementId)) entity.State = 1;
                }
                if (entity.TypeId == 223 && MainlineMapEntityPresentation.HeroActivated(profile, mapClassId, entity.PlacementId)
                    && !local.completedEntityPlacements.Contains(entity.PlacementId))
                    local.completedEntityPlacements = local.completedEntityPlacements.Concat(new[] { entity.PlacementId }).ToArray();
                if (entity.TypeId == 223)
                    entity.State = MainlineMapEntityPresentation.HeroState(profile, mapClassId, entity.PlacementId,
                        profile.World.ActivatedGoddessRegions.Any(value => value.MapClassId == mapClassId && value.Region == entity.Region));
            }
        }
    }
}
