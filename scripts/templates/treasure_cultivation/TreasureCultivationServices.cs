using System.Collections.Generic;

namespace Zjcs.Official.TreasureCultivation
{
    public enum ItemType { TreasurePiece = 306, CommonTreasureMaterial = 427 }

    public sealed class ItemInfo { public int ClassId, Quality; public ItemType ItemType; }
    public sealed class TreasureInfo
    {
        public int StarMax;
        public bool LimitUseCommonMaterial;
        public int UpgradeMaxStar => StarMax - 1;
    }
    public sealed class TreasureState { public bool Active; public int Star; }
    public sealed class TreasureItem
    {
        public int Id;
        public long Count;
        public ItemInfo Info;
        public TreasureState State;
        public TreasureState GetItemParamTreasure() => State;
    }
    public sealed class TreasureUpgradeInfo
    {
        public int PieceCount, SpecialPieceCount;
        public Dictionary<int, long> Materials;
    }
    public sealed class TreasureCommonMaterial { public int ItemClassId, Item2PieceCount; }

    public interface ITreasureCultivationPlayer
    {
        TreasureItem GetItemById(int id);
        long GetItemAmount(int id);
        bool HasItem(int id, long count);
        bool HasItems(Dictionary<int, long> costs);
    }

    // These are the exact parser/merge operations consumed by TreasureHelper.
    // The owner supplies verified tables and its recursive item-merge module.
    public interface ITreasureCultivationTables
    {
        TreasureInfo GetTreasure(int id);
        ItemInfo GetItemInfo(int id);
        int GetTreasureIdByPieceId(int id);
        int GetPieceIdByTreasureId(int id);
        TreasureUpgradeInfo GetUpgrade(int quality, int star);
        Dictionary<int, long> GetMergeMaterials(int id);
        Dictionary<int, long> GetUpgradeMaterials(TreasureItem item);
        Dictionary<int, long> GetSpecialUpgradeMaterials(int id, int star, int quality);
        IEnumerable<KeyValuePair<int, long>> GetStarUpNonPiece(int id, int star);
        bool TryGetCommonMaterial(int quality, out TreasureCommonMaterial setting);
        bool IsUpgradeMaterial(int id);
        bool IsLevelMaterial(int id);
        void CalcMergeCost(ITreasureCultivationPlayer player, ref Dictionary<int, long> costs, Dictionary<int, long> requested);
    }
}
