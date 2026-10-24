using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineNpcTravelService
    {
        private const int PositionCount = 4;
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialNpcTravelCatalog catalog;

        public OfflineNpcTravelService(OfflineMainlineProgressSession session, OfficialNpcTravelCatalog catalog)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public OfflineNpcTravelProgress State => session.Save.Player.NpcTravel;

        public IReadOnlyList<int> EligibleMetNpcIds(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) return Array.Empty<int>();
            DateTime localNow = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime;
            var travelling = new HashSet<int>(State.Targets.Where(target => !target.Received
                    && !IsComplete(target, localNow)).SelectMany(target => target.NpcIds));
            return (session.Save.Player.CharacterProperties.NpcFriendships ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && !travelling.Contains(value.NpcClassId))
                .Where(value => State.NpcDailyCounts.All(count => count.NpcId != value.NpcClassId
                    || count.Count < catalog.Data.npc_daily_limit))
                .Select(value => catalog.Npc(value.NpcClassId))
                .Where(npc => npc != null && npc.can_develop && npc.travel_profession > 0)
                .Select(npc => npc.id).Distinct().OrderBy(id => id).ToArray();
        }

        public bool RefreshLocalDay(DateTime localNow, int mapGroupId, int regionId, string eventId)
        {
            if (mapGroupId <= 0 || regionId <= 0 || string.IsNullOrWhiteSpace(eventId)) return false;
            int dayKey = LocalTravelDayKey(localNow);
            bool newDay = State.LocalDayKey != dayKey;
            bool newLocation = State.MapGroupId != mapGroupId || State.RegionId != regionId;
            if (!newDay && !newLocation) return true;
            ProgressionResult result = session.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineNpcTravelProgress state = save.Player.NpcTravel;
                bool dayChanged = state.LocalDayKey != dayKey;
                bool locationChanged = state.MapGroupId != mapGroupId || state.RegionId != regionId;
                if (!dayChanged && !locationChanged) return;
                EnsureRandomState(state, save.CreatedAtUnixSeconds, dayKey);
                if (dayChanged)
                {
                    state.LocalDayKey = dayKey;
                    state.DailyStartedCount = 0;
                    state.AutoRefreshedCount = 0;
                    state.ManualRefreshedCount = 0;
                    state.NpcDailyCounts.Clear();
                }
                var next = new List<OfflineNpcTravelPosition>(PositionCount);
                for (int position = 1; position <= PositionCount; position++)
                {
                    OfflineNpcTravelPosition old = state.Positions.FirstOrDefault(value => value.PositionId == position);
                    OfflineNpcTravelTarget active = state.Targets.FirstOrDefault(value => value.PositionId == position
                        && !value.Received);
                    if (active != null && old != null)
                    {
                        next.Add(old);
                        continue;
                    }
                    NpcTravelRow offer = catalog.Sample(mapGroupId, regionId, position,
                        state.Pity, NextRandom(state), state.AutoRefreshedCount == 0, false);
                    if (offer == null || offer.position_id != position)
                        throw new InvalidOperationException("Official travel offer is missing or belongs to another position.");
                    AdvancePity(state, position, offer.quality);
                    next.Add(new OfflineNpcTravelPosition { PositionId = position, TravelId = offer.travel_id });
                }
                if (state.AutoRefreshedCount == 0) state.AutoRefreshedCount++;
                state.MapGroupId = mapGroupId;
                state.RegionId = regionId;
                state.Positions = next;
            });
            return result.Status == ProgressionResultStatus.Applied || result.Status == ProgressionResultStatus.Unchanged;
        }

        public ProgressionResult ManualRefresh(IReadOnlyList<int> positionIds, int mapGroupId, int regionId,
            string eventId)
        {
            if (positionIds == null || positionIds.Count == 0 || mapGroupId <= 0 || regionId <= 0)
                return Invalid("Manual travel refresh has no replacements.");
            return session.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineNpcTravelProgress state = save.Player.NpcTravel;
                EnsureRandomState(state, save.CreatedAtUnixSeconds, state.LocalDayKey);
                int costIndex = Math.Min(state.ManualRefreshedCount, catalog.Data.refresh_costs.Length - 1);
                if (costIndex < 0 || catalog.Data.refresh_cost_item_id <= 0)
                    throw new InvalidOperationException("Official travel refresh cost is missing.");
                long cost = catalog.Data.refresh_costs[costIndex];
                OfflineItemStack currency = save.Player.Inventory.FirstOrDefault(value =>
                    value.ItemId == catalog.Data.refresh_cost_item_id);
                if (currency == null || currency.Count < cost)
                    throw new InvalidOperationException("Travel refresh currency is insufficient.");
                var refreshed = new List<OfflineNpcTravelPosition>();
                foreach (int positionId in positionIds.Distinct().OrderBy(value => value))
                {
                    OfflineNpcTravelPosition position = state.Positions.FirstOrDefault(value => value.PositionId == positionId);
                    if (position == null || state.Targets.Any(value => value.PositionId == positionId && !value.Received))
                        continue;
                    NpcTravelRow offer = catalog.Sample(mapGroupId, regionId, positionId,
                        state.Pity, NextRandom(state), false, state.ManualRefreshedCount == 0);
                    if (offer == null || offer.position_id != positionId)
                        throw new InvalidOperationException("Manual refresh offer does not match its position.");
                    position.TravelId = offer.travel_id;
                    position.Used = false;
                    position.BaseReward = 0;
                    position.ExtraReward = 0;
                    refreshed.Add(position);
                    AdvancePity(state, positionId, offer.quality);
                }
                if (refreshed.Count == 0) throw new InvalidOperationException("No travel positions can be refreshed.");
                currency.Count -= cost;
                state.ManualRefreshedCount++;
            });
        }

        public ProgressionResult Dispatch(int positionId, int travelId, int quality, int durationMinutes,
            int regionId, int mapGroupId, IReadOnlyDictionary<int, long> npcProperties,
            IReadOnlyDictionary<int, long> treasureProperties, IReadOnlyList<int> npcIds,
            long nowUnixSeconds, string eventId)
        {
            if (positionId < 1 || positionId > PositionCount || travelId <= 0 || quality < 1 || quality > 5
                || durationMinutes <= 0 || npcProperties == null || treasureProperties == null || npcIds == null || npcIds.Count == 0
                || npcIds.Count > 5 || npcIds.Any(id => id <= 0)
                || npcIds.Distinct().Count() != npcIds.Count
                || nowUnixSeconds < 0)
                return Invalid("Travel dispatch input is invalid.");
            NpcTravelRow offer = catalog.FindTravel(travelId);
            NpcTravelQuality qualityConfig = catalog.Quality(quality);
            if (offer == null || offer.position_id != positionId || offer.quality != quality
                || offer.map_group_id != mapGroupId || offer.region_id != regionId
                || qualityConfig == null || durationMinutes != qualityConfig.duration
                || npcIds.Count > qualityConfig.npc_count)
                return Invalid("Travel dispatch differs from official quality configuration.");
            ProgressionResult result = session.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineNpcTravelProgress state = save.Player.NpcTravel;
                if (state.DailyStartedCount >= catalog.Data.daily_start_limit)
                    throw new InvalidOperationException("Daily travel dispatch limit reached.");
                OfflineNpcTravelPosition position = state.Positions.FirstOrDefault(value => value.PositionId == positionId);
                if (position == null || position.TravelId != travelId || position.Used)
                    throw new InvalidOperationException("Travel offer is no longer available.");
                if (state.Targets.Any(value => value.PositionId == positionId && !value.Received
                        && !IsComplete(value, DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime)))
                    throw new InvalidOperationException("Travel position is occupied.");
                foreach (int npcId in npcIds)
                {
                    if (!(save.Player.CharacterProperties.NpcFriendships ?? new List<OfflineNpcFriendship>())
                            .Any(value => value.NpcClassId == npcId))
                        throw new InvalidOperationException("A selected partner has not been met.");
                    NpcTravelNpc npc = catalog.Npc(npcId);
                    if (npc == null || !npc.can_develop || npc.travel_profession <= 0)
                        throw new InvalidOperationException("A selected partner is not eligible for travel.");
                    OfflineNpcTravelDailyCount count = state.NpcDailyCounts.FirstOrDefault(value => value.NpcId == npcId);
                    if (count != null && count.Count >= catalog.Data.npc_daily_limit)
                        throw new InvalidOperationException("A selected partner has already travelled today.");
                    if (state.Targets.Any(value => !value.Received && value.NpcIds.Contains(npcId)
                            && !IsComplete(value, DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime)))
                        throw new InvalidOperationException("A selected partner is already travelling.");
                }
                EnsureRandomState(state, save.CreatedAtUnixSeconds, state.LocalDayKey);
                System.Random random = NextRandom(state);
                NpcTravelNode node = catalog.Node(offer.map_group_id, offer.region_id, offer.node_id);
                NpcTravelQuality qualityData = catalog.Quality(quality);
                double npcBasePercent = Property(npcProperties, catalog.Data.travel_npc_prop_types[1]);
                double treasureBasePercent = Property(treasureProperties, catalog.Data.travel_treasure_prop_types[1]);
                int nodeBaseCount = node == null ? 0 : node.base_add_count;
                long baseReward = CalcBaseReward(catalog.Data.base_reward_item_count, nodeBaseCount,
                    treasureBasePercent, qualityData.base_reward_add, npcBasePercent);
                int meetConditionCount = catalog.MeetConditionCount(travelId, npcIds);
                double extraProbability = CalcExtraRewardProbability(
                    Property(npcProperties, catalog.Data.travel_npc_prop_types[2]),
                    Property(treasureProperties, catalog.Data.travel_treasure_prop_types[2]),
                    node == null ? 0 : node.extra_probability_add,
                    qualityData.extra_base_add, meetConditionCount, qualityData.extra_condition_add);
                int extraRewardType = RollExtraRewardType(extraProbability,
                    catalog.Data.extra_normal_threshold, catalog.Data.extra_super_threshold,
                    maximum => random.Next(Math.Max(1, maximum)));
                long extraMultiplier = catalog.Data.extra_rates.FirstOrDefault(value => value.type == extraRewardType)?.rate ?? 0;
                List<OfflineNpcTravelReward> baseRewards = new List<OfflineNpcTravelReward>
                {
                    new OfflineNpcTravelReward { ItemId = catalog.Data.base_reward_item_id, Count = baseReward }
                };
                List<OfflineNpcTravelReward> extraRewards = SampleExtraRewards(
                    catalog.RewardRule(offer.extra_reward_rule_id), extraMultiplier, random);
                foreach (int npcId in npcIds)
                {
                    OfflineNpcTravelDailyCount count = state.NpcDailyCounts.FirstOrDefault(value => value.NpcId == npcId);
                    if (count == null) state.NpcDailyCounts.Add(new OfflineNpcTravelDailyCount { NpcId = npcId, Count = 1 });
                    else count.Count++;
                }
                state.DailyStartedCount++;
                position.Used = true;
                state.Targets.Add(new OfflineNpcTravelTarget
                {
                    TravelId = travelId, PositionId = positionId, Quality = quality,
                    DurationMinutes = durationMinutes, StartedAtUnixSeconds = nowUnixSeconds,
                    LocalDayKey = state.LocalDayKey,
                    RewardItemId = catalog.Data.base_reward_item_id, BaseReward = baseReward,
                    ExtraReward = extraRewards.Sum(value => value.Count),
                    BaseRewards = baseRewards, ExtraRewards = extraRewards,
                    ExtraProbability = checked((int)Math.Round(extraProbability * 10000.0, MidpointRounding.ToEven)),
                    MeetConditionCount = meetConditionCount, ExtraRewardType = extraRewardType,
                    RegionId = regionId, MapGroupId = mapGroupId,
                    NpcIds = npcIds.ToList()
                });
                position.RewardItemId = catalog.Data.base_reward_item_id;
                position.BaseReward = baseReward;
                position.ExtraReward = extraRewards.Sum(value => value.Count);
            });
            return result;
        }

        public ProgressionResult Claim(int travelId, long nowUnixSeconds, string eventId)
        {
            if (travelId <= 0 || nowUnixSeconds < 0) return Invalid("Travel claim input is invalid.");
            return session.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineNpcTravelTarget target = save.Player.NpcTravel.Targets.FirstOrDefault(value =>
                    value.TravelId == travelId && !value.Received);
                if (target == null || target.Received) throw new InvalidOperationException("Travel reward is unavailable.");
                DateTime localNow = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime;
                if (!IsComplete(target, localNow)) throw new InvalidOperationException("Travel has not finished.");
                List<OfflineNpcTravelReward> snapshotRewards = (target.BaseRewards ?? new List<OfflineNpcTravelReward>())
                    .Concat(target.ExtraRewards ?? new List<OfflineNpcTravelReward>()).ToList();
                if (snapshotRewards.Count == 0 && target.RewardItemId > 0 && target.BaseReward + target.ExtraReward > 0)
                    snapshotRewards.Add(new OfflineNpcTravelReward
                    {
                        ItemId = target.RewardItemId, Count = target.BaseReward + target.ExtraReward
                    });
                IEnumerable<OfflineNpcTravelReward> rewards = snapshotRewards;
                foreach (IGrouping<int, OfflineNpcTravelReward> group in rewards.GroupBy(value => value.ItemId))
                {
                    long reward = group.Sum(value => value.Count);
                    OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == group.Key);
                    if (stack == null)
                    {
                        stack = new OfflineItemStack { ItemId = group.Key };
                        save.Player.Inventory.Add(stack);
                    }
                    if (reward <= 0 || long.MaxValue - stack.Count < reward)
                        throw new InvalidOperationException("Travel reward amount is invalid.");
                    stack.Count += reward;
                }
                OfflineNpcTravelPosition position = FindPosition(save.Player.NpcTravel, target.PositionId);
                if (position == null || position.TravelId != target.TravelId)
                    throw new InvalidOperationException("Travel position snapshot is missing.");
                position.Used = false;
                position.RewardItemId = 0;
                position.BaseReward = 0;
                position.ExtraReward = 0;
                target.Received = true;
            });
        }

        public ProgressionResult Recall(int travelId, long nowUnixSeconds, string eventId)
        {
            if (travelId <= 0 || nowUnixSeconds < 0) return Invalid("Travel recall input is invalid.");
            return session.ApplyInventoryTransaction(eventId, save =>
            {
                OfflineNpcTravelProgress state = save.Player.NpcTravel;
                OfflineNpcTravelTarget target = state.Targets.FirstOrDefault(value =>
                    value.TravelId == travelId && !value.Received);
                if (target == null || target.Received) throw new InvalidOperationException("Travel cannot be recalled.");
                DateTime localNow = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime;
                if (IsComplete(target, localNow)) throw new InvalidOperationException("Finished travel must be claimed.");
                if (state.LocalDayKey == target.LocalDayKey)
                {
                    if (state.DailyStartedCount <= 0)
                        throw new InvalidOperationException("Travel dispatch count is invalid.");
                    state.DailyStartedCount--;
                    foreach (int npcId in target.NpcIds)
                    {
                        OfflineNpcTravelDailyCount count = state.NpcDailyCounts.FirstOrDefault(value => value.NpcId == npcId);
                        if (count == null || count.Count <= 0)
                            throw new InvalidOperationException("Partner travel count is invalid.");
                        count.Count--;
                        if (count.Count == 0) state.NpcDailyCounts.Remove(count);
                    }
                }
                OfflineNpcTravelPosition position = state.Positions.FirstOrDefault(value => value.PositionId == target.PositionId);
                if (position == null || position.TravelId != travelId)
                    throw new InvalidOperationException("Travel position snapshot is missing.");
                position.Used = false;
                position.RewardItemId = 0;
                position.BaseReward = 0;
                position.ExtraReward = 0;
                state.Targets.Remove(target);
            });
        }

        public static bool IsComplete(OfflineNpcTravelTarget target, DateTime localNow)
        {
            if (target == null || target.StartedAtUnixSeconds < 0 || target.DurationMinutes <= 0) return false;
            long finish = target.StartedAtUnixSeconds + (long)target.DurationMinutes * 60L;
            return new DateTimeOffset(localNow.ToUniversalTime()).ToUnixTimeSeconds() >= finish;
        }

        public static long CalcBaseReward(double baseCount, double nodeAddCount, double treasureAddPercent,
            int qualityAddPercent, double npcAddPercent)
        {
            double count = (baseCount + nodeAddCount) * (1.0 + treasureAddPercent / 10000.0)
                * (1.0 + (qualityAddPercent + npcAddPercent) / 10000.0);
            return checked((long)Math.Round(count, MidpointRounding.ToEven));
        }

        public static double CalcExtraRewardProbability(double npcAdd, double treasureAdd,
            double nodeAdd, int qualityBaseAdd, int meetConditionCount, int qualityConditionAdd)
        {
            return (npcAdd + treasureAdd + nodeAdd) / 10000.0
                + (qualityBaseAdd + (long)meetConditionCount * qualityConditionAdd) / 10000.0;
        }

        public static int RollExtraRewardType(double probability, int normalThreshold,
            int superThreshold, Func<int, int> range)
        {
            if (range == null) throw new ArgumentNullException(nameof(range));
            double value = probability * 100.0;
            if (value > 0.0 && value <= normalThreshold && range(normalThreshold) < value) return 1;
            if (value > normalThreshold)
            {
                int span = superThreshold - normalThreshold;
                if (span > 0 && range(span) < value - normalThreshold) return 2;
                return 1;
            }
            return 0;
        }

        public static int LocalTravelDayKey(DateTime localNow)
        {
            DateTime todayRefresh = localNow.Date.AddHours(8);
            DateTime travelDay = localNow < todayRefresh ? todayRefresh.AddDays(-1) : todayRefresh;
            return travelDay.Year * 10000 + travelDay.Month * 100 + travelDay.Day;
        }

        private static ProgressionResult Invalid(string message) => new ProgressionResult
        {
            Status = ProgressionResultStatus.InvalidState,
            Message = message
        };

        private static OfflineNpcTravelPosition FindPosition(OfflineNpcTravelProgress state, int positionId) =>
            state.Positions.FirstOrDefault(value => value.PositionId == positionId);

        private void AdvancePity(OfflineNpcTravelProgress state, int positionId, int generatedQuality)
        {
            foreach (NpcTravelQuality quality in catalog.Data.qualities.Where(value => value.pity_threshold > 0))
            {
                OfflineNpcTravelPity pity = state.Pity.FirstOrDefault(value => value.PositionId == positionId
                    && value.Quality == quality.quality);
                if (pity == null)
                {
                    pity = new OfflineNpcTravelPity { PositionId = positionId, Quality = quality.quality };
                    state.Pity.Add(pity);
                }
                if (quality.quality == generatedQuality) pity.Count = 0;
                else if (pity.Count < quality.pity_threshold) pity.Count++;
            }
        }

        private static double Property(IReadOnlyDictionary<int, long> values, int propertyType)
        {
            return values != null && values.TryGetValue(propertyType, out long value) ? value : 0.0;
        }

        private static List<OfflineNpcTravelReward> SampleExtraRewards(NpcTravelRewardRule rule,
            long multiplier, System.Random random)
        {
            var result = new Dictionary<int, long>();
            if (rule == null || multiplier <= 0 || random == null) return new List<OfflineNpcTravelReward>();
            long totalWeight = rule.weights.Sum(value => (long)Math.Max(0, value));
            if (totalWeight <= 0) return new List<OfflineNpcTravelReward>();
            foreach (IGrouping<int, int> item in Enumerable.Range(0, rule.item_ids.Length)
                .GroupBy(index => rule.item_ids[index]).OrderBy(group => group.Key))
            {
                long itemWeight = 0;
                float count = 0f;
                foreach (int index in item)
                {
                    if (rule.weights[index] <= 0 || rule.counts[index] <= 0f) continue;
                    itemWeight += rule.weights[index];
                    count = Math.Max(count, rule.counts[index]);
                }
                if (itemWeight <= 0 || count <= 0f) continue;
                double probability = rule.percent * itemWeight / totalWeight;
                int guaranteedRolls = (int)Math.Floor(probability);
                int rolls = guaranteedRolls;
                double fractional = probability - guaranteedRolls;
                if (fractional > 0d && random.NextDouble() < fractional) rolls++;
                long amount = checked((long)Math.Truncate(count) * rolls * multiplier);
                if (amount <= 0) continue;
                result[item.Key] = checked((result.TryGetValue(item.Key, out long current) ? current : 0L) + amount);
            }
            return result.OrderBy(value => value.Key)
                .Select(value => new OfflineNpcTravelReward { ItemId = value.Key, Count = value.Value }).ToList();
        }

        private static void EnsureRandomState(OfflineNpcTravelProgress state, long createdAtUnixSeconds, int dayKey)
        {
            if (state.RandomState > 0) return;
            unchecked
            {
                ulong seed = (ulong)Math.Max(0, createdAtUnixSeconds) ^ ((ulong)(uint)dayKey << 32)
                    ^ 0x9E3779B97F4A7C15UL;
                seed ^= seed >> 12;
                seed ^= seed << 25;
                seed ^= seed >> 27;
                long initial = (long)((seed * 2685821657736338717UL) & 0x7FFFFFFFFFFFFFFFUL);
                state.RandomState = initial == 0 ? 88172645463325252L : initial;
            }
        }

        private static System.Random NextRandom(OfflineNpcTravelProgress state)
        {
            unchecked
            {
                ulong value = (ulong)state.RandomState;
                value ^= value >> 12;
                value ^= value << 25;
                value ^= value >> 27;
                value &= 0x7FFFFFFFFFFFFFFFUL;
                state.RandomState = (long)(value == 0 ? 88172645463325252UL : value);
                int seed = (int)(state.RandomState & 0x7FFFFFFF);
                return new System.Random(seed);
            }
        }
    }
}
