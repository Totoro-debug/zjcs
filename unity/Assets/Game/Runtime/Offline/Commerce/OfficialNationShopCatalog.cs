using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Commerce
{
    [Serializable]
    public sealed class OfficialNationShopCatalogData
    {
        public string manifest;
        public OfficialNationShopGroup[] groups;
        public OfficialNationShopLevel[] levels;
        public OfficialNationShop[] shops;
        public OfficialNationShopGood[] goods;
    }

    [Serializable]
    public sealed class OfficialNationShopGroup
    {
        public int groupId;
        public int currencyItemId;
    }

    [Serializable]
    public sealed class OfficialNationShopLevel
    {
        public int groupId;
        public int level;
        public int shopId;
        public long experienceBoundary;
    }

    [Serializable]
    public sealed class OfficialNationShop
    {
        public int id;
        public int refreshLimit;
        public int refreshCycleType;
        public int[] fixedGoods;
    }

    [Serializable]
    public sealed class OfficialNationShopGood
    {
        public int id;
        public int shopId;
        public int itemId;
        public long itemCount;
        public int currencyItemId;
        public long price;
        public int inventory;
        public int totalBuyLimit;
        public int refreshCycleType;
        public bool supportsMultiple;
    }

    public sealed class OfficialNationShopCatalog
    {
        public OfficialNationShopCatalogData Data { get; }
        private readonly Dictionary<int, OfficialNationShopGroup> groups;
        private readonly Dictionary<int, OfficialNationShop> shops;
        private readonly Dictionary<int, OfficialNationShopGood> goods;
        private readonly Dictionary<int, OfficialNationShopLevel[]> levels;

        private OfficialNationShopCatalog(OfficialNationShopCatalogData data)
        {
            Data = data;
            groups = data.groups.ToDictionary(row => row.groupId);
            shops = data.shops.ToDictionary(row => row.id);
            goods = data.goods.ToDictionary(row => row.id);
            levels = data.levels.GroupBy(row => row.groupId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.level).ToArray());
        }

        public static OfficialNationShopCatalog Load(TextAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            OfficialNationShopCatalogData data = JsonUtility.FromJson<OfficialNationShopCatalogData>(source.text);
            Validate(data);
            return new OfficialNationShopCatalog(data);
        }

        public static OfficialNationShopCatalog LoadDefault()
        {
            return Load(Resources.Load<TextAsset>("Commerce/nation-shops"));
        }

        public bool TryGroup(int groupId, out OfficialNationShopGroup group) => groups.TryGetValue(groupId, out group);
        public bool TryShop(int shopId, out OfficialNationShop shop) => shops.TryGetValue(shopId, out shop);
        public bool TryGood(int goodId, out OfficialNationShopGood good) => goods.TryGetValue(goodId, out good);
        public IReadOnlyList<OfficialNationShopLevel> Levels(int groupId) =>
            levels.TryGetValue(groupId, out OfficialNationShopLevel[] rows) ? rows : Array.Empty<OfficialNationShopLevel>();

        public OfficialNationShopLevel LevelForExperience(int groupId, long experience)
        {
            OfficialNationShopLevel[] rows = levels.TryGetValue(groupId, out OfficialNationShopLevel[] found)
                ? found : Array.Empty<OfficialNationShopLevel>();
            return rows.Where(row => row.experienceBoundary <= experience).LastOrDefault();
        }

        public IReadOnlyList<OfficialNationShopGood> Goods(int shopId)
        {
            if (!shops.TryGetValue(shopId, out OfficialNationShop shop)) return Array.Empty<OfficialNationShopGood>();
            return shop.fixedGoods.Select(id => goods[id]).ToArray();
        }

        private static void Validate(OfficialNationShopCatalogData data)
        {
            if (data == null || data.manifest != "871_156598" || data.groups == null || data.levels == null
                || data.shops == null || data.goods == null || data.groups.Length == 0)
                throw new InvalidOperationException("The pinned official nation-shop catalog is incomplete.");
            var groupIds = new HashSet<int>();
            foreach (OfficialNationShopGroup group in data.groups)
                if (group == null || group.groupId <= 0 || group.currencyItemId <= 0 || !groupIds.Add(group.groupId))
                    throw new InvalidOperationException("The official nation-shop group is invalid.");
            var shopIds = new HashSet<int>();
            foreach (OfficialNationShop shop in data.shops)
                if (shop == null || shop.id <= 0 || !shopIds.Add(shop.id) || shop.fixedGoods == null
                    || shop.refreshLimit != 0 || shop.refreshCycleType != 10)
                    throw new InvalidOperationException("The official nation-shop refresh or goods binding is unsupported.");
            var goodsIds = new HashSet<int>();
            foreach (OfficialNationShopGood good in data.goods)
                if (good == null || good.id <= 0 || !goodsIds.Add(good.id) || !shopIds.Contains(good.shopId)
                    || good.itemId <= 0 || good.itemCount <= 0 || good.currencyItemId <= 0 || good.price <= 0
                    || good.inventory < -1 || good.totalBuyLimit < -1 || good.refreshCycleType != 0)
                    throw new InvalidOperationException("The official nation-shop good is invalid.");
            var levelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficialNationShopLevel level in data.levels)
                if (level == null || !groupIds.Contains(level.groupId) || level.level <= 0 || level.shopId <= 0
                    || !shopIds.Contains(level.shopId) || level.experienceBoundary < 0
                    || !levelIds.Add(level.groupId + ":" + level.level))
                    throw new InvalidOperationException("The official nation-shop level is invalid.");
            foreach (OfficialNationShopGroup group in data.groups)
            {
                OfficialNationShopLevel[] rows = data.levels.Where(row => row.groupId == group.groupId)
                    .OrderBy(row => row.level).ToArray();
                if (rows.Length == 0 || rows[0].level != 1 || rows[0].experienceBoundary != 0
                    || rows.Where((row, index) => row.level != index + 1 || index > 0
                        && row.experienceBoundary <= rows[index - 1].experienceBoundary).Any())
                    throw new InvalidOperationException("The official nation-shop level boundaries are incomplete.");
                foreach (OfficialNationShopLevel level in rows)
                {
                    OfficialNationShop shop = data.shops.Single(row => row.id == level.shopId);
                    if (shop.fixedGoods.Length == 0 || shop.fixedGoods.Any(id => !goodsIds.Contains(id)
                        || data.goods.Single(row => row.id == id).shopId != shop.id
                        || data.goods.Single(row => row.id == id).currencyItemId != group.currencyItemId))
                        throw new InvalidOperationException("The official nation-shop goods do not match their group currency.");
                }
            }
            foreach (OfficialNationShop shop in data.shops)
                if (shop.fixedGoods.Distinct().Count() != shop.fixedGoods.Length)
                    throw new InvalidOperationException("The official nation-shop contains duplicate goods.");
        }
    }
}
