using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public enum InventoryOperationStatus
    {
        Applied,
        Unchanged,
        MissingItem,
        InsufficientItems,
        InvalidArgument
    }

    public sealed class InventoryOperationResult
    {
        public InventoryOperationStatus Status { get; }
        public long ChangedCount { get; }
        public bool IsApplied => Status == InventoryOperationStatus.Applied;

        internal InventoryOperationResult(InventoryOperationStatus status, long changedCount = 0)
        {
            Status = status;
            ChangedCount = changedCount;
        }
    }

    public static class OfflineInventoryOperations
    {
        private const int MaximumTrackedUnitRecords = 200000;

        public static InventoryOperationResult Grant(OfflinePlayerProgress player, int itemId, long count,
            long acquiredAtUnixSeconds, long expiresAtUnixSeconds = 0, bool trackAcquisition = false)
        {
            if (player == null || itemId <= 0 || count <= 0 || acquiredAtUnixSeconds < 0
                || expiresAtUnixSeconds < 0 || expiresAtUnixSeconds > 0 && expiresAtUnixSeconds < acquiredAtUnixSeconds)
                return new InventoryOperationResult(InventoryOperationStatus.InvalidArgument);

            OfflineItemStack stack = player.Inventory == null ? null
                : player.Inventory.FirstOrDefault(value => value != null && value.ItemId == itemId);
            long currentCount = stack == null ? 0 : stack.Count;
            int currentRecords = stack == null || stack.UnitRecords == null ? 0 : stack.UnitRecords.Count;
            if (long.MaxValue - currentCount < count) return new InventoryOperationResult(InventoryOperationStatus.InvalidArgument);
            if (trackAcquisition || expiresAtUnixSeconds > 0)
            {
                if (count > MaximumTrackedUnitRecords - currentRecords)
                    return new InventoryOperationResult(InventoryOperationStatus.InvalidArgument);
            }
            if (stack == null) stack = FindOrCreate(player, itemId);
            else if (stack.UnitRecords == null) stack.UnitRecords = new List<OfflineItemUnitRecord>();
            if (trackAcquisition || expiresAtUnixSeconds > 0)
            {
                for (long index = 0; index < count; index++)
                    stack.UnitRecords.Add(new OfflineItemUnitRecord
                    {
                        AcquiredAtUnixSeconds = acquiredAtUnixSeconds,
                        ExpiresAtUnixSeconds = expiresAtUnixSeconds
                    });
            }
            stack.Count += count;
            return new InventoryOperationResult(InventoryOperationStatus.Applied, count);
        }

        public static InventoryOperationResult Consume(OfflinePlayerProgress player, int itemId, long count,
            long nowUnixSeconds, bool oldestExpiryFirst = false, bool requireUnexpired = false)
        {
            if (player == null || itemId <= 0 || count <= 0 || nowUnixSeconds < 0)
                return new InventoryOperationResult(InventoryOperationStatus.InvalidArgument);

            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value != null && value.ItemId == itemId);
            if (stack == null) return new InventoryOperationResult(InventoryOperationStatus.MissingItem);
            if (stack.Count < count) return new InventoryOperationResult(InventoryOperationStatus.InsufficientItems);
            if (requireUnexpired && count > stack.Count - ExpiredCount(stack, nowUnixSeconds))
                return new InventoryOperationResult(InventoryOperationStatus.InsufficientItems);

            if (oldestExpiryFirst && stack.UnitRecords.Count > 0)
            {
                var order = stack.UnitRecords.Select((record, index) => new { record, index })
                    .Where(value => !requireUnexpired || value.record.ExpiresAtUnixSeconds == 0
                        || nowUnixSeconds <= value.record.ExpiresAtUnixSeconds)
                    .OrderBy(value => value.record.ExpiresAtUnixSeconds == 0 ? long.MaxValue : value.record.ExpiresAtUnixSeconds)
                    .ThenBy(value => value.record.AcquiredAtUnixSeconds)
                    .ThenBy(value => value.index)
                    .Take((int)Math.Min(count, stack.UnitRecords.Count))
                    .Select(value => value.index).OrderByDescending(index => index).ToArray();
                foreach (int index in order) stack.UnitRecords.RemoveAt(index);
            }
            else if (stack.UnitRecords.Count > 0)
            {
                int remove = (int)Math.Min(count, stack.UnitRecords.Count);
                stack.UnitRecords.RemoveRange(stack.UnitRecords.Count - remove, remove);
            }

            stack.Count -= count;
            if (stack.Count == 0) player.Inventory.Remove(stack);
            return new InventoryOperationResult(InventoryOperationStatus.Applied, count);
        }

        public static long RemoveExpired(OfflinePlayerProgress player, int itemId, long nowUnixSeconds)
        {
            if (player == null || itemId <= 0 || nowUnixSeconds < 0) return 0;
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value != null && value.ItemId == itemId);
            if (stack == null || stack.UnitRecords.Count == 0) return 0;

            int[] expiredIndices = stack.UnitRecords.Select((record, index) => new { record, index })
                .Where(value => value.record.ExpiresAtUnixSeconds > 0 && nowUnixSeconds > value.record.ExpiresAtUnixSeconds)
                .Select(value => value.index).OrderByDescending(index => index).ToArray();
            foreach (int index in expiredIndices) stack.UnitRecords.RemoveAt(index);
            long expired = Math.Min(stack.Count, expiredIndices.LongLength);
            stack.Count -= expired;
            if (stack.Count == 0) player.Inventory.Remove(stack);
            return expired;
        }

        public static long ExpiredCount(OfflineItemStack stack, long nowUnixSeconds)
        {
            if (stack == null || nowUnixSeconds < 0) return 0;
            return stack.UnitRecords.LongCount(record => record.ExpiresAtUnixSeconds > 0
                && nowUnixSeconds > record.ExpiresAtUnixSeconds);
        }

        private static OfflineItemStack FindOrCreate(OfflinePlayerProgress player, int itemId)
        {
            if (player.Inventory == null) player.Inventory = new List<OfflineItemStack>();
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value != null && value.ItemId == itemId);
            if (stack != null)
            {
                if (stack.UnitRecords == null) stack.UnitRecords = new List<OfflineItemUnitRecord>();
                return stack;
            }
            stack = new OfflineItemStack { ItemId = itemId };
            player.Inventory.Add(stack);
            return stack;
        }
    }
}
