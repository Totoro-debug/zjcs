using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Zjcs.Presentation.World
{
    /// <summary>Per-map source facts and runtime readiness for shared mainline entry.</summary>
    public sealed class OfficialMainlineMapProfile
    {
        private static readonly IReadOnlyDictionary<int, OfficialMainlineMapProfile> Profiles =
            new ReadOnlyDictionary<int, OfficialMainlineMapProfile>(new Dictionary<int, OfficialMainlineMapProfile>
            {
                [10] = new OfficialMainlineMapProfile(10, 21, 9, null,
                    new[] { 1201, 1202, 1203, 1204 }, true,
                    visibilityResource: "World/Mainline/Visibility/Map10/visibility",
                    bodiesResource: "World/Mainline/Visibility/Map10/bodies", fogResource: "World/Mainline/Fog/Map10", localChannelCount: 1),
                [11] = new OfficialMainlineMapProfile(11, 130, 44, "Map11/UI/",
                    new[] { 1205, 1206, 1207, 1208, 4003, 4065, 4066, 4445 }, true, 73, defaultEntryPlacementId: 101,
                    visibilityResource: "World/Mainline/Visibility/Map11/visibility",
                    bodiesResource: "World/Mainline/Visibility/Map11/bodies", fogResource: "World/Mainline/Fog/Map11", localChannelCount: 1),
                [12] = new OfficialMainlineMapProfile(12, 155, 49, "Map12/UI/",
                    new[] { 1211, 1212, 1213, 1218, 1219, 1221, 1222, 1223, 4108, 4110, 4113 }, true, 98, 99,
                    defaultEntryPlacementId: 101,
                    visibilityResource: "World/Mainline/Visibility/Map12/visibility",
                    bodiesResource: "World/Mainline/Visibility/Map12/bodies", fogResource: "World/Mainline/Fog/Map12",
                    interactionResource: "Map12/interactions", interactionManifest: "871_156598", localChannelCount: 1),
                [13] = new OfficialMainlineMapProfile(13, 158, 74, "Map13/UI/",
                    new[] { 1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231, 4139, 4153, 4157, 4160, 4163 }, true, regionCount: 106, defaultEntryPlacementId: 11001,
                    visibilityResource: "Map13/grid-visibility", bodiesResource: "Map13/grid-entities",
                    fogResource: "World/Mainline/Fog/Map13",
                    interactionResource: "Map13/interactions", interactionManifest: "871_156598", localChannelCount: 1,
                    storyDataManifest: "871_156598"),
                [14] = new OfficialMainlineMapProfile(14, 217, 85, "Map14/UI/",
                    new[] { 1255, 1256, 1257, 1258, 1259, 1260, 1261, 1262, 4209, 4210, 4211, 4212, 4213, 4228, 4229, 4236 },
                    true, 0, 111, 101,
                    visibilityResource: "World/Mainline/Visibility/Map14/visibility",
                    bodiesResource: "World/Mainline/Visibility/Map14/bodies", fogResource: "World/Mainline/Fog/Map14",
                    interactionResource: "Map14/interactions", interactionManifest: "871_156598",
                    requiresMiniMapPrefab: false, localChannelCount: 1,
                    playerViewResource: "Map13/BaseGround/player-view",
                    entityPrefabCatalogResource: "Map14/Native/entity-prefab-catalog", entityPrefabManifest: "871_156598",
                    battleDataManifest: "871_156598", storyDataManifest: "871_156598"),
                [15] = new OfficialMainlineMapProfile(15, 0, 0, null,
                    new[] { 115 }, true, 0, 1, 101,
                    visibilityResource: "World/Mainline/Visibility/Map15/visibility",
                    bodiesResource: "World/Mainline/Visibility/Map15/bodies", requiresMiniMapPrefab: false,
                    playerViewResource: "Map15/BaseGround/player-view", nativeGroundLabelResource: "Map15/Native/region-label-data",
                    nativeGroundPrefabResource: "Map15/Native/current-ground", localChannelCount: 1,
                    storyDataManifest: "871_156598"), // Original root has no cloud; MiniMapPath is empty.
                [16] = new OfficialMainlineMapProfile(16, 70, 6, null,
                    Array.Empty<int>(), false, regionCount: 43, defaultEntryPlacementId: 101,
                    visibilityResource: "World/Mainline/Visibility/Map16/visibility",
                    bodiesResource: "World/Mainline/Visibility/Map16/bodies",
                    fogResource: "World/Mainline/Fog/Map16",
                    interactionResource: "Map16/interactions", interactionManifest: "884_156692",
                    localChannelCount: 1, battleDataManifest: "884_156692", storyDataManifest: "884_156692")
            });

        public int MapClassId { get; }
        public int StageCount { get; }
        public int StoryCount { get; }
        public string NativeUiRoot { get; }
        public bool HasNativeUi => NativeUiRoot != null;
        public int RegionShadowCount { get; }
        public bool HasRegionFog => RegionShadowCount > 0;
        public bool IsRuntimeEntryReady { get; }
        public int RegionCount { get; }
        public int DefaultEntryPlacementId { get; }
        public IReadOnlyList<int> RequiredAccessConditions { get; }
        public string GridVisibilityResource { get; }
        public string GridBodiesResource { get; }
        public string GridFogResource { get; }
        public string InteractionResource { get; }
        public string InteractionManifest { get; }
        public bool RequiresMiniMapPrefab { get; }
        public int LocalChannelCount { get; }
        public string PlayerViewResource { get; }
        public string NativeGroundLabelResource { get; }
        public string NativeGroundPrefabResource { get; }
        public string EntityPrefabCatalogResource { get; }
        public string EntityPrefabManifest { get; }
        public string BattleDataManifest { get; }
        public string StoryDataManifest { get; }

        private OfficialMainlineMapProfile(int id, int stages, int stories, string nativeUiRoot, int[] conditions,
            bool runtimeEntryReady, int regionShadowCount = 0, int regionCount = 0, int defaultEntryPlacementId = 0,
            string visibilityResource = null, string bodiesResource = null, string fogResource = null,
            string interactionResource = "World/Mainline/interactions", string interactionManifest = "854_156383",
            bool requiresMiniMapPrefab = true, string playerViewResource = null, string nativeGroundLabelResource = null,
            string nativeGroundPrefabResource = null, int localChannelCount = 1,
            string entityPrefabCatalogResource = null, string entityPrefabManifest = null,
            string battleDataManifest = "854_156383", string storyDataManifest = null)
        {
            MapClassId = id;
            StageCount = stages;
            StoryCount = stories;
            NativeUiRoot = nativeUiRoot;
            RegionShadowCount = regionShadowCount;
            IsRuntimeEntryReady = runtimeEntryReady;
            RegionCount = regionCount;
            DefaultEntryPlacementId = defaultEntryPlacementId;
            RequiredAccessConditions = Array.AsReadOnly(conditions);
            GridVisibilityResource = visibilityResource;
            GridBodiesResource = bodiesResource;
            GridFogResource = fogResource;
            InteractionResource = interactionResource;
            InteractionManifest = interactionManifest;
            RequiresMiniMapPrefab = requiresMiniMapPrefab;
            if (localChannelCount < 1) throw new ArgumentOutOfRangeException(nameof(localChannelCount));
            LocalChannelCount = localChannelCount;
            PlayerViewResource = playerViewResource ?? (nativeUiRoot == null ? null : "Map" + id + "/BaseGround/player-view");
            NativeGroundLabelResource = nativeGroundLabelResource;
            NativeGroundPrefabResource = nativeGroundPrefabResource;
            if (string.IsNullOrWhiteSpace(entityPrefabCatalogResource) != string.IsNullOrWhiteSpace(entityPrefabManifest))
                throw new ArgumentException("Entity prefab resource and source manifest must be configured together.");
            EntityPrefabCatalogResource = entityPrefabCatalogResource;
            EntityPrefabManifest = entityPrefabManifest;
            BattleDataManifest = string.IsNullOrWhiteSpace(battleDataManifest)
                ? throw new ArgumentException("A battle source manifest is required.", nameof(battleDataManifest))
                : battleDataManifest;
            StoryDataManifest = string.IsNullOrWhiteSpace(storyDataManifest) ? battleDataManifest : storyDataManifest;
        }

        public string Resource(string path) => "Map" + MapClassId + "/" + path;

        public string NativeUi(string path) => HasNativeUi ? NativeUiRoot + path
            : throw new InvalidOperationException("Native world UI has not been migrated for Map" + MapClassId);

        public static OfficialMainlineMapProfile Get(int mapClassId)
        {
            if (Profiles.TryGetValue(mapClassId, out OfficialMainlineMapProfile profile)) return profile;
            throw new ArgumentOutOfRangeException(nameof(mapClassId), "No mainline map profile is registered for ClassId " + mapClassId + ".");
        }
    }
}
