using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.ItemMerge
{
    public interface IItemMergeInventory { long GetItemAmount(int itemId); }

    [Serializable]
    public sealed class ItemMergeInfo
    {
        public int ProductId, Count, Material1, Count1, Material2, Count2, Material3, Count3,
            Material4, Count4, Material5, Count5, LevelFactorId;
        [NonSerialized] public Dictionary<int, int> Materials = new Dictionary<int, int>();

        public ItemMergeInfo CopyRaw() => new ItemMergeInfo { ProductId = ProductId, Count = Count,
            Material1 = Material1, Count1 = Count1, Material2 = Material2, Count2 = Count2,
            Material3 = Material3, Count3 = Count3, Material4 = Material4, Count4 = Count4,
            Material5 = Material5, Count5 = Count5, LevelFactorId = LevelFactorId };
    }

    [Serializable] public sealed class ItemMergeItemType { public int ClassId, ItemType; }
    [Serializable] public sealed class ItemMergeLevelNumber { public int Id; public List<double> Nums; }
    [Serializable] public sealed class ItemMergeData
    {
        public long MaxMergeAmountAtATime;
        public ItemMergeInfo[] Recipes;
        public ItemMergeItemType[] ItemTypes;
        public ItemMergeLevelNumber[] Levels;
    }

    public sealed partial class OfficialItemMergeRules
    {
        private readonly Dictionary<int, ItemMergeInfo> configs;
        private readonly Dictionary<int, List<ItemMergeInfo>> ItemMergeDict = new Dictionary<int, List<ItemMergeInfo>>();
        private readonly Dictionary<int, List<ItemMergeInfo>> ProductDict = new Dictionary<int, List<ItemMergeInfo>>();
        private readonly Func<int, int> readItemType;
        private readonly Func<int, int, float> levelNumber;
        private readonly long maxMergeAmountAtATime;

        public OfficialItemMergeRules(IEnumerable<ItemMergeInfo> rows, long maximum,
            Func<int, int> readItemType, Func<int, int, float> levelNumber)
        {
            configs = (rows ?? throw new ArgumentNullException(nameof(rows))).ToDictionary(row => row.ProductId, row => row.CopyRaw());
            this.readItemType = readItemType ?? throw new ArgumentNullException(nameof(readItemType));
            this.levelNumber = levelNumber ?? throw new ArgumentNullException(nameof(levelNumber));
            maxMergeAmountAtATime = maximum;
            PostParse();
        }

        public ItemMergeInfo Get(int productId) => configs.TryGetValue(productId, out var row) ? row : null;
        private static long Existing(IReadOnlyDictionary<int, long> rows, int key)
            => rows.TryGetValue(key, out long value) ? value : 0;
    }

    public sealed partial class ItemMergeLevelNumbers
    {
        private readonly Dictionary<int, ItemMergeLevelNumber> rows;
        private readonly Action<int> missing;
        public ItemMergeLevelNumbers(IEnumerable<ItemMergeLevelNumber> rows, Action<int> missing)
        {
            this.rows = rows.ToDictionary(row => row.Id);
            this.missing = missing ?? throw new ArgumentNullException(nameof(missing));
        }
        private ItemMergeLevelNumber Get(int id) => rows.TryGetValue(id, out var row) ? row : null;
    }
}
