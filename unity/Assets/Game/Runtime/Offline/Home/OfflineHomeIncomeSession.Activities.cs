using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Home
{
    public sealed partial class OfflineHomeIncomeSession
    {
        Func<OfflineHomeSave, IHomeIncomeAdditionSnapshot> incomeAdditionFactory;
        bool incomeOpened;

        // Configure in Open's callback, before the first offline catch-up.
        public void SetIncomeAdditionSource(Func<OfflineHomeSave, IHomeIncomeAdditionSnapshot> capture)
            => incomeAdditionFactory = capture ?? throw new ArgumentNullException(nameof(capture));

        public bool HasActivityIncome(int id)
            => FindEntry(Save, id).Activity?.Enabled == true || incomeAdditionFactory?.Invoke(Save)?.HasRecords == true;

        public HangUpIncomePreview PreviewAt(int id, DateTimeOffset now)
        {
            var definition = FindDefinition(id);
            var saved = FindEntry(Save, id);
            var entry = new OfflineHomeIncomeEntry { HangUpId = id, StoredSeconds = saved.StoredSeconds,
                SettledCount = saved.SettledCount, Activity = saved.Activity?.Copy() };
            long from = CheckpointTicks(Save), until = Math.Max(from, now.UtcDateTime.Ticks);
            var additions = incomeAdditionFactory?.Invoke(Save);
            if (definition.Unlocked)
                AdvanceIncomeEntry(entry, definition, additions, from, until, PendingIncomeSeconds(now));
            return OfflineHomeIncomeActivityLedger.Preview(ToRecord(entry), entry.Activity, id, definition.Rule,
                properties, floatPropertyIds, additions, until);
        }

        public bool MutateAt(string receipt, DateTimeOffset now, Action<OfflineHomeSave> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (string.IsNullOrWhiteSpace(receipt)) throw new ArgumentException("A receipt is required.", nameof(receipt));
            if (Save.AppliedReceipts.Contains(receipt)) return false;
            var next = Clone(Save);
            AdvanceIncome(next, now.ToUnixTimeSeconds(), Subsecond(now));
            mutation(next);
            AddReceipt(next, receipt);
            return Commit(next);
        }

        HangUpIncomePreview IncomePreview(OfflineHomeSave save, OfflineHomeIncomeEntry entry,
            OfflineHomeIncomeDefinition definition, long at)
            => OfflineHomeIncomeActivityLedger.Preview(ToRecord(entry), entry.Activity, definition.HangUpId,
                definition.Rule, properties, floatPropertyIds, incomeAdditionFactory?.Invoke(save), at);

        void AdvanceIncomeEntry(OfflineHomeIncomeEntry entry, OfflineHomeIncomeDefinition definition,
            IHomeIncomeAdditionSnapshot additions, long from, long until, double elapsed)
        {
            if (entry.Activity?.Enabled == true || additions?.HasRecords == true)
            {
                var state = entry.Activity ?? new HomeIncomeActivityState();
                var record = ToRecord(entry);
                OfflineHomeIncomeActivityLedger.Advance(record, state, definition.HangUpId,
                    definition.Rule, properties, floatPropertyIds, additions, from, until);
                entry.StoredSeconds = record.StoredSeconds;
                entry.SettledCount = record.SettledCount;
                if (state.Enabled) entry.Activity = state;
            }
            else if (elapsed > 0)
            {
                double capacitySeconds = Math.Max(0, definition.Rule.MaxStoreHours) * 3600.0;
                entry.StoredSeconds = Math.Min(capacitySeconds, Math.Max(0, entry.StoredSeconds) + elapsed);
            }
        }

        static long CheckpointTicks(OfflineHomeSave save)
        {
            const long epoch = 621355968000000000L;
            long maximum = (DateTime.MaxValue.Ticks - epoch) / TimeSpan.TicksPerSecond;
            if (save.LastLocalUnixSeconds > maximum) return DateTime.MaxValue.Ticks;
            long fraction = Math.Min(TimeSpan.TicksPerSecond - 1,
                (long)Math.Round(save.LastIncomeSubsecond * TimeSpan.TicksPerSecond));
            return Math.Min(DateTime.MaxValue.Ticks, epoch + save.LastLocalUnixSeconds * TimeSpan.TicksPerSecond + fraction);
        }

        void RebaseChangedActivityRules(OfflineHomeIncomeDefinition[] nextDefinitions,
            IReadOnlyDictionary<int, long> nextProperties, ISet<int> nextFloatProperties)
        {
            if (!Save.Income.Any(value => value.Activity?.Enabled == true)) return;
            var next = Clone(Save);
            bool changed = false;
            foreach (var entry in next.Income.Where(value => value.Activity?.Enabled == true))
            {
                var oldRule = definitions.FirstOrDefault(value => value.HangUpId == entry.HangUpId)?.Rule;
                var newRule = nextDefinitions.FirstOrDefault(value => value.HangUpId == entry.HangUpId)?.Rule;
                if (SameActivityRule(oldRule, newRule, nextProperties, nextFloatProperties)) continue;
                OfflineHomeIncomeActivityLedger.Rebase(ToRecord(entry), entry.Activity);
                changed = true;
            }
            if (changed && !Commit(next)) throw new InvalidOperationException("Could not persist the activity production boundary.");
        }

        bool SameActivityRule(HangUpIncomeRule oldRule, HangUpIncomeRule newRule,
            IReadOnlyDictionary<int, long> nextProperties, ISet<int> nextFloatProperties)
        {
            if (oldRule == null || newRule == null || oldRule.EffectPercentPropertyId != newRule.EffectPercentPropertyId
                || oldRule.EffectFixedPropertyId != newRule.EffectFixedPropertyId
                || oldRule.FinallyEffectPercentPropertyId != newRule.FinallyEffectPercentPropertyId
                || oldRule.IsCart != newRule.IsCart || oldRule.HourlyOutput != newRule.HourlyOutput
                || oldRule.LevelNum != newRule.LevelNum || oldRule.MaxStoreHours != newRule.MaxStoreHours) return false;
            foreach (int id in new[] { oldRule.EffectPercentPropertyId, oldRule.EffectFixedPropertyId,
                oldRule.FinallyEffectPercentPropertyId,
                oldRule.IsCart ? (int)Zjcs.Official.HomeIncomeRules.PropType.HangUpCartAllPercent : 0 })
            {
                if (id == 0) continue;
                properties.TryGetValue(id, out long previous);
                long current = 0; nextProperties?.TryGetValue(id, out current);
                if (previous != current || (floatPropertyIds?.Contains(id) ?? false) != (nextFloatProperties?.Contains(id) ?? false))
                    return false;
            }
            return true;
        }
    }
}
