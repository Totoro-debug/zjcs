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
                default: throw new ArgumentOutOfRangeException(nameof(mapClassId), "Only migrated world maps can be opened.");
            }
        }
    }
}
