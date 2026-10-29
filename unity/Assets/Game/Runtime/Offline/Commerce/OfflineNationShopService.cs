using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Commerce
{
    public enum NationShopPurchaseCode
    {
        Applied,
        InvalidRequest,
        Locked,
        SoldOut,
        CurrencyInsufficient,
        PersistenceFailed
    }

    public sealed class NationShopPurchaseResult
    {
        public NationShopPurchaseCode Code { get; }
        public long Experience { get; }
        public int Level { get; }
        public string Message { get; }

        internal NationShopPurchaseResult(NationShopPurchaseCode code, long experience, int level, string message)
        { Code = code; Experience = experience; Level = level; Message = message; }
    }

    public sealed class OfflineNationShopService
    {
        private const string ExperiencePrefix = "nation-shop-exp:";
        private const string GoodsPrefix = "nation-shop-good:";
        private readonly OfficialNationShopCatalog catalog;
        private readonly OfflineCommerceSession session;

        public OfflineNationShopService(OfficialNationShopCatalog catalog, OfflineCommerceSession session)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public long Experience(int groupId) => Counter(session.State.Bought, ExperienceKey(groupId));

        public int Level(int groupId) => catalog.LevelForExperience(groupId, Experience(groupId))?.level ?? 0;

        public int CurrentShopId(int groupId) => catalog.LevelForExperience(groupId, Experience(groupId))?.shopId ?? 0;

        public int PurchasedCount(int goodId) => checked((int)Counter(session.State.Bought, GoodsKey(goodId)));

        public int RemainingStock(OfficialNationShopGood good)
        {
            if (good == null) throw new ArgumentNullException(nameof(good));
            int bought = PurchasedCount(good.id);
            if (good.totalBuyLimit >= 0) return Math.Max(0, Math.Min(good.inventory < 0
                ? good.totalBuyLimit : good.inventory, good.totalBuyLimit) - bought);
            return good.inventory < 0 ? -1 : Math.Max(0, good.inventory - bought);
        }

        public NationShopPurchaseResult Purchase(int groupId, int goodId, int quantity = 1)
        {
            OfficialNationShopLevel level = catalog.LevelForExperience(groupId, Experience(groupId));
            if (!catalog.TryGroup(groupId, out OfficialNationShopGroup group) || quantity <= 0)
                return Result(NationShopPurchaseCode.InvalidRequest, groupId, "购买请求无效。");
            if (level == null || !catalog.TryGood(goodId, out OfficialNationShopGood good)
                || good.shopId != level.shopId || !catalog.TryShop(level.shopId, out OfficialNationShop shop)
                || !shop.fixedGoods.Contains(goodId))
                return Result(NationShopPurchaseCode.Locked, groupId, "该商品尚未解锁。");
            if (quantity > 1 && !good.supportsMultiple)
                return Result(NationShopPurchaseCode.InvalidRequest, groupId, "该商品不支持批量购买。");

            long cost;
            long reward;
            long nextExperience;
            try
            {
                cost = checked(good.price * quantity);
                reward = checked(good.itemCount * quantity);
                nextExperience = checked(Experience(groupId) + cost);
            }
            catch (OverflowException)
            {
                return Result(NationShopPurchaseCode.InvalidRequest, groupId, "购买数量超出上限。");
            }
            if (good.currencyItemId != group.currencyItemId)
                return Result(NationShopPurchaseCode.InvalidRequest, groupId, "商品货币与国度商店配置不一致。");
            int remaining = RemainingStock(good);
            if (remaining >= 0 && quantity > remaining)
                return Result(NationShopPurchaseCode.SoldOut, groupId, "商品库存不足。");
            if (Amount(session.State, group.currencyItemId) < cost)
                return Result(NationShopPurchaseCode.CurrencyInsufficient, groupId, "专属货币不足。");
            if (Amount(session.State, good.itemId) > long.MaxValue - reward)
                return Result(NationShopPurchaseCode.InvalidRequest, groupId, "商品数量超出存档上限。");

            int previousLevel = level.level;
            int nextLevel = catalog.LevelForExperience(groupId, nextExperience)?.level ?? previousLevel;
            var candidate = session.State.Clone();
            SetAmount(candidate, group.currencyItemId, Amount(candidate, group.currencyItemId) - cost);
            SetAmount(candidate, good.itemId, Amount(candidate, good.itemId) + reward);
            SetCounter(candidate.Bought, GoodsKey(good.id), checked(Counter(candidate.Bought, GoodsKey(good.id)) + quantity));
            SetCounter(candidate.Bought, ExperienceKey(groupId), nextExperience);
            if (!session.Persist(candidate))
                return Result(NationShopPurchaseCode.PersistenceFailed, groupId, "购买未保存，商品和货币均已回滚。");
            CopyState(candidate, session.State);
            return new NationShopPurchaseResult(NationShopPurchaseCode.Applied, nextExperience, nextLevel,
                nextLevel > previousLevel ? "购买成功，国度商店已升阶。" : "购买成功。");
        }

        private NationShopPurchaseResult Result(NationShopPurchaseCode code, int groupId, string message) =>
            new NationShopPurchaseResult(code, Experience(groupId), Level(groupId), message);

        private static long Amount(OfflineCommerceState state, int itemId)
        {
            CommerceAmount item = state.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            return item?.Count ?? 0;
        }

        private static void SetAmount(OfflineCommerceState state, int itemId, long count)
        {
            CommerceAmount item = state.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (item == null) state.Inventory.Add(new CommerceAmount { ItemId = itemId, Count = count });
            else item.Count = count;
        }

        private static long Counter(List<CommerceCounter> counters, string key) =>
            counters.FirstOrDefault(value => value.Key == key)?.Count ?? 0L;

        private static void SetCounter(List<CommerceCounter> counters, string key, long count)
        {
            CommerceCounter value = counters.FirstOrDefault(item => item.Key == key);
            if (value == null) counters.Add(new CommerceCounter { Key = key, Count = count });
            else value.Count = count;
        }

        private static string ExperienceKey(int groupId) => ExperiencePrefix + groupId.ToString(CultureInfo.InvariantCulture);
        private static string GoodsKey(int goodId) => GoodsPrefix + goodId.ToString(CultureInfo.InvariantCulture);

        private static void CopyState(OfflineCommerceState source, OfflineCommerceState target)
        {
            target.Day = source.Day;
            target.DailyGroup = source.DailyGroup;
            target.WeeklyGroup = source.WeeklyGroup;
            target.FirstPurchaseDay = source.FirstPurchaseDay;
            target.AccumulatedValue = source.AccumulatedValue;
            Replace(target.Inventory, source.Inventory, item => item.Clone());
            Replace(target.Bought, source.Bought, item => item.Clone());
            Replace(target.PaidOrders, source.PaidOrders, item => item.Clone());
            Replace(target.AccumulatedClaims, source.AccumulatedClaims, item => item.Clone());
            Replace(target.FirstClaims, source.FirstClaims, item => item.Clone());
            Replace(target.DailyFreeClaims, source.DailyFreeClaims, item => item.Clone());
        }

        private static void Replace<T>(List<T> target, IEnumerable<T> source, Func<T, T> clone)
        {
            target.Clear();
            target.AddRange(source.Select(clone));
        }
    }
}
