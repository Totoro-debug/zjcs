using System;
using System.Collections.Generic;

namespace Zjcs.Presentation.World
{
    /// <summary>Bindings and source assertions for maps already migrated to the shared runtime.</summary>
    public sealed class OfficialMainlineMapProfile
    {
        private static readonly OfficialMainlineMapProfile Map10 = new OfficialMainlineMapProfile(
            10, 21, 9, null, new[] { 1201, 1202, 1203, 1204 });
        private static readonly OfficialMainlineMapProfile Map11 = new OfficialMainlineMapProfile(
            11, 130, 44, "Map11/UI/", new[] { 1205, 1206, 1207, 1208, 4003, 4065, 4066, 4445 }, 73);
        private static readonly OfficialMainlineMapProfile Map12 = new OfficialMainlineMapProfile(
            12, 155, 49, "Map12/UI/", new[] { 1211, 1212, 1213, 1218, 1219, 1221, 1222, 1223, 4108, 4110, 4113 }, 98);

        private static readonly OfficialMainlineMapProfile Map13 = new OfficialMainlineMapProfile(
            13, 158, 74, null, new[] { 1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231, 4139, 4153, 4157, 4160, 4163 });

        public int MapClassId { get; }
        public int StageCount { get; }
        public int StoryCount { get; }
        public string NativeUiRoot { get; }
        public bool HasNativeUi => NativeUiRoot != null;
        public int RegionShadowCount { get; }
        public bool HasRegionFog => RegionShadowCount > 0;
        public IReadOnlyList<int> RequiredAccessConditions { get; }

        private OfficialMainlineMapProfile(int id, int stages, int stories, string nativeUiRoot, int[] conditions,
            int regionShadowCount = 0)
        {
            MapClassId = id;
            StageCount = stages;
            StoryCount = stories;
            NativeUiRoot = nativeUiRoot;
            RegionShadowCount = regionShadowCount;
            RequiredAccessConditions = Array.AsReadOnly(conditions);
        }

        public string Resource(string path) => "Map" + MapClassId + "/" + path;

        public string NativeUi(string path) => HasNativeUi ? NativeUiRoot + path
            : throw new InvalidOperationException("Native world UI has not been migrated for Map" + MapClassId);

        public static OfficialMainlineMapProfile Get(int mapClassId)
        {
            switch (mapClassId)
            {
                case 10: return Map10;
                case 11: return Map11;
                case 12: return Map12;
                case 13: return Map13;
                default: throw new ArgumentOutOfRangeException(nameof(mapClassId), "Only migrated world maps can be opened.");
            }
        }
    }
}
