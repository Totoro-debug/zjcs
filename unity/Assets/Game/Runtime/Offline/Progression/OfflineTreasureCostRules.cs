using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    // Original TreasureUpgradeInfoParser queries. Ownership, inventory and
    // transaction decisions belong to the caller, outside these table queries.
    public sealed class OfflineTreasureCostRules
    {
        private readonly OfflineTreasureCatalog catalog;
        private readonly Func<int, int> readItemQuality;

        public OfflineTreasureCostRules(OfflineTreasureCatalog catalog, Func<int, int> readItemQuality)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.readItemQuality = readItemQuality ?? throw new ArgumentNullException(nameof(readItemQuality));
        }

        public Dictionary<int, long> GetMergeMaterials(int treasureClassId)
        {
            var rule = catalog.GetUpgradeRule(readItemQuality(treasureClassId), 0);
            var result = new Dictionary<int, long>();
            foreach (var material in rule.Materials) result.Add(material.ItemId, material.Count);
            if (rule.PieceCount > 0)
                result.Add(catalog.GetTreasure(treasureClassId).TreasurePieceId, rule.PieceCount);
            return result;
        }

        public Dictionary<int, long> GetUpgradeMaterials(int treasureClassId, int currentStar, int targetStar)
        {
            var result = new Dictionary<int, long>();
            var treasure = catalog.GetTreasure(treasureClassId);
            if (targetStar > treasure.StarMax) targetStar = treasure.StarMax;
            if (targetStar == currentStar) return result;
            int quality = readItemQuality(treasureClassId);
            var current = catalog.GetUpgradeRule(quality, currentStar);
            int pieceCount = unchecked(catalog.GetUpgradeRule(quality, targetStar).PieceCount - (current?.PieceCount ?? 0));
            if (pieceCount > 0) result.Add(treasure.TreasurePieceId, pieceCount);
            for (int star = currentStar + 1; star <= targetStar; star++)
            {
                foreach (var material in catalog.GetUpgradeRule(quality, star).Materials)
                {
                    if (material.Count == 0) continue;
                    if (!result.ContainsKey(material.ItemId)) result.Add(material.ItemId, 0);
                    result[material.ItemId] = unchecked(result[material.ItemId] + material.Count);
                }
            }
            return result;
        }

        public Dictionary<int, long> GetSpecialUpgradeMaterials(int treasureClassId, int star, int quality)
        {
            int pieceId = catalog.GetTreasure(treasureClassId).TreasurePieceId;
            var current = catalog.GetUpgradeRule(quality, star);
            var next = catalog.GetUpgradeRule(quality, star + 1);
            return new Dictionary<int, long>
            {
                { pieceId, current == null || next == null ? long.MaxValue : unchecked(next.SpecialPieceCount - current.SpecialPieceCount) }
            };
        }
    }
}
