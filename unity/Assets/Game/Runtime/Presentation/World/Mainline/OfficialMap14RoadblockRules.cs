using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    internal sealed class OfficialMainlineRoadblockRule
    {
        public int PlacementId { get; }
        public int EntityClassId { get; }
        public int Region { get; }
        public int PropertyType { get; }
        public long PropertyStandard { get; }
        public IReadOnlyList<Vector2Int> BodyOffsets { get; }

        public OfficialMainlineRoadblockRule(int placementId, int entityClassId, int region,
            int propertyType, long propertyStandard, params Vector2Int[] openOffsets)
        {
            PlacementId = placementId;
            EntityClassId = entityClassId;
            Region = region;
            PropertyType = propertyType;
            PropertyStandard = propertyStandard;
            BodyOffsets = openOffsets?.ToArray() ?? System.Array.Empty<Vector2Int>();
        }
    }

    internal static class OfficialMainlineRoadblockRules
    {
        private static readonly OfficialMainlineRoadblockRule[] Rules =
        {
            new OfficialMainlineRoadblockRule(701, 8625, 6, 415, 6215,
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0)),
            new OfficialMainlineRoadblockRule(2701, 8626, 26, 411, 7280,
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0)),
            new OfficialMainlineRoadblockRule(3801, 8634, 37, 419, 7525,
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)),
            new OfficialMainlineRoadblockRule(6401, 8637, 63, 417, 8680,
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)),
            new OfficialMainlineRoadblockRule(9101, 8633, 90, 418, 10055,
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, -1))
        };

        public static IReadOnlyList<OfficialMainlineRoadblockRule> All => Rules;

        public static OfficialMainlineRoadblockRule Find(int placementId) =>
            Rules.FirstOrDefault(value => value.PlacementId == placementId);
    }
}
