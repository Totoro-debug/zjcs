using System.Linq;
using Zjcs.Offline.World;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    internal sealed class OfficialMap14RoadblockRule
    {
        internal readonly int PlacementId;
        internal readonly int EntityClassId;
        internal readonly int Region;
        internal readonly int PropertyType;
        internal readonly long RequiredValue;
        internal readonly MapGridPoint[] OpenGroundOffsets;

        internal OfficialMap14RoadblockRule(int placementId, int entityClassId, int region,
            int propertyType, long requiredValue, params MapGridPoint[] openGroundOffsets)
        {
            PlacementId = placementId;
            EntityClassId = entityClassId;
            Region = region;
            PropertyType = propertyType;
            RequiredValue = requiredValue;
            OpenGroundOffsets = openGroundOffsets;
        }
    }

    internal static class OfficialMap14RoadblockRules
    {
        private static readonly OfficialMap14RoadblockRule[] Gates =
        {
            new OfficialMap14RoadblockRule(701, 8625, 6, 415, 6215,
                new MapGridPoint(0, 0), new MapGridPoint(1, 0), new MapGridPoint(-1, 0)),
            new OfficialMap14RoadblockRule(2701, 8626, 26, 411, 7280,
                new MapGridPoint(0, 0), new MapGridPoint(1, 0), new MapGridPoint(-1, 0)),
            new OfficialMap14RoadblockRule(3801, 8634, 37, 419, 7525,
                new MapGridPoint(0, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1)),
            new OfficialMap14RoadblockRule(6401, 8637, 63, 417, 8680,
                new MapGridPoint(0, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1)),
            new OfficialMap14RoadblockRule(9101, 8633, 90, 418, 10055,
                new MapGridPoint(0, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1))
        };

        internal static OfficialMap14RoadblockRule Find(int placementId) =>
            Gates.SingleOrDefault(value => value.PlacementId == placementId);

        internal static bool Contains(int placementId) => Find(placementId) != null;

        internal static long OverlayId(int placementId, int offsetIndex) =>
            (14L << 56) | ((long)placementId << 16) | (uint)offsetIndex;
    }
}
