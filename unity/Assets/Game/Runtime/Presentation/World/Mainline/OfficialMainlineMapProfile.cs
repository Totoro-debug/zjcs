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
                    new[] { 1201, 1202, 1203, 1204 }, true),
                [11] = new OfficialMainlineMapProfile(11, 130, 44, "Map11/UI/",
                    new[] { 1205, 1206, 1207, 1208, 4003, 4065, 4066, 4445 }, true, 73),
                [12] = new OfficialMainlineMapProfile(12, 155, 49, "Map12/UI/",
                    new[] { 1211, 1212, 1213, 1218, 1219, 1221, 1222, 1223, 4108, 4110, 4113 }, true, 98),
                [13] = new OfficialMainlineMapProfile(13, 158, 74, "Map13/UI/",
                    new[] { 1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231, 4139, 4153, 4157, 4160, 4163 }, true),
                [14] = new OfficialMainlineMapProfile(14, 217, 85, null,
                    new[] { 1255, 1256, 1257, 1258, 1259, 1260, 1261, 1262, 4209, 4210, 4211, 4212, 4213, 4228, 4229, 4236 },
                    true, 0, 111, 101),
                [15] = new OfficialMainlineMapProfile(15, 0, 0, null,
                    new[] { 115 }, true, 0, 1, 101)
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

        private OfficialMainlineMapProfile(int id, int stages, int stories, string nativeUiRoot, int[] conditions,
            bool runtimeEntryReady, int regionShadowCount = 0, int regionCount = 0, int defaultEntryPlacementId = 0)
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
