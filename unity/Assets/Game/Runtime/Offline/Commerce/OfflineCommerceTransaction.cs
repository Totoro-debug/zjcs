using System;
using System.Collections.Generic;
using System.Globalization;

namespace Zjcs.Offline.Commerce
{
    public enum CommercePaymentMethod
    {
        CashConfirmation,
        Voucher
    }

    public enum CommerceTransactionCode
    {
        Applied,
        InvalidState,
        InvalidRequest,
        StaleQuote,
        SoldOut,
        VoucherInsufficient,
        InvalidRewards,
        PersistenceFailed
    }

    [Serializable]
    public sealed class CommerceAmount
    {
        public int ItemId;
        public long Count;

        public CommerceAmount Clone() => new CommerceAmount { ItemId = ItemId, Count = Count };
    }

    [Serializable]
    public sealed class CommerceCounter
    {
        public string Key;
        public long Count;

        public CommerceCounter Clone() => new CommerceCounter { Key = Key, Count = Count };
    }

    [Serializable]
    public sealed class OfflineCommerceState
    {
        public int Version = 1;
        public int Day;
        public int DailyGroup = 1;
        public int WeeklyGroup = 85;
        public int FirstPurchaseDay = -1;
        public double AccumulatedValue;
        public List<CommerceAmount> Inventory = new List<CommerceAmount>();
        public List<CommerceCounter> Bought = new List<CommerceCounter>();
        public List<CommerceCounter> PaidOrders = new List<CommerceCounter>();
        public List<CommerceCounter> AccumulatedClaims = new List<CommerceCounter>();
        public List<CommerceCounter> FirstClaims = new List<CommerceCounter>();
        public List<CommerceCounter> DailyFreeClaims = new List<CommerceCounter>();

        public OfflineCommerceState Clone()
        {
            var copy = new OfflineCommerceState
            {
                Version = Version,
                Day = Day,
                DailyGroup = DailyGroup,
                WeeklyGroup = WeeklyGroup,
                FirstPurchaseDay = FirstPurchaseDay,
                AccumulatedValue = AccumulatedValue
            };
            Copy(Inventory, copy.Inventory, item => item.Clone());
            Copy(Bought, copy.Bought, item => item.Clone());
            Copy(PaidOrders, copy.PaidOrders, item => item.Clone());
            Copy(AccumulatedClaims, copy.AccumulatedClaims, item => item.Clone());
            Copy(FirstClaims, copy.FirstClaims, item => item.Clone());
            Copy(DailyFreeClaims, copy.DailyFreeClaims, item => item.Clone());
            return copy;
        }

        public static bool IsValid(OfflineCommerceState state)
        {
            return state != null && state.Version == 1 && state.Day >= 0
                && state.DailyGroup > 0 && state.WeeklyGroup > 0
                && state.FirstPurchaseDay >= -1 && state.FirstPurchaseDay <= state.Day
                && !double.IsNaN(state.AccumulatedValue) && !double.IsInfinity(state.AccumulatedValue)
                && state.AccumulatedValue >= 0
                && ValidAmounts(state.Inventory) && ValidCounters(state.Bought)
                && ValidCounters(state.PaidOrders) && ValidCounters(state.AccumulatedClaims)
                && ValidCounters(state.FirstClaims) && ValidCounters(state.DailyFreeClaims);
        }

        private static bool ValidAmounts(List<CommerceAmount> values)
        {
            if (values == null || values.Count > 20000) return false;
            var ids = new HashSet<int>();
            foreach (CommerceAmount item in values)
                if (item == null || item.ItemId <= 0 || item.Count < 0 || !ids.Add(item.ItemId)) return false;
            return true;
        }

        private static bool ValidCounters(List<CommerceCounter> values)
        {
            if (values == null || values.Count > 20000) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CommerceCounter item in values)
                if (item == null || string.IsNullOrEmpty(item.Key) || item.Count < 0
                    || item.Count > 1000000000L || !keys.Add(item.Key)) return false;
            return true;
        }

        private static void Copy<T>(List<T> source, List<T> target, Func<T, T> clone)
        {
            foreach (T item in source) target.Add(clone(item));
        }
    }

    public sealed class CommerceOffer
    {
        public string Kind;
        public int Id;
        public int GroupId;
        public int MoneyCostId;
        public long VoucherCost;
        public int Limit;
        public int Bought;
        public int Period;
        public double AccumulatedValue;
        public bool First;
        public int GreatValuePercent;
        public string PurchaseKey;
        public Dictionary<int, long> Rewards = new Dictionary<int, long>();
        public List<string> BundlePurchaseKeys = new List<string>();

        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Kind) || Id < 0 || GroupId < 0 || MoneyCostId < 0
                || VoucherCost < 0 || Limit < 1 || Bought < 0 || Bought >= Limit
                || string.IsNullOrEmpty(PurchaseKey) || Rewards == null || Rewards.Count == 0)
                return false;
            foreach (KeyValuePair<int, long> reward in Rewards)
                if (reward.Key <= 0 || reward.Value <= 0) return false;
            return true;
        }
    }

    public sealed class CommercePurchaseRequest
    {
        public string Kind;
        public int Id;
        public int GroupId;
        public int ExpectedPeriod;
        public int ExpectedBought;
        public int ExpectedMoneyCostId;
        public long ExpectedVoucherCost;
        public Dictionary<int, long> ExpectedRewards;
        public CommercePaymentMethod PaymentMethod;
    }

    public sealed class CommerceTransactionResult
    {
        public CommerceTransactionCode Code { get; }
        public string Message { get; }
        public IReadOnlyDictionary<int, long> Rewards { get; }

        public CommerceTransactionResult(CommerceTransactionCode code, string message,
            IDictionary<int, long> rewards = null)
        {
            Code = code;
            Message = message;
            Rewards = rewards == null ? new Dictionary<int, long>()
                : new Dictionary<int, long>(rewards);
        }
    }

    public static class OfflineCommerceTransaction
    {
        public static CommerceTransactionResult ActivateFirstPurchase(OfflineCommerceState state,
            int moneyCostId, double accumulatedValue, Func<OfflineCommerceState, bool> persist)
        {
            if (!OfflineCommerceState.IsValid(state))
                return Result(CommerceTransactionCode.InvalidState, "商城存档无效。");
            if (moneyCostId <= 0 || double.IsNaN(accumulatedValue) || double.IsInfinity(accumulatedValue)
                || accumulatedValue < 0 || persist == null)
                return Result(CommerceTransactionCode.InvalidRequest, "首购配置无效。");
            if (state.FirstPurchaseDay >= 0)
                return Result(CommerceTransactionCode.SoldOut, "首购礼包已激活。");

            OfflineCommerceState candidate = state.Clone();
            candidate.FirstPurchaseDay = candidate.Day;
            candidate.AccumulatedValue += accumulatedValue;
            if (double.IsNaN(candidate.AccumulatedValue) || double.IsInfinity(candidate.AccumulatedValue))
                return Result(CommerceTransactionCode.InvalidRewards, "累计星值配置无效。");
            string key = moneyCostId.ToString(CultureInfo.InvariantCulture);
            SetCounter(candidate.PaidOrders, key, Counter(candidate.PaidOrders, key) + 1);
            bool saved;
            try { saved = OfflineCommerceState.IsValid(candidate) && persist(candidate); }
            catch { saved = false; }
            if (!saved) return Result(CommerceTransactionCode.PersistenceFailed, "首购状态未保存。");
            CopyState(candidate, state);
            return Result(CommerceTransactionCode.Applied, "首购礼包已激活，首购奖励现已开放。");
        }

        public static CommercePaymentMethod PaymentMethod(CommerceOffer offer, int voucherItemId,
            OfflineCommerceState state)
        {
            return offer != null && state != null && voucherItemId > 0
                && offer.Kind != "voucher" && offer.VoucherCost > 0
                && Amount(state, voucherItemId) >= offer.VoucherCost
                ? CommercePaymentMethod.Voucher : CommercePaymentMethod.CashConfirmation;
        }

        public static CommerceTransactionResult Purchase(OfflineCommerceState state,
            CommercePurchaseRequest request, int voucherItemId,
            Func<OfflineCommerceState, CommercePurchaseRequest, CommerceOffer> requote,
            Func<OfflineCommerceState, bool> persist)
        {
            if (!OfflineCommerceState.IsValid(state))
                return Result(CommerceTransactionCode.InvalidState, "商城存档无效。");
            if (request == null || voucherItemId <= 0 || requote == null || persist == null
                || request.Id < 0 || request.GroupId < 0)
                return Result(CommerceTransactionCode.InvalidRequest, "购买请求无效。");

            OfflineCommerceState candidate = state.Clone();
            CommerceOffer offer = requote(candidate, request);
            if (offer == null || !offer.IsValid())
                return Result(CommerceTransactionCode.SoldOut, "商品不存在、未解锁或已售罄。");
            if (offer.Kind != request.Kind || offer.Id != request.Id || offer.GroupId != request.GroupId
                || offer.Period != request.ExpectedPeriod || offer.Bought != request.ExpectedBought
                || offer.MoneyCostId != request.ExpectedMoneyCostId
                || offer.VoucherCost != request.ExpectedVoucherCost
                || !SameRewards(offer.Rewards, request.ExpectedRewards)
                || offer.Bought >= offer.Limit
                || ((offer.Kind == "daily" || offer.Kind == "all") && offer.Period != candidate.Day)
                || (offer.Kind == "weekly" && offer.Period != Period("weekly", candidate.Day))
                || ((offer.Kind == "daily" || offer.Kind == "all") && candidate.DailyGroup != offer.GroupId)
                || (offer.Kind == "weekly" && candidate.WeeklyGroup != offer.GroupId))
                return Result(CommerceTransactionCode.StaleQuote, "商品状态已变化，请重新确认。");

            CommercePaymentMethod availableMethod = PaymentMethod(offer, voucherItemId, candidate);
            if (!Enum.IsDefined(typeof(CommercePaymentMethod), request.PaymentMethod)
                || request.PaymentMethod == CommercePaymentMethod.CashConfirmation
                    && availableMethod == CommercePaymentMethod.Voucher)
                return Result(CommerceTransactionCode.StaleQuote, "支付方式已变化，请重新确认。");
            if (request.PaymentMethod == CommercePaymentMethod.Voucher)
            {
                if (offer.Kind == "voucher" || offer.VoucherCost <= 0)
                    return Result(CommerceTransactionCode.InvalidRequest, "请选择有效的购买方式。");
                if (Amount(candidate, voucherItemId) < offer.VoucherCost)
                    return Result(CommerceTransactionCode.VoucherInsufficient, "代金券不足。");
                SetAmount(candidate, voucherItemId, Amount(candidate, voucherItemId) - offer.VoucherCost);
            }

            foreach (KeyValuePair<int, long> reward in offer.Rewards)
                if (reward.Key <= 0 || reward.Value <= 0
                    || Amount(candidate, reward.Key) > long.MaxValue - reward.Value)
                    return Result(CommerceTransactionCode.InvalidRewards, "商品奖励配置无效。");
            foreach (KeyValuePair<int, long> reward in offer.Rewards)
                SetAmount(candidate, reward.Key, Amount(candidate, reward.Key) + reward.Value);

            SetCounter(candidate.Bought, offer.PurchaseKey, offer.Bought + 1);
            if (offer.Kind == "all" && offer.BundlePurchaseKeys != null)
                foreach (string key in offer.BundlePurchaseKeys)
                    if (!string.IsNullOrEmpty(key)) SetCounter(candidate.Bought, key, 1);

            if (offer.Kind != "voucher")
            {
                if (offer.MoneyCostId > 0)
                    SetCounter(candidate.PaidOrders, offer.MoneyCostId.ToString(CultureInfo.InvariantCulture),
                        Counter(candidate.PaidOrders, offer.MoneyCostId.ToString(CultureInfo.InvariantCulture)) + 1);
                candidate.AccumulatedValue += offer.AccumulatedValue;
                if (double.IsNaN(candidate.AccumulatedValue) || double.IsInfinity(candidate.AccumulatedValue))
                    return Result(CommerceTransactionCode.InvalidRewards, "累计星值配置无效。");
            }

            bool saved;
            try { saved = OfflineCommerceState.IsValid(candidate) && persist(candidate); }
            catch { saved = false; }
            if (!saved)
                return Result(CommerceTransactionCode.PersistenceFailed, "购买未保存，奖励已回滚。");
            CopyState(candidate, state);
            return Result(CommerceTransactionCode.Applied, "购买成功，奖励已入库。", offer.Rewards);
        }

        public static int Period(string kind, int day)
        {
            return kind == "weekly" ? day / 7 : day;
        }

        public static CommerceTransactionResult Claim(OfflineCommerceState state, string key,
            IDictionary<int, long> rewards, bool firstPurchase, Func<OfflineCommerceState, bool> persist)
        {
            if (!OfflineCommerceState.IsValid(state))
                return Result(CommerceTransactionCode.InvalidState, "商城存档无效。");
            if (string.IsNullOrWhiteSpace(key) || rewards == null || rewards.Count == 0 || persist == null)
                return Result(CommerceTransactionCode.InvalidRequest, "领取请求无效。");

            OfflineCommerceState candidate = state.Clone();
            List<CommerceCounter> claims = firstPurchase ? candidate.FirstClaims : candidate.AccumulatedClaims;
            if (Counter(claims, key) > 0)
                return Result(CommerceTransactionCode.StaleQuote, "奖励已领取。");
            if (!AddRewards(candidate, rewards) || !ValidRewards(rewards))
                return Result(CommerceTransactionCode.InvalidRewards, "奖励配置无效。");
            SetCounter(claims, key, 1);

            bool saved;
            try { saved = OfflineCommerceState.IsValid(candidate) && persist(candidate); }
            catch { saved = false; }
            if (!saved) return Result(CommerceTransactionCode.PersistenceFailed, "领取未保存，奖励已回滚。");
            CopyState(candidate, state);
            return Result(CommerceTransactionCode.Applied, "奖励已领取并保存。", rewards);
        }

        /// <summary>
        /// Applies one self-contained commerce mutation (free gifts, exchange) with the same
        /// candidate/persist/rollback guarantee as purchases and claims.
        /// </summary>
        public static CommerceTransactionResult Mutate(OfflineCommerceState state,
            Action<OfflineCommerceState> mutation, IDictionary<int, long> rewards, string message,
            Func<OfflineCommerceState, bool> persist)
        {
            if (!OfflineCommerceState.IsValid(state))
                return Result(CommerceTransactionCode.InvalidState, "商城存档无效。");
            if (mutation == null || persist == null || rewards == null || rewards.Count == 0)
                return Result(CommerceTransactionCode.InvalidRequest, "操作请求无效。");
            if (!ValidRewards(rewards))
                return Result(CommerceTransactionCode.InvalidRewards, "奖励配置无效。");

            OfflineCommerceState candidate = state.Clone();
            try { mutation(candidate); }
            catch (Exception error)
            {
                return Result(CommerceTransactionCode.InvalidRequest, error.Message);
            }
            if (!OfflineCommerceState.IsValid(candidate))
                return Result(CommerceTransactionCode.InvalidRewards, "操作结果无效。");
            bool saved;
            try { saved = persist(candidate); }
            catch { saved = false; }
            if (!saved) return Result(CommerceTransactionCode.PersistenceFailed, "操作未保存，状态已回滚。");
            CopyState(candidate, state);
            return Result(CommerceTransactionCode.Applied, message, rewards);
        }

        public static bool SameRewards(IDictionary<int, long> left, IDictionary<int, long> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            foreach (KeyValuePair<int, long> item in left)
                if (!right.TryGetValue(item.Key, out long count) || count != item.Value) return false;
            return true;
        }

        private static bool ValidRewards(IDictionary<int, long> rewards)
        {
            foreach (KeyValuePair<int, long> reward in rewards)
                if (reward.Key <= 0 || reward.Value <= 0) return false;
            return true;
        }

        private static bool AddRewards(OfflineCommerceState state, IDictionary<int, long> rewards)
        {
            foreach (KeyValuePair<int, long> reward in rewards)
                if (reward.Key <= 0 || reward.Value <= 0
                    || Amount(state, reward.Key) > long.MaxValue - reward.Value) return false;
            foreach (KeyValuePair<int, long> reward in rewards)
                SetAmount(state, reward.Key, Amount(state, reward.Key) + reward.Value);
            return true;
        }

        private static long Amount(OfflineCommerceState state, int itemId)
        {
            foreach (CommerceAmount item in state.Inventory)
                if (item.ItemId == itemId) return item.Count;
            return 0;
        }

        private static void SetAmount(OfflineCommerceState state, int itemId, long count)
        {
            foreach (CommerceAmount item in state.Inventory)
                if (item.ItemId == itemId) { item.Count = count; return; }
            state.Inventory.Add(new CommerceAmount { ItemId = itemId, Count = count });
        }

        private static long Counter(List<CommerceCounter> values, string key)
        {
            foreach (CommerceCounter item in values)
                if (item.Key == key) return item.Count;
            return 0;
        }

        private static void SetCounter(List<CommerceCounter> values, string key, long count)
        {
            foreach (CommerceCounter item in values)
                if (item.Key == key) { item.Count = count; return; }
            values.Add(new CommerceCounter { Key = key, Count = count });
        }

        private static void CopyState(OfflineCommerceState source, OfflineCommerceState target)
        {
            target.Day = source.Day;
            target.DailyGroup = source.DailyGroup;
            target.WeeklyGroup = source.WeeklyGroup;
            target.FirstPurchaseDay = source.FirstPurchaseDay;
            target.AccumulatedValue = source.AccumulatedValue;
            target.Inventory = source.Inventory;
            target.Bought = source.Bought;
            target.PaidOrders = source.PaidOrders;
            target.AccumulatedClaims = source.AccumulatedClaims;
            target.FirstClaims = source.FirstClaims;
            target.DailyFreeClaims = source.DailyFreeClaims;
        }

        private static CommerceTransactionResult Result(CommerceTransactionCode code, string message)
        {
            return new CommerceTransactionResult(code, message);
        }

        private static CommerceTransactionResult Result(CommerceTransactionCode code, string message,
            IDictionary<int, long> rewards)
        {
            return new CommerceTransactionResult(code, message, rewards);
        }
    }
}
