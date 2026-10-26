using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Offline.Commerce
{
    [Serializable]
    public sealed class OfflineCommerceSave
    {
        public int Version = 1;
        public OfflineCommerceState State = new OfflineCommerceState();
    }

    /// <summary>
    /// Owns the commerce state embedded in the profile save. The main profile inventory is
    /// authoritative: opening reconciles the commerce mirror with it, and every commit writes
    /// only the signed inventory delta so other features' latest changes are preserved.
    /// </summary>
    public sealed class OfflineCommerceSession
    {
        private static readonly DateTime DayZero = new DateTime(2025, 12, 1, 8, 0, 0, DateTimeKind.Local);
        private readonly ProfileSlotCatalog slots;
        private readonly string slotId;
        private readonly OfflineMainlineProgressSession profile;

        public OfflineCommerceState State { get; private set; }

        private OfflineCommerceSession(ProfileSlotCatalog slots, string slotId, OfflineCommerceState state,
            OfflineMainlineProgressSession profile)
        {
            this.slots = slots;
            this.slotId = slotId;
            this.profile = profile;
            State = state;
        }

        public static OfflineCommerceSession Open(ProfileSlotCatalog slots, string slotId, DateTime localNow,
            OfflineMainlineProgressSession profile)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile slot does not exist.");

            OfflineCommerceSave save = null;
            string embedded = profile.Save.Player.CommerceStateJson;
            if (!string.IsNullOrEmpty(embedded)) save = JsonUtility.FromJson<OfflineCommerceSave>(embedded);
            if (save == null)
            {
                SaveReadResult legacy = LocalJsonSaveFile.Read(slots.GetCommercePath(slotId), IsValidJson);
                if (legacy.Status == SaveReadStatus.Corrupt)
                    throw new InvalidDataException("The legacy commerce save is corrupt.");
                save = legacy.Json == null ? new OfflineCommerceSave() : JsonUtility.FromJson<OfflineCommerceSave>(legacy.Json);
            }
            if (save == null || save.Version != 1 || !OfflineCommerceState.IsValid(save.State))
                throw new InvalidDataException("The commerce save is invalid or unsupported.");

            var session = new OfflineCommerceSession(slots, slotId, save.State, profile);
            // The main profile inventory is authoritative. Mirror it before the first commit
            // so a legacy commerce snapshot can never add to or deduct from the profile.
            session.State.Inventory = session.MainInventory();
            if (!session.Reconcile(localNow))
                throw new IOException("The commerce save could not be reconciled with the profile.");
            return session;
        }

        public static IReadOnlyDictionary<int, long> ActivePeriodicProperties(
            OfficialCommerceCatalogView catalog, OfflineCommerceState state)
        {
            var properties = new Dictionary<int, long>();
            if (catalog?.Data?.periodicCards == null || state?.Bought == null) return properties;
            foreach (CommercePeriodicCard card in catalog.Data.periodicCards)
            {
                if (card?.extraProps == null || ReadCounter(state.Bought, "periodic-expiry:" + card.id) <= state.Day)
                    continue;
                foreach (CommerceExtraProp prop in card.extraProps)
                    if (prop != null && prop.propId > 0 && prop.value != 0)
                        properties[prop.propId] = properties.TryGetValue(prop.propId, out long current)
                            ? checked(current + prop.value) : prop.value;
            }
            return properties;
        }

        private static long ReadCounter(List<CommerceCounter> counters, string key)
        {
            CommerceCounter counter = counters.FirstOrDefault(value => value.Key == key);
            return counter == null ? 0L : counter.Count;
        }

        /// <summary>
        /// Advances the local day checkpoint and mirrors the main profile inventory into the
        /// commerce state. Returns false when the resulting state could not be saved.
        /// </summary>
        public bool Advance(DateTime localNow)
        {
            return Reconcile(localNow);
        }

        private bool Reconcile(DateTime localNow)
        {
            OfflineCommerceState candidate = State.Clone();
            bool changed = false;
            int today = Today(localNow);
            if (today > candidate.Day)
            {
                candidate.Day = today;
                changed = true;
            }
            try { profile.Reload(); } catch { }
            List<CommerceAmount> authoritative = MainInventory();
            if (!SameInventory(candidate.Inventory, authoritative))
            {
                candidate.Inventory = authoritative;
                changed = true;
            }
            if (!changed) return true;
            if (!PersistState(candidate, false)) return false;
            State = candidate;
            return true;
        }

        private int Today(DateTime localNow)
        {
            return Math.Max(0, (int)Math.Floor((localNow.ToLocalTime() - DayZero).TotalDays));
        }

        private List<CommerceAmount> MainInventory()
        {
            var result = new List<CommerceAmount>();
            foreach (OfflineItemStack item in profile.Save.Player.Inventory)
                if (item != null && item.ItemId > 0 && item.Count > 0)
                    result.Add(new CommerceAmount { ItemId = item.ItemId, Count = item.Count });
            return result;
        }

        private static bool SameInventory(List<CommerceAmount> left, List<CommerceAmount> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            var values = new Dictionary<int, long>();
            foreach (CommerceAmount item in left) values[item.ItemId] = item.Count;
            foreach (CommerceAmount item in right)
                if (!values.TryGetValue(item.ItemId, out long count) || count != item.Count) return false;
            return true;
        }

        public bool Persist(OfflineCommerceState candidate) => PersistState(candidate, true);

        public bool PersistState(OfflineCommerceState candidate, bool applyInventoryDelta)
        {
            if (!slots.Contains(slotId) || !OfflineCommerceState.IsValid(candidate)) return false;
            try
            {
                // Another feature may have written the profile since this session opened.
                profile.Reload();
                Dictionary<int, long> delta = applyInventoryDelta
                    ? InventoryDelta(State.Inventory, candidate.Inventory)
                    : new Dictionary<int, long>();
                OfflineCommerceState persisted = null;
                string eventId = "commerce:" + Guid.NewGuid().ToString("N");
                ProgressionResult result = profile.ApplyInventoryTransaction(eventId, mainline =>
                {
                    ApplyDelta(mainline.Player, delta);
                    var final = candidate.Clone();
                    final.Inventory = ToAmounts(mainline.Player.Inventory);
                    SyncPeriodicCharacterProperties(mainline.Player.CharacterProperties, final);
                    mainline.Player.CommerceStateJson = JsonUtility.ToJson(new OfflineCommerceSave { State = final });
                    persisted = final;
                });
                if (result == null || !result.IsApplied || persisted == null) return false;
                // Keep the caller's candidate consistent with the committed main inventory so
                // transaction copies never resurrect stale balances.
                candidate.Inventory = persisted.Inventory;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool UpdateState(Action<OfflineCommerceState> mutation)
        {
            if (mutation == null) return false;
            OfflineCommerceState candidate = State.Clone();
            try { mutation(candidate); }
            catch { return false; }
            if (!PersistState(candidate, false)) return false;
            State = candidate;
            return true;
        }

        private static void SyncPeriodicCharacterProperties(OfflineCharacterPropertyProgress properties,
            OfflineCommerceState commerce)
        {
            OfflineCharacterPropertyProgress.Normalize(properties);
            properties.PeriodicCards.Clear();
            const string prefix = "periodic-expiry:";
            foreach (CommerceCounter counter in commerce.Bought)
            {
                if (counter == null || !counter.Key.StartsWith(prefix, StringComparison.Ordinal)
                    || !int.TryParse(counter.Key.Substring(prefix.Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int cardId)
                    || cardId <= 0 || counter.Count <= commerce.Day) continue;
                long expiresAt = new DateTimeOffset(DayZero.AddDays(counter.Count)).ToUnixTimeSeconds();
                properties.PeriodicCards.Add(new OfflinePeriodicCard
                {
                    CardId = cardId,
                    ExpiresAtUnixSeconds = expiresAt
                });
            }
        }

        private static Dictionary<int, long> InventoryDelta(List<CommerceAmount> previous,
            List<CommerceAmount> next)
        {
            var before = new Dictionary<int, long>();
            foreach (CommerceAmount item in previous) before[item.ItemId] = item.Count;
            var after = new Dictionary<int, long>();
            foreach (CommerceAmount item in next) after[item.ItemId] = item.Count;
            var delta = new Dictionary<int, long>();
            foreach (KeyValuePair<int, long> item in before)
            {
                after.TryGetValue(item.Key, out long count);
                if (count != item.Value) delta[item.Key] = count - item.Value;
            }
            foreach (KeyValuePair<int, long> item in after)
                if (!before.ContainsKey(item.Key) && item.Value != 0) delta[item.Key] = item.Value;
            return delta;
        }

        private static List<CommerceAmount> ToAmounts(List<OfflineItemStack> inventory)
        {
            var result = new List<CommerceAmount>();
            foreach (OfflineItemStack item in inventory)
                if (item != null && item.ItemId > 0 && item.Count > 0)
                    result.Add(new CommerceAmount { ItemId = item.ItemId, Count = item.Count });
            return result;
        }

        private static void ApplyDelta(OfflinePlayerProgress player, Dictionary<int, long> delta)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (KeyValuePair<int, long> item in delta.OrderBy(value => value.Key))
            {
                if (item.Value > 0)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(
                        player, item.Key, item.Value, now);
                    if (!granted.IsApplied)
                        throw new InvalidDataException("The commerce inventory grant was rejected.");
                }
                else if (item.Value < 0)
                {
                    InventoryOperationResult consumed = OfflineInventoryOperations.Consume(
                        player, item.Key, -item.Value, now);
                    if (!consumed.IsApplied)
                        throw new InvalidDataException("The commerce inventory deduction was rejected.");
                }
            }
        }

        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 2_000_000) return false;
            try
            {
                OfflineCommerceSave save = JsonUtility.FromJson<OfflineCommerceSave>(json);
                return save != null && save.Version == 1 && OfflineCommerceState.IsValid(save.State);
            }
            catch { return false; }
        }
    }

    public sealed class OfflineCommerceOffers
    {
        private readonly OfficialCommerceCatalogView catalog;
        private readonly int voucherItemId;
        private readonly OfflineCommerceSession session;

        public OfflineCommerceOffers(OfficialCommerceCatalogView catalog, int voucherItemId,
            OfflineCommerceSession session)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.voucherItemId = voucherItemId;
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public OfficialCommerceCatalogView Catalog => catalog;
        public OfflineCommerceSession Session => session;

        public CommerceOffer Quote(OfflineCommerceState state, CommercePurchaseRequest request)
        {
            if (state == null || request == null) return null;
            switch (request.Kind)
            {
                case "direct": return Direct(request.Id, state);
                case "voucher": return Voucher(request.Id, state);
                case "daily": return Daily(request.Id, request.GroupId, false, state);
                case "all": return Daily(request.Id, request.GroupId, true, state);
                case "weekly": return Weekly(request.Id, request.GroupId, state);
                case "periodic": return Periodic(request.Id, state);
                default: return null;
            }
        }

        public CommerceOffer Quote(string kind, int id, int groupId)
        {
            return Quote(session.State, new CommercePurchaseRequest { Kind = kind, Id = id, GroupId = groupId });
        }

        public CommerceOffer Direct(int id)
        {
            return Direct(id, session.State);
        }

        public CommerceOffer Periodic(int id) => Periodic(id, session.State);

        private CommerceOffer Periodic(int id, OfflineCommerceState state)
        {
            CommercePeriodicCard row = (catalog.Data.periodicCards ?? Array.Empty<CommercePeriodicCard>())
                .FirstOrDefault(value => value.id == id);
            if (row == null) return null;
            CommerceMoneyCost cost = catalog.MoneyCost(row.moneyCostId);
            if (cost == null) return null;
            long expiry = Counter(state.Bought, "periodic-expiry:" + id);
            int purchased = (int)Math.Min(int.MaxValue, Counter(state.Bought, "periodic-buy:" + id));
            return new CommerceOffer
            {
                Kind = "periodic", Id = id, GroupId = 0, MoneyCostId = row.moneyCostId,
                VoucherCost = cost.voucherCost, Limit = int.MaxValue, Bought = purchased,
                Period = state.Day, PurchaseKey = "periodic-buy:" + id,
                Rewards = ToDictionary(row.fixedAward), AccumulatedValue = cost.startValue,
                First = purchased == 0, GreatValuePercent = row.discount,
            };
        }

        public int PeriodicExpiryDay(int id) => (int)Math.Min(int.MaxValue,
            Counter(session.State.Bought, "periodic-expiry:" + id));

        public bool PeriodicHasNewMark(int id)
        {
            bool active = PeriodicExpiryDay(id) > session.State.Day;
            bool hasRecord = session.State.Bought.Any(value => value.Key == "periodic-seen:" + id);
            long lastView = Counter(session.State.Bought, "periodic-seen:" + id);
            return !active && (!hasRecord || lastView > 0);
        }

        public bool HasPeriodicCardNotice() => (catalog.Data.periodicCards ?? Array.Empty<CommercePeriodicCard>())
            .Any(card => PeriodicHasNewMark(card.id) || PeriodicCanClaim(card.id));

        public bool RecordPeriodicCardsSeen()
        {
            return session.UpdateState(candidate =>
            {
                foreach (CommercePeriodicCard card in catalog.Data.periodicCards ?? Array.Empty<CommercePeriodicCard>())
                {
                    long active = Counter(candidate.Bought, "periodic-expiry:" + card.id) > candidate.Day ? 1L : 0L;
                    SetCounter(candidate.Bought, "periodic-seen:" + card.id, active);
                }
            });
        }

        public bool PeriodicCanClaim(int id) => PeriodicExpiryDay(id) > session.State.Day
            && Counter(session.State.Bought, "periodic-claim:" + id + ":" + session.State.Day) == 0;

        public CommerceTransactionResult ClaimPeriodic(int id)
        {
            CommercePeriodicCard row = (catalog.Data.periodicCards ?? Array.Empty<CommercePeriodicCard>())
                .FirstOrDefault(value => value.id == id);
            if (row == null || !PeriodicCanClaim(id))
                return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "周期卡已过期或今日奖励已领取。");
            Dictionary<int, long> rewards = ToDictionary(row.periodicAward);
            return OfflineCommerceTransaction.Mutate(session.State, candidate =>
            {
                foreach (KeyValuePair<int, long> reward in rewards)
                    SetAmount(candidate, reward.Key, checked(Amount(candidate, reward.Key) + reward.Value));
                SetCounter(candidate.Bought, "periodic-claim:" + id + ":" + candidate.Day, 1);
            }, rewards, "每日奖励已领取。", session.Persist);
        }

        private CommerceOffer Direct(int id, OfflineCommerceState state)
        {
            CommerceDirectPay row = catalog.Data.directPay.FirstOrDefault(value => value.id == id);
            if (row == null) return null;
            long bought = Counter(state.Bought, "direct:" + id);
            CommerceMoneyCost cost = catalog.MoneyCost(row.moneyCostId);
            bool first = bought == 0;
            var awards = Merge(row.fixedAward, first ? row.firstExtraAward : row.repeatExtraAward);
            return new CommerceOffer
            {
                Kind = "direct", Id = row.id, GroupId = 0, MoneyCostId = row.moneyCostId,
                VoucherCost = cost == null ? 0 : cost.voucherCost, Limit = int.MaxValue,
                Bought = (int)Math.Min(int.MaxValue, bought), Period = 0, PurchaseKey = "direct:" + id,
                Rewards = awards, AccumulatedValue = cost == null ? 0 : cost.startValue,
                First = first, GreatValuePercent = first ? 0 : row.repeatExtraPercent
            };
        }

        public CommerceOffer Voucher(int id)
        {
            return Voucher(id, session.State);
        }

        private CommerceOffer Voucher(int id, OfflineCommerceState state)
        {
            CommerceVoucher row = catalog.Data.vouchers.FirstOrDefault(value => value.id == id);
            CommerceMoneyCost cost = row == null ? null : catalog.MoneyCost(row.moneyCostId);
            if (row == null || cost == null || cost.voucherCost <= 0) return null;
            return new CommerceOffer
            {
                Kind = "voucher", Id = row.id, GroupId = 0, MoneyCostId = row.moneyCostId,
                VoucherCost = 0, Limit = int.MaxValue, Bought = 0, Period = 0,
                PurchaseKey = "voucher:" + id,
                Rewards = new Dictionary<int, long> { [voucherItemId] = cost.voucherCost },
                AccumulatedValue = 0, First = false
            };
        }

        public CommerceOffer Daily(int id, int groupId, bool all)
        {
            return Daily(id, groupId, all, session.State);
        }

        private CommerceOffer Daily(int id, int groupId, bool all, OfflineCommerceState state)
        {
            if (groupId != state.DailyGroup) return null;
            CommerceGift[] rows = catalog.Data.dailyGifts.Where(value => value.groupId == groupId)
                .OrderBy(value => value.id).ToArray();
            if (rows.Length == 0) return null;
            CommerceGift row = all ? null : rows.FirstOrDefault(value => value.id == id);
            if (!all && row == null) return null;
            CommerceDailyGroup group = catalog.Data.dailyGroups.FirstOrDefault(value => value.groupId == groupId);
            if (group == null) return null;
            int period = state.Day;
            long boughtAll = Counter(state.Bought, "daily-all:" + period + ":" + groupId);
            if (!all)
            {
                // Official DailyPayGiftCanBuy: buying the whole group blocks every single tier.
                if (boughtAll > 0) return null;
                return GiftOffer("daily", row, groupId, period,
                    "daily:" + period + ":" + groupId + ":" + id, state);
            }

            if (boughtAll > 0) return null;
            var rewards = new Dictionary<int, long>();
            long voucherCost = 0;
            foreach (CommerceGift gift in rows)
            {
                if (Counter(state.Bought, "daily:" + period + ":" + groupId + ":" + gift.id) > 0) return null;
                CommerceMoneyCost moneyCost = catalog.MoneyCost(gift.moneyCostId);
                if (moneyCost == null) return null;
                voucherCost = checked(voucherCost + moneyCost.voucherCost);
                foreach (CommerceReward reward in gift.rewards)
                    Add(rewards, reward.itemId, reward.count);
            }
            CommerceMoneyCost bundleCost = catalog.MoneyCost(group.bundleMoneyCostId);
            if (bundleCost == null) return null;
            return new CommerceOffer
            {
                Kind = "all", Id = 0, GroupId = groupId, MoneyCostId = group.bundleMoneyCostId,
                VoucherCost = bundleCost.voucherCost, Limit = 1, Bought = 0, Period = period,
                PurchaseKey = "daily-all:" + period + ":" + groupId, Rewards = rewards,
                AccumulatedValue = bundleCost.startValue
            };
        }

        public CommerceOffer Weekly(int id, int groupId)
        {
            return Weekly(id, groupId, session.State);
        }

        private CommerceOffer Weekly(int id, int groupId, OfflineCommerceState state)
        {
            if (groupId != state.WeeklyGroup) return null;
            CommerceGift row = catalog.Data.weeklyGifts.FirstOrDefault(value => value.groupId == groupId && value.id == id);
            if (row == null) return null;
            int period = OfflineCommerceTransaction.Period("weekly", state.Day);
            return GiftOffer("weekly", row, groupId, period,
                "weekly:" + period + ":" + groupId + ":" + id, state);
        }

        /// <summary>Official weekly ordering: available tiers first, then ascending gift id.</summary>
        public IReadOnlyList<CommerceGift> WeeklyGifts()
        {
            int groupId = session.State.WeeklyGroup;
            return catalog.Data.weeklyGifts.Where(value => value.groupId == groupId)
                .OrderBy(value => IsWeeklySoldOut(value) ? 1 : 0)
                .ThenBy(value => value.id)
                .ToArray();
        }

        public bool IsWeeklySoldOut(CommerceGift gift)
        {
            int period = OfflineCommerceTransaction.Period("weekly", session.State.Day);
            long bought = Counter(session.State.Bought,
                "weekly:" + period + ":" + gift.groupId + ":" + gift.id);
            return bought >= Math.Max(1, gift.limit);
        }

        public int WeeklyRemaining(CommerceGift gift)
        {
            int period = OfflineCommerceTransaction.Period("weekly", session.State.Day);
            long bought = Counter(session.State.Bought,
                "weekly:" + period + ":" + gift.groupId + ":" + gift.id);
            return (int)Math.Max(0, Math.Max(1, gift.limit) - bought);
        }

        public int DailyRemaining(int giftId)
        {
            long bought = Counter(session.State.Bought,
                "daily:" + session.State.Day + ":" + session.State.DailyGroup + ":" + giftId);
            return bought > 0 ? 0 : 1;
        }

        public bool IsDailyGroupBought()
        {
            return Counter(session.State.Bought,
                "daily-all:" + session.State.Day + ":" + session.State.DailyGroup) > 0;
        }

        public bool CanBuyAllDaily()
        {
            int day = session.State.Day;
            if (Counter(session.State.Bought, "daily-all:" + day + ":" + session.State.DailyGroup) > 0) return false;
            foreach (CommerceGift gift in catalog.Data.dailyGifts.Where(value => value.groupId == session.State.DailyGroup))
                if (Counter(session.State.Bought, "daily:" + day + ":" + session.State.DailyGroup + ":" + gift.id) > 0)
                    return false;
            return true;
        }

        public CommerceTransactionResult Purchase(CommerceOffer offer, CommercePaymentMethod paymentMethod)
        {
            if (offer == null)
                return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "商品不存在或已售罄。");
            var request = new CommercePurchaseRequest
            {
                Kind = offer.Kind,
                Id = offer.Id,
                GroupId = offer.GroupId,
                ExpectedPeriod = offer.Period,
                ExpectedBought = offer.Bought,
                ExpectedMoneyCostId = offer.MoneyCostId,
                ExpectedVoucherCost = offer.VoucherCost,
                ExpectedRewards = new Dictionary<int, long>(offer.Rewards),
                PaymentMethod = paymentMethod
            };
            if (offer.Kind == "periodic")
                return PurchasePeriodic(offer, paymentMethod);
            return OfflineCommerceTransaction.Purchase(session.State, request, voucherItemId,
                Quote, session.Persist);
        }

        private CommerceTransactionResult PurchasePeriodic(CommerceOffer offer, CommercePaymentMethod method)
        {
            CommercePeriodicCard row = catalog.Data.periodicCards.FirstOrDefault(value => value.id == offer.Id);
            CommerceMoneyCost cost = row == null ? null : catalog.MoneyCost(row.moneyCostId);
            if (row == null || cost == null || offer.Period != session.State.Day
                || offer.Bought != Counter(session.State.Bought, "periodic-buy:" + offer.Id)
                || method != OfflineCommerceTransaction.PaymentMethod(offer, voucherItemId, session.State)
                || method == CommercePaymentMethod.Voucher && (cost.voucherCost <= 0
                    || Amount(session.State, voucherItemId) < cost.voucherCost))
                return new CommerceTransactionResult(CommerceTransactionCode.StaleQuote, "周期卡状态已变化，请重新确认。");
            var rewards = new Dictionary<int, long>(offer.Rewards);
            return OfflineCommerceTransaction.Mutate(session.State, candidate =>
            {
                if (method == CommercePaymentMethod.Voucher)
                    SetAmount(candidate, voucherItemId, Amount(candidate, voucherItemId) - cost.voucherCost);
                long oldExpiry = Counter(candidate.Bought, "periodic-expiry:" + offer.Id);
                long start = Math.Max(oldExpiry, candidate.Day);
                long expiry = Math.Min(candidate.Day + 179L, checked(start + row.durationDays));
                if (expiry <= start) throw new InvalidOperationException("周期卡续期已达到时长上限。");
                SetCounter(candidate.Bought, "periodic-expiry:" + offer.Id, expiry);
                SetCounter(candidate.Bought, "periodic-buy:" + offer.Id, offer.Bought + 1);
                foreach (KeyValuePair<int, long> reward in rewards)
                    SetAmount(candidate, reward.Key, checked(Amount(candidate, reward.Key) + reward.Value));
                string key = row.moneyCostId.ToString(CultureInfo.InvariantCulture);
                SetCounter(candidate.PaidOrders, key, Counter(candidate.PaidOrders, key) + 1);
                candidate.AccumulatedValue += cost.startValue;
            }, rewards, "周期卡已激活，立即奖励已入库。", session.Persist);
        }

        public CommerceTransactionResult Purchase(string kind, int id, int groupId,
            CommercePaymentMethod paymentMethod)
        {
            CommerceOffer quote = Quote(kind, id, groupId);
            if (quote == null) return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "商品不存在或未开放。");
            return Purchase(quote, paymentMethod);
        }

        public CommercePaymentMethod PaymentMethod(CommerceOffer offer)
        {
            return OfflineCommerceTransaction.PaymentMethod(offer, voucherItemId, session.State);
        }

        public CommerceTransactionResult ActivateFirstPurchase()
        {
            CommerceMoneyCost cost = catalog.MoneyCost(catalog.Data.settings.firstPurchaseMoneyCostId);
            if (cost == null)
                return new CommerceTransactionResult(CommerceTransactionCode.InvalidRequest, "首购价格配置缺失。");
            return OfflineCommerceTransaction.ActivateFirstPurchase(session.State, cost.id, cost.startValue,
                session.Persist);
        }

        public CommerceTransactionResult ClaimDailyFree()
        {
            if (!CanClaimDailyFree())
                return new CommerceTransactionResult(CommerceTransactionCode.StaleQuote, "今日赠礼已领取。");
            CommerceReward[] rewards = catalog.Data.settings.dailyFreeRewards;
            if (rewards == null || rewards.Length == 0)
                return new CommerceTransactionResult(CommerceTransactionCode.InvalidRewards, "奖励配置缺失。");
            Dictionary<int, long> map = ToDictionary(rewards);
            string key = "daily-free:" + session.State.Day;
            return OfflineCommerceTransaction.Mutate(session.State, candidate =>
            {
                AddRewards(candidate, map);
                SetCounter(candidate.DailyFreeClaims, key, 1);
            }, map, "免费赠礼已领取。", session.Persist);
        }

        public CommerceTransactionResult ClaimWeeklyFree(int id, int groupId)
        {
            CommerceOffer offer = Weekly(id, groupId);
            if (offer == null || offer.MoneyCostId > 0 || offer.Bought >= offer.Limit)
                return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "免费礼包不可领取。");
            return OfflineCommerceTransaction.Mutate(session.State, candidate =>
            {
                AddRewards(candidate, offer.Rewards);
                SetCounter(candidate.Bought, offer.PurchaseKey, offer.Bought + 1);
            }, offer.Rewards, "免费礼包已领取。", session.Persist);
        }

        public bool CanClaimDailyFree() => Counter(session.State.DailyFreeClaims, "daily-free:" + session.State.Day) == 0;

        public CommerceTransactionResult ClaimFirstAward(int day, int professionId)
        {
            CommerceFirstAward row = catalog.Data.firstAwards.FirstOrDefault(value => value.day == day);
            if (row == null || !CanClaimFirst(day))
                return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "首购奖励尚未开放。");
            var rewards = ToDictionary(row.rewards);
            CommerceProfessionReward weapon = FirstAwardWeapon(row, professionId, day);
            if (weapon == null && row.professionItems != null && row.professionItems.Length > 0)
                return new CommerceTransactionResult(CommerceTransactionCode.InvalidRequest, "职业未匹配首购奖励武器。");
            if (weapon != null) Add(rewards, weapon.itemId, weapon.count);
            return OfflineCommerceTransaction.Claim(session.State, day.ToString(CultureInfo.InvariantCulture),
                rewards, true, session.Persist);
        }

        /// <summary>
        /// Official `FirstPayDialog.GetShowAward`: day 1 uses the weapon matching the current
        /// profession; later days use the first configured first-award item. No day expiry.
        /// </summary>
        public CommerceProfessionReward FirstAwardWeapon(CommerceFirstAward row, int professionId, int day)
        {
            CommerceProfessionReward[] items = row?.professionItems ?? Array.Empty<CommerceProfessionReward>();
            if (items.Length == 0) return null;
            if (day != 1) return items[0];
            return items.FirstOrDefault(value => value.professions != null && value.professions.Contains(professionId));
        }

        public bool CanClaimFirst(int day)
        {
            CommerceFirstAward row = catalog.Data.firstAwards.FirstOrDefault(value => value.day == day);
            if (row == null || session.State.FirstPurchaseDay < 0) return false;
            int available = Math.Min(CatalogMaxFirstDay(),
                session.State.Day - session.State.FirstPurchaseDay + 1);
            return day <= available
                && Counter(session.State.FirstClaims, day.ToString(CultureInfo.InvariantCulture)) == 0;
        }

        public bool HasClaimedFirst(int day) =>
            Counter(session.State.FirstClaims, day.ToString(CultureInfo.InvariantCulture)) > 0;

        public bool FirstPayIsActive() => session.State.FirstPurchaseDay >= 0;

        public bool FirstPayIsTakenAllAward()
        {
            return catalog.Data.firstAwards.All(value => HasClaimedFirst(value.day));
        }

        public int FirstPayTodayCanGetAwardIndex()
        {
            if (session.State.FirstPurchaseDay < 0) return 0;
            int max = CatalogMaxFirstDay();
            return Math.Min(max, session.State.Day - session.State.FirstPurchaseDay + 1);
        }

        public bool FirstPayTodayHasAvailableAward()
        {
            if (!FirstPayIsActive() || FirstPayIsTakenAllAward()) return false;
            int index = FirstPayTodayCanGetAwardIndex();
            return catalog.Data.firstAwards.Any(value => value.day <= index && !HasClaimedFirst(value.day));
        }

        private int CatalogMaxFirstDay() =>
            catalog.Data.firstAwards.Length == 0 ? 0 : catalog.Data.firstAwards.Max(value => value.day);

        public CommerceTransactionResult ClaimAccumulated(int level)
        {
            CommerceAccumulatedAward row = catalog.Data.accumulatedAwards.FirstOrDefault(value => value.level == level);
            if (row == null || row.group != CurrentAccumulatedGroup()
                || session.State.AccumulatedValue < row.threshold
                || HasClaimedAccumulated(level))
                return new CommerceTransactionResult(CommerceTransactionCode.SoldOut, "星值不足或奖励不存在。");
            return OfflineCommerceTransaction.Claim(session.State, level.ToString(CultureInfo.InvariantCulture),
                ToDictionary(row.rewards), false, session.Persist);
        }

        public CommerceTransactionResult Exchange(long amount)
        {
            CommerceSettings settings = catalog.Data.settings;
            long held = Amount(session.State, settings.flareStarItemId);
            if (held <= 0 || amount < settings.exchangeMin || amount > settings.exchangeMax || amount > held)
                return new CommerceTransactionResult(CommerceTransactionCode.InvalidRequest, "兑换数量超出原配置范围。");
            long granted = checked(amount * settings.exchangeScale);
            var rewards = new Dictionary<int, long> { [settings.diamondItemId] = granted };
            return OfflineCommerceTransaction.Mutate(session.State, candidate =>
            {
                SetAmount(candidate, settings.flareStarItemId, held - amount);
                SetAmount(candidate, settings.diamondItemId, Amount(candidate, settings.diamondItemId) + granted);
            }, rewards, "兑换成功。", session.Persist);
        }

        public long ExchangeMax()
        {
            CommerceSettings settings = catalog.Data.settings;
            return Math.Min(Amount(session.State, settings.flareStarItemId), settings.exchangeMax);
        }

        public bool HasClaimedAccumulated(int level) =>
            Counter(session.State.AccumulatedClaims, level.ToString(CultureInfo.InvariantCulture)) > 0;

        public bool CanClaimAccumulated(int level)
        {
            CommerceAccumulatedAward row = catalog.Data.accumulatedAwards.FirstOrDefault(value => value.level == level);
            return row != null && row.group == CurrentAccumulatedGroup()
                && session.State.AccumulatedValue >= row.threshold
                && !HasClaimedAccumulated(level);
        }

        public bool HasAvailableAccumulatedAward()
        {
            return CurrentAccumulatedAwards().Any(value => CanClaimAccumulated(value.level));
        }

        public int FirstUnclaimedAccumulatedLevel()
        {
            foreach (CommerceAccumulatedAward award in catalog.Data.accumulatedAwards.OrderBy(value => value.level))
                if (!HasClaimedAccumulated(award.level)) return award.level;
            return catalog.Data.accumulatedAwards.Length == 0 ? 0
                : catalog.Data.accumulatedAwards.Max(value => value.level);
        }

        public IReadOnlyList<CommerceAccumulatedAward> CurrentAccumulatedAwards()
        {
            int group = CurrentAccumulatedGroup();
            if (group == 0) return Array.Empty<CommerceAccumulatedAward>();
            return catalog.Data.accumulatedAwards.Where(value => value.group == group)
                .OrderBy(value => value.level).ToArray();
        }

        public int CurrentAccumulatedGroup()
        {
            CommerceAccumulatedAward[] ordered = catalog.Data.accumulatedAwards
                .OrderBy(value => value.level).ToArray();
            if (ordered.Length == 0) return 0;
            int reached = ordered.Where(value => session.State.AccumulatedValue >= value.threshold)
                .Select(value => value.level).DefaultIfEmpty(0).Max();
            CommerceAccumulatedAward anchor = ordered.FirstOrDefault(value => value.level == (reached > 0 ? reached : ordered[0].level));
            if (anchor == null) return 0;
            CommerceAccumulatedAward[] groupRows = ordered.Where(value => value.group == anchor.group).ToArray();
            int group = anchor.group;
            if (groupRows.Length > 0 && groupRows[groupRows.Length - 1].level < ordered[ordered.Length - 1].level
                && groupRows[groupRows.Length - 1].level <= reached
                && ordered.Any(value => value.group == group + 1))
                group++;
            return group;
        }

        private CommerceOffer GiftOffer(string kind, CommerceGift row, int groupId, int period, string key,
            OfflineCommerceState state)
        {
            CommerceMoneyCost cost = row.moneyCostId <= 0 ? null : catalog.MoneyCost(row.moneyCostId);
            long bought = Counter(state.Bought, key);
            int limit = Math.Max(1, row.limit);
            if (bought >= limit) return null;
            return new CommerceOffer
            {
                Kind = kind, Id = row.id, GroupId = groupId, MoneyCostId = row.moneyCostId,
                VoucherCost = cost == null ? 0 : cost.voucherCost, Limit = limit,
                Bought = (int)bought, Period = period, PurchaseKey = key, Rewards = ToDictionary(row.rewards),
                AccumulatedValue = cost == null ? 0 : cost.startValue, GreatValuePercent = row.greatValue
            };
        }

        private static Dictionary<int, long> Merge(CommerceReward[] left, CommerceReward[] right)
        {
            var result = ToDictionary(left);
            foreach (KeyValuePair<int, long> item in ToDictionary(right)) Add(result, item.Key, item.Value);
            return result;
        }

        public static Dictionary<int, long> ToDictionary(CommerceReward[] rewards)
        {
            var result = new Dictionary<int, long>();
            foreach (CommerceReward reward in rewards ?? Array.Empty<CommerceReward>()) Add(result, reward.itemId, reward.count);
            return result;
        }

        private static void Add(Dictionary<int, long> values, int id, long count)
        {
            if (count <= 0) return;
            values[id] = values.TryGetValue(id, out long current) ? checked(current + count) : count;
        }

        private static void AddRewards(OfflineCommerceState state, IDictionary<int, long> rewards)
        {
            foreach (KeyValuePair<int, long> reward in rewards)
                SetAmount(state, reward.Key, checked(Amount(state, reward.Key) + reward.Value));
        }

        private static long Counter(List<CommerceCounter> values, string key)
        {
            foreach (CommerceCounter item in values) if (item.Key == key) return item.Count;
            return 0;
        }

        private static long Amount(OfflineCommerceState state, int id)
        {
            foreach (CommerceAmount item in state.Inventory) if (item.ItemId == id) return item.Count;
            return 0;
        }

        private static void SetAmount(OfflineCommerceState state, int id, long count)
        {
            foreach (CommerceAmount item in state.Inventory)
                if (item.ItemId == id) { item.Count = count; return; }
            state.Inventory.Add(new CommerceAmount { ItemId = id, Count = count });
        }

        private static void SetCounter(List<CommerceCounter> values, string key, long count)
        {
            foreach (CommerceCounter item in values)
                if (item.Key == key) { item.Count = count; return; }
            values.Add(new CommerceCounter { Key = key, Count = count });
        }
    }
}
