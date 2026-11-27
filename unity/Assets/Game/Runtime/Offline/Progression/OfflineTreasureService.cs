using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.TreasureCultivation;

namespace Zjcs.Offline.Progression
{
    public delegate void TreasureMergeCostCalculator(ITreasureCultivationPlayer player,
        ref Dictionary<int, long> costs, Dictionary<int, long> requested);

    // Saved-player/parser boundary for the original promotion checks.
    public sealed class OfflineTreasureService : ITreasureCultivationPlayer, ITreasureCultivationTables
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureCatalog catalog;
        private readonly OfflineTreasureCostRules costs;
        private readonly Func<int, ItemInfo> readItem;
        private readonly TreasureMergeCostCalculator mergeCosts;
        private readonly Dictionary<int, int> treasureByPiece;
        private readonly HashSet<int> upgradeMaterials, levelMaterials;

        public OfficialTreasureCultivationRules Checks { get; }

        public OfflineTreasureService(OfflineMainlineProgressSession session, OfflineTreasureCatalog catalog,
            Func<int, ItemInfo> readItem, TreasureMergeCostCalculator mergeCosts = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.readItem = readItem ?? throw new ArgumentNullException(nameof(readItem));
            this.mergeCosts = mergeCosts;
            costs = new OfflineTreasureCostRules(catalog, id => GetItemInfo(id).Quality);
            treasureByPiece = catalog.Treasures.ToDictionary(row => row.TreasurePieceId, row => row.ClassId);
            upgradeMaterials = new HashSet<int>(catalog.UpgradeMaterials.SelectMany(row => row.Materials).Select(row => row.ItemId));
            levelMaterials = new HashSet<int>(catalog.LevelUpMaterials.SelectMany(row => row.Materials).Select(row => row.ItemId));
            Checks = new OfficialTreasureCultivationRules(this);
        }

        public int CanActivateTreasure(int id) => Checks.CanActivateTreasure(id, this);
        public int CanMergeTreasure(int id) => Checks.CanMergeTreasure(id, this);
        public bool PieceCanMerge(int id) => Checks.PieceCanMerge(id, this);
        public int GetTreasureNextLevelUpgradeMaterial(int id, bool hasEmptyItem, bool useCommonItem,
            out Dictionary<int, long> materials)
            => Checks.GetTreasureNextLevelUpgradeMaterial(id, this, hasEmptyItem, useCommonItem, out materials);
        public int CanTreasureUpgrade(int id, Dictionary<int, long> materials, bool autoMergeMaterial = false,
            bool canUseCommonMaterial = true)
            => Checks.CanTreasureUpgrade(id, materials, this, autoMergeMaterial, canUseCommonMaterial);

        public TreasureItem GetItemById(int id)
        {
            // Original GetItemById chooses the first actual stack, independently of Count.
            var stack = session.Save.Player.Inventory.FirstOrDefault(row => row.ItemId == id);
            if (stack == null) return null;
            var state = session.Save.Player.CharacterProperties.Treasures.FirstOrDefault(row => row.ClassId == id);
            return new TreasureItem { Id = id, Count = stack.Count, Info = GetItemInfo(id),
                State = state == null ? null : new TreasureState { Active = state.Active, Star = state.Star } };
        }

        public long GetItemAmount(int id) => session.InventoryCount(id);
        public bool HasItem(int id, long count) => GetItemAmount(id) >= count;
        public bool HasItems(Dictionary<int, long> requested)
        {
            foreach (var item in requested)
                if (GetItemAmount(item.Key) < item.Value) return false;
            return true;
        }

        public ItemInfo GetItemInfo(int id)
        {
            var row = readItem(id);
            return row == null ? null : new ItemInfo { ClassId = row.ClassId, Quality = row.Quality, ItemType = row.ItemType };
        }
        public TreasureInfo GetTreasure(int id)
        {
            var row = catalog.GetTreasure(id);
            return row == null ? null : new TreasureInfo { StarMax = row.StarMax, LimitUseCommonMaterial = row.LimitUseCommonMaterial };
        }
        public int GetTreasureIdByPieceId(int id) => treasureByPiece.TryGetValue(id, out int treasure) ? treasure : 0;
        public int GetPieceIdByTreasureId(int id)
        {
            var row = catalog.GetTreasure(id);
            if (row != null) return row.TreasurePieceId;
            UnityEngine.Debug.LogError("获取古遗物碎片Id数据失败 treasureId: " + id);
            return -1;
        }
        public TreasureUpgradeInfo GetUpgrade(int quality, int star)
        {
            var row = catalog.GetUpgradeRule(quality, star);
            return row == null ? null : new TreasureUpgradeInfo { PieceCount = row.PieceCount,
                SpecialPieceCount = row.SpecialPieceCount, Materials = row.Materials.ToDictionary(item => item.ItemId, item => item.Count) };
        }
        public Dictionary<int, long> GetMergeMaterials(int id) => costs.GetMergeMaterials(id);
        public Dictionary<int, long> GetUpgradeMaterials(TreasureItem item)
            => costs.GetUpgradeMaterials(item.Info.ClassId, item.State.Star, item.State.Star + 1);
        public Dictionary<int, long> GetSpecialUpgradeMaterials(int id, int star, int quality)
            => costs.GetSpecialUpgradeMaterials(id, star, quality);
        public IEnumerable<KeyValuePair<int, long>> GetStarUpNonPiece(int id, int star)
        {
            foreach (var row in costs.GetUpgradeMaterials(id, star, star + 1))
                if (row.Key != -2147483555 && GetItemInfo(row.Key).ItemType != ItemType.TreasurePiece)
                    yield return row;
        }
        public bool TryGetCommonMaterial(int quality, out TreasureCommonMaterial setting)
        {
            var row = catalog.GetCommonMaterial(quality);
            setting = row == null ? null : new TreasureCommonMaterial { ItemClassId = row.ItemClassId, Item2PieceCount = row.Item2PieceCount };
            return row != null;
        }
        public bool IsUpgradeMaterial(int id) => upgradeMaterials.Contains(id);
        public bool IsLevelMaterial(int id) => levelMaterials.Contains(id);
        public void CalcMergeCost(ITreasureCultivationPlayer player, ref Dictionary<int, long> result, Dictionary<int, long> requested)
        {
            if (mergeCosts == null) throw new InvalidOperationException("The recursive item-merge owner is required for automatic treasure material merging.");
            mergeCosts(player, ref result, requested);
        }
    }
}
