using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public enum FruitTreeResultCode
    {
        Applied,
        Unchanged,
        InvalidArgument,
        ConditionNotMet,
        ClockRollback,
        NotReady,
        InvalidState
    }

    public sealed class FruitTreeResult
    {
        public FruitTreeResultCode Code { get; }
        public string Message { get; }
        public long CollectedCount { get; }
        public bool IsApplied => Code == FruitTreeResultCode.Applied;

        internal FruitTreeResult(FruitTreeResultCode code, string message = null, long collectedCount = 0)
        {
            Code = code;
            Message = message ?? string.Empty;
            CollectedCount = collectedCount;
        }
    }

    public sealed class OfflineFruitTreeService
    {
        public const int ProductionFurnitureType = 4;
        public const int PetFurnitureType = 11;
        public const int FruitItemId = 56;
        public const int RewardItemId = 115;
        public const long InitialExtraRewardSeconds = 144000;
        public const long BaseCapacity = 15;
        public const double BaseSpeedPerHour = 0.25;

        private readonly OfflineMainlineProgressSession profile;

        public OfflineFruitTreeService(OfflineMainlineProgressSession profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public OfflineFruitTreeProgress State => profile.Save.Player.FruitTree;

        public FruitTreeResult TryRepair(int furnitureType, int playerLevel, bool anyGoddessActive,
            long nowUnixSeconds, string receiptId, bool automatic = false)
        {
            if (nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(receiptId) || receiptId.Length > 160)
                return Result(FruitTreeResultCode.InvalidArgument);
            if (furnitureType != ProductionFurnitureType && furnitureType != PetFurnitureType)
                return Result(FruitTreeResultCode.InvalidArgument);

            profile.Reload();
            OfflineFruitTreeProgress current = State;
            if (current.LastUpdateUnixSeconds > 0 && nowUnixSeconds < current.LastUpdateUnixSeconds)
                return ClockRollback();
            if (IsRepaired(current, furnitureType)) return Result(FruitTreeResultCode.Unchanged);
            bool conditionMet = furnitureType == ProductionFurnitureType
                ? anyGoddessActive && playerLevel >= (automatic ? 38 : 31)
                : playerLevel >= 50;
            if (!conditionMet) return Result(FruitTreeResultCode.ConditionNotMet);

            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineFruitTreeProgress state = save.Player.FruitTree;
                if (furnitureType == ProductionFurnitureType)
                {
                    OfflineItemStack previousFruit = save.Player.Inventory.FirstOrDefault(value => value.ItemId == FruitItemId);
                    if (previousFruit != null) save.Player.Inventory.Remove(previousFruit);
                    state.ProductionRepaired = true;
                    if (!state.InitialRewardGranted)
                    {
                        state.StoredCount = 0;
                        state.FractionalCount = 0;
                        ApplySegment(state, nowUnixSeconds, InitialExtraRewardSeconds / 3600.0);
                        state.InitialRewardGranted = true;
                    }
                }
                else
                {
                    state.PetFacilityRepaired = true;
                }
                state.LastUpdateUnixSeconds = nowUnixSeconds;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public FruitTreeResult TryAutoRepairProduction(int playerLevel, bool anyGoddessActive,
            long nowUnixSeconds, string receiptId)
        {
            if (playerLevel < 38 || !anyGoddessActive) return Result(FruitTreeResultCode.Unchanged);
            return TryRepair(ProductionFurnitureType, playerLevel, anyGoddessActive,
                nowUnixSeconds, receiptId, automatic: true);
        }

        public FruitTreeResult MarkRepairStorySeen(int furnitureType)
        {
            if (furnitureType != ProductionFurnitureType && furnitureType != PetFurnitureType)
                return Result(FruitTreeResultCode.InvalidArgument);
            profile.Reload();
            ProgressionResult transaction = profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineFruitTreeProgress state = player.FruitTree;
                if (furnitureType == ProductionFurnitureType)
                {
                    if (state.ProductionRepairStorySeen) return false;
                    state.ProductionRepairStorySeen = true;
                }
                else
                {
                    if (state.PetRepairStorySeen) return false;
                    state.PetRepairStorySeen = true;
                }
                return true;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public FruitTreeResult MarkCollectionTutorialSeen()
        {
            profile.Reload();
            ProgressionResult transaction = profile.ApplyCharacterMutationIfChanged(player =>
            {
                if (player.FruitTree.CollectionTutorialSeen) return false;
                player.FruitTree.CollectionTutorialSeen = true;
                return true;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public FruitTreeResult AdvanceTo(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) return Result(FruitTreeResultCode.InvalidArgument);
            profile.Reload();
            OfflineFruitTreeProgress current = State;
            if (current.LastUpdateUnixSeconds > 0 && nowUnixSeconds < current.LastUpdateUnixSeconds)
                return ClockRollback();
            if (!current.ProductionRepaired) return Result(FruitTreeResultCode.Unchanged);
            if (current.LastUpdateUnixSeconds <= 0)
            {
                ProgressionResult checkpoint = profile.ApplyCharacterMutationIfChanged(player =>
                {
                    if (player.FruitTree.LastUpdateUnixSeconds > 0) return false;
                    player.FruitTree.LastUpdateUnixSeconds = nowUnixSeconds;
                    return true;
                });
                return Result(Map(checkpoint.Status), checkpoint.Message);
            }
            if (nowUnixSeconds == current.LastUpdateUnixSeconds) return Result(FruitTreeResultCode.Unchanged);

            long from = current.LastUpdateUnixSeconds;
            ProgressionResult transaction = profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineFruitTreeProgress state = player.FruitTree;
                if (!state.ProductionRepaired || state.LastUpdateUnixSeconds != from) return false;
                AdvanceSegments(state, from, nowUnixSeconds);
                state.LastUpdateUnixSeconds = nowUnixSeconds;
                return true;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public FruitTreeResult Collect(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId) || receiptId.Length > 160)
                return Result(FruitTreeResultCode.InvalidArgument);
            profile.Reload();
            if (!State.ProductionRepaired || State.StoredCount <= 0)
                return Result(FruitTreeResultCode.NotReady);
            long gained = State.StoredCount;
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineFruitTreeProgress state = save.Player.FruitTree;
                if (!state.ProductionRepaired || state.StoredCount <= 0)
                    throw new InvalidOperationException("There are no mature fruits to collect.");
                gained = state.StoredCount;
                state.StoredCount = 0;
                state.TotalCollected = checked(state.TotalCollected + gained);
                state.CollectionCount = checked(state.CollectionCount + 1);
                AddItem(save.Player.Inventory, RewardItemId, gained);
            });
            return Result(Map(transaction.Status), transaction.Message, transaction.IsApplied ? gained : 0);
        }

        public FruitTreeResult SetOutputAddition(OfflineFruitTreeOutputAddition addition, long nowUnixSeconds)
        {
            if (addition == null || string.IsNullOrWhiteSpace(addition.Id) || addition.BeginUnixSeconds < 0
                || addition.EndUnixSeconds < 0 || addition.EndUnixSeconds > 0
                && addition.EndUnixSeconds < addition.BeginUnixSeconds || nowUnixSeconds < 0)
                return Result(FruitTreeResultCode.InvalidArgument);
            FruitTreeResult advance = AdvanceTo(nowUnixSeconds);
            if (advance.Code == FruitTreeResultCode.ClockRollback || advance.Code == FruitTreeResultCode.InvalidState)
                return advance;
            profile.Reload();
            ProgressionResult transaction = profile.ApplyCharacterMutationIfChanged(player =>
            {
                List<OfflineFruitTreeOutputAddition> additions = player.FruitTree.OutputAdditions;
                int index = additions.FindIndex(value => value.Id == addition.Id);
                OfflineFruitTreeOutputAddition copy = Clone(addition);
                if (index >= 0 && Equivalent(additions[index], copy)) return false;
                if (index >= 0) additions[index] = copy;
                else additions.Add(copy);
                return true;
            });
            return Result(Map(transaction.Status), transaction.Message);
        }

        public long SecondsUntilNextFruit()
        {
            OfflineFruitTreeProgress state = State;
            if (!state.ProductionRepaired || state.StoredCount >= CapacityAt(state, state.LastUpdateUnixSeconds))
                return long.MaxValue;
            double speed = SpeedPerHourAt(state, state.LastUpdateUnixSeconds);
            if (speed <= 0) return long.MaxValue;
            return Math.Max(0, (long)Math.Ceiling((1 - state.FractionalCount) * 3600.0 / speed));
        }

        private static void AdvanceSegments(OfflineFruitTreeProgress state, long from, long until)
        {
            var boundaries = new SortedSet<long> { from, until };
            foreach (OfflineFruitTreeOutputAddition addition in state.OutputAdditions)
            {
                if (addition.BeginUnixSeconds > from && addition.BeginUnixSeconds < until)
                    boundaries.Add(addition.BeginUnixSeconds);
                if (addition.EndUnixSeconds > from && addition.EndUnixSeconds < until)
                    boundaries.Add(addition.EndUnixSeconds);
            }
            long[] points = boundaries.ToArray();
            for (int index = 0; index + 1 < points.Length; index++)
            {
                long segmentStart = points[index];
                long seconds = points[index + 1] - segmentStart;
                if (seconds <= 0) continue;
                ApplySegment(state, segmentStart, seconds / 3600.0);
            }
        }

        private static void ApplySegment(OfflineFruitTreeProgress state, long unixSeconds, double hours)
        {
            double speed = SpeedPerHourAt(state, unixSeconds);
            long capacity = CapacityAt(state, unixSeconds);
            if (speed > 0.0 && state.StoredCount >= capacity) return;

            double total = speed * hours + state.FractionalCount;
            long whole = total >= long.MaxValue ? long.MaxValue : (long)Math.Floor(total);
            long previousCount = state.StoredCount;
            state.FractionalCount = total >= long.MaxValue ? 0.0 : total - whole;
            long increased = whole > 0 && state.StoredCount > long.MaxValue - whole
                ? long.MaxValue : state.StoredCount + whole;
            state.StoredCount = Math.Max(0L, Math.Min(increased, capacity));
            if (speed > 0.0 && previousCount < capacity && state.StoredCount >= capacity)
                state.FractionalCount = 0.0;
        }

        private static double SpeedPerHourAt(OfflineFruitTreeProgress state, long unixSeconds)
        {
            long addition = state.OutputAdditions.Where(value => IsActive(value, unixSeconds))
                .Sum(value => (long)value.SpeedPercent);
            float multiplier = 1f + (float)addition / 10000f;
            return BaseSpeedPerHour * multiplier;
        }

        private static long CapacityAt(OfflineFruitTreeProgress state, long unixSeconds)
        {
            long addition = state.OutputAdditions.Where(value => IsActive(value, unixSeconds))
                .Sum(value => (long)value.CapacityPercent);
            float capacity = (float)BaseCapacity * (1f + (float)addition / 10000f);
            if (capacity <= 0f) return 0;
            return capacity >= long.MaxValue ? long.MaxValue : (long)capacity;
        }

        private static bool IsActive(OfflineFruitTreeOutputAddition addition, long unixSeconds)
        {
            return addition != null && !addition.Disabled && unixSeconds >= addition.BeginUnixSeconds
                && (addition.EndUnixSeconds == 0 || unixSeconds < addition.EndUnixSeconds);
        }

        private static bool IsRepaired(OfflineFruitTreeProgress state, int furnitureType)
        {
            return furnitureType == ProductionFurnitureType ? state.ProductionRepaired : state.PetFacilityRepaired;
        }

        private static void AddItem(List<OfflineItemStack> inventory, int itemId, long count)
        {
            if (count <= 0) return;
            OfflineItemStack stack = inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (stack == null)
            {
                stack = new OfflineItemStack { ItemId = itemId };
                inventory.Add(stack);
            }
            stack.Count = checked(stack.Count + count);
        }

        private static OfflineFruitTreeOutputAddition Clone(OfflineFruitTreeOutputAddition value)
        {
            return new OfflineFruitTreeOutputAddition
            {
                Id = value.Id,
                BeginUnixSeconds = value.BeginUnixSeconds,
                EndUnixSeconds = value.EndUnixSeconds,
                SpeedPercent = value.SpeedPercent,
                CapacityPercent = value.CapacityPercent,
                Disabled = value.Disabled
            };
        }

        private static bool Equivalent(OfflineFruitTreeOutputAddition left, OfflineFruitTreeOutputAddition right)
        {
            return left.BeginUnixSeconds == right.BeginUnixSeconds && left.EndUnixSeconds == right.EndUnixSeconds
                && left.SpeedPercent == right.SpeedPercent && left.CapacityPercent == right.CapacityPercent
                && left.Disabled == right.Disabled;
        }

        private static FruitTreeResultCode Map(ProgressionResultStatus status)
        {
            switch (status)
            {
                case ProgressionResultStatus.Applied: return FruitTreeResultCode.Applied;
                case ProgressionResultStatus.Unchanged: return FruitTreeResultCode.Unchanged;
                case ProgressionResultStatus.Ready: return FruitTreeResultCode.Applied;
                default: return FruitTreeResultCode.InvalidState;
            }
        }

        private static FruitTreeResult Result(FruitTreeResultCode code, string message = null, long count = 0)
            => new FruitTreeResult(code, message, count);

        private static FruitTreeResult ClockRollback() => Result(FruitTreeResultCode.ClockRollback,
            "检测到系统时间早于果树上次存档时间，请不要修改系统时间。");
    }
}
