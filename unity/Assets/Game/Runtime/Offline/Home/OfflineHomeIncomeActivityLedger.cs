using System;
using System.Collections.Generic;
using Zjcs.Offline.State;

namespace Zjcs.Offline.Home
{
    [Serializable]
    public sealed class HomeIncomeActivityState
    {
        public bool Enabled;
        public long ExtraCount;
        public double ExtraFraction;
        public long DiscardedCount;
        public double DiscardedFraction;
        public long ProtectedCount;
        public long Capacity;
        public double BaseAnchorSeconds;
        public long ProducedTicks;
        public bool RateAnchorInitialized;
        public int Rate;
        public double RateAnchorSeconds;
        public long RateAnchorCount;
        public double RateAnchorFraction;

        public HomeIncomeActivityState Copy() => (HomeIncomeActivityState)MemberwiseClone();

        public bool IsValid => ExtraCount >= 0 && DiscardedCount >= 0 && ProtectedCount >= 0 && Capacity >= 0
            && !double.IsNaN(ExtraFraction) && ExtraFraction >= 0 && ExtraFraction < 1
            && !double.IsNaN(DiscardedFraction) && DiscardedFraction >= 0 && DiscardedFraction < 1
            && !double.IsNaN(BaseAnchorSeconds) && !double.IsInfinity(BaseAnchorSeconds) && BaseAnchorSeconds >= 0
            && ProducedTicks >= 0 && Rate >= 0 && RateAnchorCount >= 0
            && !double.IsNaN(RateAnchorSeconds) && !double.IsInfinity(RateAnchorSeconds) && RateAnchorSeconds >= 0
            && !double.IsNaN(RateAnchorFraction) && RateAnchorFraction >= 0 && RateAnchorFraction < 1;
    }

    // Local accounting policy for original single-output furniture. Original
    // production and percentage/capacity formulas remain in their own ports.
    // The state is separate from those ports and from inventory transactions.
    public static class OfflineHomeIncomeActivityLedger
    {
        public static void Advance(HangUpIncomeRecord record, HomeIncomeActivityState state, int id,
            HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties,
            IHomeIncomeAdditionSnapshot additions, long fromTicks, long untilTicks)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (fromTicks > untilTicks) return;
            long baseCapacity = OfflineHangUpIncome.Preview(record, rule, properties, floatProperties).Capacity;
            foreach (var segment in OfflineIncomeTimeSegments.Split(fromTicks, untilTicks,
                additions?.ChangeTicks ?? Array.Empty<long>()))
            {
                long capacity = CapacityAt(id, baseCapacity, additions, segment.Start);
                if (!state.Enabled && ((additions?.AddPercent(id, segment.Start) ?? 0) > 0 || capacity > baseCapacity))
                    Enable(record, state, rule, properties, floatProperties, baseCapacity, capacity);
                if (!state.Enabled)
                {
                    OfflineHangUpIncome.Accrue(record, rule, properties, floatProperties,
                        (segment.End - segment.Start) / (double)TimeSpan.TicksPerSecond);
                    continue;
                }
                SetCapacity(record, state, rule, properties, floatProperties, capacity);
                if (Count(record, state, rule, properties, floatProperties, capacity) >= capacity) continue;
                long acceptedTicks = segment.End - segment.Start;
                if (ProjectedCount(record, state, id, rule, properties, floatProperties,
                    additions, segment.Start, acceptedTicks) >= capacity)
                {
                    // Find the first accepted production tick that fills this
                    // capacity. Time spent full cannot reappear after a boost.
                    long low = 0, high = acceptedTicks;
                    while (low < high)
                    {
                        long middle = low + (high - low) / 2;
                        if (ProjectedCount(record, state, id, rule, properties, floatProperties,
                            additions, segment.Start, middle) >= capacity) high = middle;
                        else low = middle + 1;
                    }
                    acceptedTicks = low;
                }
                Produce(record, state, id, rule, properties, floatProperties, additions, segment.Start, acceptedTicks);
                if (Uncapped(record, state, rule, properties, floatProperties) >= capacity)
                    DiscardAtCapacity(record, state, rule, properties, floatProperties, capacity);
            }
            long finalCapacity = CapacityAt(id, baseCapacity, additions, untilTicks);
            if (!state.Enabled && ((additions?.AddPercent(id, untilTicks) ?? 0) > 0 || finalCapacity > baseCapacity))
                Enable(record, state, rule, properties, floatProperties, baseCapacity, finalCapacity);
            if (state.Enabled)
                SetCapacity(record, state, rule, properties, floatProperties, finalCapacity);
        }

        public static HangUpIncomePreview Preview(HangUpIncomeRecord record, HomeIncomeActivityState state, int id,
            HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties,
            IHomeIncomeAdditionSnapshot additions, long atTicks)
        {
            var original = OfflineHangUpIncome.Preview(record, rule, properties, floatProperties);
            long capacity = CapacityAt(id, original.Capacity, additions, atTicks);
            if (state == null || !state.Enabled)
            { original.Capacity = capacity; return original; }
            long protectedCount = state.ProtectedCount;
            if (capacity < state.Capacity)
                protectedCount = Math.Max(protectedCount, Count(record, state, rule, properties, floatProperties, state.Capacity));
            original.Capacity = capacity;
            original.Count = Math.Max(protectedCount, Count(record, state, rule, properties, floatProperties, capacity));
            double raw = BaseValue(record, rule, properties, floatProperties);
            double adjusted = Math.Max(0, raw - state.DiscardedFraction);
            original.RemainderSeconds = raw > 0 ? (adjusted - Whole(adjusted)) * record.StoredSeconds / raw : 0;
            return original;
        }

        public static long Collect(HangUpIncomeRecord record, HomeIncomeActivityState state, int id,
            HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties,
            IHomeIncomeAdditionSnapshot additions, long atTicks)
        {
            if (state == null || !state.Enabled) return OfflineHangUpIncome.Collect(record, rule, properties, floatProperties);
            var preview = Preview(record, state, id, rule, properties, floatProperties, additions, atTicks);
            if (preview.Count <= 0) return 0;
            record.StoredSeconds = Math.Max(0, preview.RemainderSeconds);
            record.SettledCount = 0;
            state.ExtraCount = state.DiscardedCount = state.ProtectedCount = 0;
            state.DiscardedFraction = 0;
            state.Capacity = preview.Capacity;
            Rebase(record, state);
            return preview.Count;
        }

        // Call after a property/rule change, retaining already earned output.
        public static void Rebase(HangUpIncomeRecord record, HomeIncomeActivityState state)
        {
            state.BaseAnchorSeconds = record.StoredSeconds;
            state.ProducedTicks = 0;
            state.RateAnchorInitialized = false;
        }

        static void Enable(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties, long baseCapacity, long capacity)
        {
            var previous = OfflineHangUpIncome.Preview(record, rule, properties, floatProperties);
            state.Enabled = true;
            state.Capacity = capacity;
            state.BaseAnchorSeconds = record.StoredSeconds;
            decimal oldTotal = (decimal)Math.Max(0, record.SettledCount) + previous.UnsettledCount;
            state.DiscardedCount = (long)Math.Min(long.MaxValue, Math.Max(0, oldTotal - previous.Count));
            if (previous.Count >= baseCapacity)
            {
                double raw = BaseValue(record, rule, properties, floatProperties);
                state.DiscardedFraction = raw - Whole(raw);
            }
        }

        static long CapacityAt(int id, long capacity, IHomeIncomeAdditionSnapshot additions, long at)
        {
            long extra = Math.Max(0, additions?.ExtraCapacity(id, capacity, at) ?? 0);
            return extra > long.MaxValue - capacity ? long.MaxValue : capacity + extra;
        }

        static void SetCapacity(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties, long capacity)
        {
            if (capacity < state.Capacity)
                state.ProtectedCount = Math.Max(state.ProtectedCount, Count(record, state, rule, properties, floatProperties, state.Capacity));
            state.Capacity = capacity;
        }

        static long Count(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties, long capacity)
            => Math.Max(Math.Max(Math.Max(0, record.SettledCount), state.ProtectedCount),
                (long)Math.Min(capacity, Uncapped(record, state, rule, properties, floatProperties)));

        static decimal Uncapped(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties)
            => Math.Max(0, (decimal)Math.Max(0, record.SettledCount)
                + Whole(Math.Max(0, BaseValue(record, rule, properties, floatProperties) - state.DiscardedFraction))
                + state.ExtraCount - state.DiscardedCount);

        static double BaseValue(HangUpIncomeRecord record, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties)
        {
            double value = OfflineHangUpIncome.UnsettledRewardValue(rule, properties, floatProperties, record.StoredSeconds);
            return double.IsNaN(value) || value <= 0 ? 0 : Math.Min(long.MaxValue, value);
        }

        static long Whole(double value) => value >= long.MaxValue ? long.MaxValue : (long)value;

        static decimal ProjectedCount(HangUpIncomeRecord record, HomeIncomeActivityState state, int id,
            HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties,
            IHomeIncomeAdditionSnapshot additions, long from, long ticks)
        {
            var projected = new HangUpIncomeRecord { StoredSeconds = record.StoredSeconds, SettledCount = record.SettledCount };
            var projectedState = state.Copy();
            Produce(projected, projectedState, id, rule, properties, floatProperties, additions, from, ticks);
            return Uncapped(projected, projectedState, rule, properties, floatProperties);
        }

        static void Produce(HangUpIncomeRecord record, HomeIncomeActivityState state, int id,
            HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties,
            IHomeIncomeAdditionSnapshot additions, long from, long ticks)
        {
            if (ticks <= 0) return;
            long accepted = checked(state.ProducedTicks + ticks);
            double seconds = state.BaseAnchorSeconds + accepted / (double)TimeSpan.TicksPerSecond;
            int percent = Math.Max(0, additions?.AddPercent(id, from) ?? 0);
            if (!state.RateAnchorInitialized || state.Rate != percent)
            {
                state.RateAnchorInitialized = true;
                state.Rate = percent;
                state.RateAnchorSeconds = record.StoredSeconds;
                state.RateAnchorCount = state.ExtraCount;
                state.RateAnchorFraction = state.ExtraFraction;
            }
            // Re-evaluate one constant-rate interval from its fixed anchor.
            // Checkpoint frequency cannot change the number of float products
            // or discard a fractional output near an integer boundary.
            double extra = OfflineHangUpIncomeAddition.ValueForProductionChange(rule, properties, floatProperties,
                percent, state.RateAnchorSeconds, seconds) + state.RateAnchorFraction;
            long whole = Whole(extra);
            if (whole > long.MaxValue - state.RateAnchorCount)
            { state.ExtraCount = long.MaxValue; state.ExtraFraction = 0; }
            else
            {
                state.ExtraCount = state.RateAnchorCount + whole;
                state.ExtraFraction = extra >= long.MaxValue ? 0 : extra - whole;
            }
            state.ProducedTicks = accepted;
            record.StoredSeconds = seconds;
        }

        static void DiscardAtCapacity(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatProperties, long capacity)
        {
            decimal discard = Uncapped(record, state, rule, properties, floatProperties) - capacity + state.DiscardedCount;
            if (discard > long.MaxValue) throw new InvalidOperationException("Discarded income exceeds the local ledger range.");
            state.DiscardedCount = (long)discard;
            double value = Math.Max(0, BaseValue(record, rule, properties, floatProperties) - state.DiscardedFraction);
            double fractions = state.DiscardedFraction + value - Whole(value);
            long whole = Whole(fractions);
            state.DiscardedCount = checked(state.DiscardedCount + whole);
            state.DiscardedFraction = fractions - whole;
            state.ExtraFraction = 0;
            state.ProtectedCount = Math.Max(state.ProtectedCount, capacity);
            Rebase(record, state);
        }
    }
}
