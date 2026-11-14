using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Home
{
    [Serializable]
    public sealed class OfflineHomeFastForwardProperty
    {
        public int Id;
        public long Value;
    }

    [Serializable]
    public sealed class OfflineHomeFastForwardAutoIncrease
    {
        public int HangUpId, ItemId, OutputAdditionPropValue;
        public bool HasOutputAddition;
    }

    // A local transaction journal, not an additional source of game rules.
    // Snapshot only the already selected request inputs. After a restart, the
    // profile receipt decides whether its costs/extras still need to be applied.
    [Serializable]
    public sealed class OfflineHomeFastForwardOperation
    {
        public int Version = 1;
        public int FastHangUpId;
        public string Receipt;
        public double Hours;
        public long AcceptedUnixSeconds;
        public double AcceptedSubsecond;
        public bool IsFree, HealToFull;
        public HomeCostEntry[] Costs;
        public OfflineHomeFastForwardAutoIncrease[] AutoIncrease;
        public OfflineHomeIncomeDefinition[] Definitions;
        public OfflineHomeFastForwardProperty[] Properties;
        public int[] FloatPropertyIds;
        public string ProfileReceipt => Receipt + ":profile";
    }

    public sealed partial class OfflineHomeIncomeSession
    {
        public OfflineHomeFastForwardOperation PendingFastForwardOperation
            => string.IsNullOrEmpty(Save.PendingFastForwardJson) ? null
                : JsonUtility.FromJson<OfflineHomeFastForwardOperation>(Save.PendingFastForwardJson);

        public OfflineHomeFastForwardOperation PrepareFastForwardOperation(int fastHangUpId,
            IReadOnlyList<int> hangUpIds, double hours, bool isFree, IReadOnlyList<HomeCostEntry> costs,
            IReadOnlyList<OfflineHomeFastForwardAutoIncrease> autoIncrease, bool healToFull, string receipt)
        {
            if (!string.IsNullOrEmpty(Save.PendingFastForwardJson) || string.IsNullOrWhiteSpace(receipt)
                || receipt.Length > 152
                || Save.AppliedReceipts.Contains(receipt) || Save.AppliedReceipts.Contains(receipt + ":prepared")
                || fastHangUpId <= 0 || hangUpIds == null || !IsFinite(hours) || hours <= 0) return null;
            var selected = hangUpIds.Distinct().Select(id => definitions.FirstOrDefault(value => value.HangUpId == id))
                .Where(value => value != null).ToArray();
            if (!selected.Any(value => value.Unlocked)) return null;
            if (isFree)
            {
                var owners = selected.Where(value => value.FastHangUpId == fastHangUpId).ToArray();
                if (owners.Length == 0 || owners.Any(value => value.FreeFastForwardLimited
                    || value.FreeFastForwardHours != hours)
                    || GetTodayFreeFastForwardCount(fastHangUpId)
                        >= owners.Max(value => value.FreeFastForwardLimit) + ExtraFreeFastForwardCount()) return null;
            }
            var operation = new OfflineHomeFastForwardOperation {
                FastHangUpId = fastHangUpId, Receipt = receipt, Hours = hours, IsFree = isFree,
                AcceptedUnixSeconds = Save.LastLocalUnixSeconds, AcceptedSubsecond = Save.LastIncomeSubsecond,
                HealToFull = healToFull, Costs = costs?.ToArray() ?? Array.Empty<HomeCostEntry>(),
                AutoIncrease = autoIncrease?.ToArray() ?? Array.Empty<OfflineHomeFastForwardAutoIncrease>(),
                Definitions = selected,
                Properties = properties.Select(value => new OfflineHomeFastForwardProperty {
                    Id = value.Key, Value = value.Value }).ToArray(),
                FloatPropertyIds = floatPropertyIds?.ToArray() ?? Array.Empty<int>()
            };
            string json = JsonUtility.ToJson(operation);
            if (!ValidPendingFastForward(json)) return null;
            var next = Clone(Save);
            next.PendingFastForwardJson = json;
            if (isFree)
            {
                int used = GetTodayFreeFastForwardCount(fastHangUpId);
                var usage = next.FastForwardUsage.FirstOrDefault(value => value.FastHangUpId == fastHangUpId);
                if (usage == null) next.FastForwardUsage.Add(usage = new OfflineHomeFastForwardUsage { FastHangUpId = fastHangUpId });
                usage.LocalDate = LocalDate(next.LastLocalUnixSeconds);
                usage.FreeUses = used + 1;
            }
            AddReceipt(next, receipt + ":prepared");
            return Commit(next) ? JsonUtility.FromJson<OfflineHomeFastForwardOperation>(json) : null;
        }

        public OfflineHomeFastForwardResult CompleteFastForwardOperation(string receipt,
            Func<string, bool> isProfileReceiptApplied)
        {
            if (isProfileReceiptApplied == null) throw new ArgumentNullException(nameof(isProfileReceiptApplied));
            var operation = PendingFastForwardOperation;
            if (operation == null || operation.Receipt != receipt || Save.AppliedReceipts.Contains(receipt)
                || !isProfileReceiptApplied(operation.ProfileReceipt)) return null;
            var next = Clone(Save);
            var outputProperties = operation.Properties.ToDictionary(value => value.Id, value => value.Value);
            var flags = new HashSet<int>(operation.FloatPropertyIds);
            var rewards = FastForwardRewards(next, operation.Definitions, operation.Hours, receipt,
                outputProperties, flags, out long total);
            if (rewards == null) return null;
            next.PendingFastForwardJson = null;
            AddReceipt(next, receipt);
            return Commit(next) ? new OfflineHomeFastForwardResult(total, rewards) : null;
        }

        public bool CancelUnpaidFastForwardOperation(string receipt, Func<string, bool> isProfileReceiptApplied)
        {
            if (isProfileReceiptApplied == null) throw new ArgumentNullException(nameof(isProfileReceiptApplied));
            var operation = PendingFastForwardOperation;
            if (operation == null || operation.Receipt != receipt || operation.IsFree
                || isProfileReceiptApplied(operation.ProfileReceipt)) return false;
            var next = Clone(Save);
            next.PendingFastForwardJson = null;
            AddReceipt(next, receipt + ":cancelled");
            return Commit(next);
        }

        static bool ValidPendingFastForward(string json)
        {
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                var operation = JsonUtility.FromJson<OfflineHomeFastForwardOperation>(json);
                if (operation == null || operation.Version != 1 || operation.FastHangUpId <= 0
                    || string.IsNullOrWhiteSpace(operation.Receipt) || operation.Receipt.Length > 152
                    || !IsFinite(operation.Hours) || operation.Hours <= 0
                    || operation.AcceptedUnixSeconds < 0 || !IsFinite(operation.AcceptedSubsecond)
                    || operation.AcceptedSubsecond < 0 || operation.AcceptedSubsecond >= 1
                    || operation.Costs == null || operation.AutoIncrease == null || operation.Definitions == null
                    || operation.Properties == null || operation.FloatPropertyIds == null
                    || operation.IsFree != (operation.Costs.Length == 0)) return false;
                ValidateCatalog(operation.Definitions);
                var costs = new HashSet<int>();
                foreach (var cost in operation.Costs)
                    if (cost == null || cost.itemId <= 0 || cost.count <= 0 || !costs.Add(cost.itemId)) return false;
                var extras = new HashSet<int>();
                foreach (var extra in operation.AutoIncrease)
                    if (extra == null || extra.HangUpId <= 0 || extra.ItemId <= 0 || !extras.Add(extra.ItemId)) return false;
                var props = new HashSet<int>();
                foreach (var property in operation.Properties)
                    if (property == null || !props.Add(property.Id)) return false;
                return operation.FloatPropertyIds.All(value => value > 0)
                    && operation.FloatPropertyIds.Distinct().Count() == operation.FloatPropertyIds.Length;
            }
            catch { return false; }
        }
    }
}
