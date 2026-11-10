using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonRewardAssetCatalog : ScriptableObject,
        IDailyDungeonRewardItemMetadataAdapter
    {
        private const string ExpectedManifest = "884_156692";
        private static readonly Dictionary<string, int> ExpectedDetailDialogCounts = new Dictionary<string, int>
        {
            ["EquipDetailDialog"] = 861,
            ["CommonEquipPieceUseDetailDialog"] = 71,
            ["CommonItemPieceUseDetailDialog"] = 1,
            ["ItemDetailDialog"] = 231,
        };

        [Serializable]
        public sealed class Item
        {
            public int itemId;
            public int quality;
            public int itemType;
            public string itemTypeName;
            public string nameKey;
            public string typeKey;
            public string descriptionKey;
            public string functionDescriptionKey;
            public int pieceMergeCount;
            public bool canOverlap;
            public bool showPlayerItemCount;
            public string detailDialogType;
            public bool showPieceIcon;
            public bool isEquipment;
            public bool isCommonEquipmentPiece;
            public bool isRandomReward;
            public bool isFiltrationItem;
            public Sprite sprite;
        }

        [SerializeField] private string manifest;
        [SerializeField] private Item[] items;
        private Dictionary<int, Item> byId;
        private bool validated;

        public static DailyDungeonRewardAssetCatalog LoadDefault()
        {
            var catalog = Resources.Load<DailyDungeonRewardAssetCatalog>("Dungeons/DailyDungeonRewardAssetCatalog");
            if (catalog == null)
                throw new InvalidOperationException("Official daily dungeon reward assets are missing.");
            catalog.ValidateBindings();
            return catalog;
        }

        public Item Find(int itemId)
        {
            ValidateBindings();
            return byId.TryGetValue(itemId, out Item found) ? found : null;
        }

        private void ValidateBindings()
        {
            if (validated) return;
            if (manifest != ExpectedManifest || items == null || items.Length != 1164)
                throw new InvalidOperationException("Official daily dungeon reward assets are incomplete or from an unsupported version.");

            var next = new Dictionary<int, Item>(items.Length);
            var detailCounts = new Dictionary<string, int>();
            foreach (Item item in items)
            {
                if (item == null || item.itemId <= 0 || string.IsNullOrEmpty(item.detailDialogType)
                    || string.IsNullOrEmpty(item.itemTypeName) || string.IsNullOrEmpty(item.nameKey)
                    || string.IsNullOrEmpty(item.typeKey) || string.IsNullOrEmpty(item.descriptionKey)
                    || string.IsNullOrEmpty(item.functionDescriptionKey)
                    || item.pieceMergeCount < 0 || item.sprite == null || next.ContainsKey(item.itemId))
                    throw new InvalidOperationException("Official daily dungeon reward binding is invalid.");
                if (item.detailDialogType == "CommonEquipPieceUseDetailDialog" && item.pieceMergeCount != 100)
                    throw new InvalidOperationException("Official equipment-piece merge count differs from the selected source.");
                if (item.detailDialogType == "CommonItemPieceUseDetailDialog" && item.pieceMergeCount != 90)
                    throw new InvalidOperationException("Official item-piece merge count differs from the selected source.");
                next.Add(item.itemId, item);
                detailCounts.TryGetValue(item.detailDialogType, out int count);
                detailCounts[item.detailDialogType] = count + 1;
            }

            if (detailCounts.Count != ExpectedDetailDialogCounts.Count)
                throw new InvalidOperationException("Official daily dungeon reward detail-dialog types are incomplete.");
            foreach (KeyValuePair<string, int> expected in ExpectedDetailDialogCounts)
                if (!detailCounts.TryGetValue(expected.Key, out int actual) || actual != expected.Value)
                    throw new InvalidOperationException("Official daily dungeon reward detail-dialog type count differs: "
                        + expected.Key + " expected " + expected.Value + ".");

            byId = next;
            validated = true;
        }

        public DailyDungeonRewardItemMetadata Get(int itemId)
        {
            Item item = Find(itemId);
            return item == null ? null : new DailyDungeonRewardItemMetadata(item.itemId,
                item.quality, item.isEquipment, item.isCommonEquipmentPiece,
                item.isRandomReward, item.isFiltrationItem);
        }
    }
}
