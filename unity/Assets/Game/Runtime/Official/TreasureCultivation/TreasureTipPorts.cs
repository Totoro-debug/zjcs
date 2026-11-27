using System.Collections.Generic;
using ElementType = Zjcs.Official.TreasureSlots.ElementType;

namespace Zjcs.Official.TreasureCultivation
{
    public sealed class TreasureTipItemInfo { public ElementType ElementType; }
    // Original player/parser/promotion operations; queries never commit inventory or slot state.
    public interface ITreasureTipContext
    {
        IReadOnlyCollection<TreasureItem> FindItemsByType(ItemType type);
        ElementType GetElement(int id);
        TreasureTipItemInfo GetItemInfo(int id);
        int GetSuitClassIdByTreasure(int id);
        int GetTreasureIdByPieceId(int id);
        bool HasItem(int id, long count);
        bool UpgradeUseCommonMaterailEnable { get; }
        int GetTreasureInSlotIndex(int id);
        int CanActivateTreasure(int id);
        int CanMergeTreasure(int id);
        int CanTreasureSlotLevelUp(ElementType element, int index, bool autoMerge);
        int GetTreasureNextLevelUpgradeMaterial(int id, bool hasEmptyItem, bool useCommonItem, out Dictionary<int, long> materials);
        int CanTreasureUpgrade(int id, Dictionary<int, long> materials, bool autoMerge, bool canUseCommonMaterial);
        TreasureInfo GetTreasure(int id);
    }
}
