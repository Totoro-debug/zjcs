using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.HomeAutoIncrease;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineItemAutoIncreaseProgress
    {
        public double Decimal;
        public long LastUpdateTimeTicks;
        public double IncreaseSpeed;
        public double BonusSeconds;
        public long MaxCount;
        public long ItemCountLimit;

        public static bool IsValid(OfflineItemAutoIncreaseProgress value) => value == null
            || Finite(value.Decimal) && value.Decimal >= 0 && value.Decimal < 1
            && value.LastUpdateTimeTicks > 0 && value.LastUpdateTimeTicks <= DateTime.MaxValue.Ticks
            && Finite(value.IncreaseSpeed) && Finite(value.BonusSeconds) && value.BonusSeconds >= 0
            && value.MaxCount >= 0 && value.ItemCountLimit >= value.MaxCount;

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    public sealed class OfficialHomeAutoIncreaseConfiguration
    {
        public string manifest;
        public int itemId;
        public double increaseSpeed;
        public long maxCount;
        public bool calcOverflow;
        public int increaseSpeedPercentProp;
        public long itemCountLimit;
        public int[] outputAdditionItems;

        public static OfficialHomeAutoIncreaseConfiguration LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/item-auto-increase");
            var value = asset ? JsonUtility.FromJson<OfficialHomeAutoIncreaseConfiguration>(asset.text) : null;
            if (value == null || value.manifest != "884_156692" || value.itemId != 57
                || value.increaseSpeed != 5 || value.maxCount != 300 || value.itemCountLimit != 1200
                || value.increaseSpeedPercentProp != 1016 || !value.calcOverflow || value.outputAdditionItems == null)
                throw new InvalidDataException("The selected official stamina configuration is unbound.");
            return value;
        }
    }

    // DateTime ticks are the only storage adaptation. Every quantity mutation
    // passes through the selected original PlayerItemDataWrap methods.
    public static class OfflineItemAutoIncrease
    {
        public static void InitializeNewCharacter(OfflinePlayerProgress player, DateTime now)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var config = OfficialHomeAutoIncreaseConfiguration.LoadDefault();
            if (player.Inventory.Any(value => value.ItemId == config.itemId))
                throw new InvalidOperationException("New-character stamina must be initialized exactly once.");
            var stack = new OfflineItemStack
            {
                ItemId = config.itemId,
                AutoIncrease = new OfflineItemAutoIncreaseProgress
                {
                    // ItemEffectAutoIncrease.CreateParam -> OnAddParam records now.
                    IncreaseSpeed = config.increaseSpeed,
                    MaxCount = config.maxCount,
                    ItemCountLimit = config.itemCountLimit,
                    LastUpdateTimeTicks = now.Ticks
                }
            };
            // Confirmed single-player initial grant, not an inferred server rule.
            SetCount(stack, 300, now);
            player.Inventory.Add(stack);
        }

        public static long Count(OfflineItemStack stack, DateTime now) => stack == null ? 0
            : stack.AutoIncrease == null ? stack.Count : Bind(stack, now).Count;

        public static void SetCount(OfflineItemStack stack, long count, DateTime now)
        {
            if (stack.AutoIncrease == null) { stack.Count = count; return; }
            var item = Bind(stack, now);
            item.SetCount(count);
            Store(stack, item);
        }

        // Call inside the owning profile transaction. Reward/effect selection is
        // the caller's boundary; absent original auto-increase records stay absent.
        public static bool SettleFastForward(OfflineItemStack stack, double hours,
            bool hasOutputAddition, int outputAdditionPropValue, DateTime now)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
                throw new ArgumentOutOfRangeException(nameof(hours));
            if (stack?.AutoIncrease == null) return false;
            var item = Bind(stack, now);
            double remainder = 0;
            AutoIncreaseItemHangUpRewardHandler.SettleHangUpReward(item, hours,
                hasOutputAddition, outputAdditionPropValue, value => remainder = value);
            // Original MsgPlayerHangUpDataUpdate consumes the server's pending
            // item-time entry by moving LastUpdateTime back, then clearing it.
            // Both steps belong to this same offline profile commit.
            if (remainder > 0)
                item.Parameter.LastUpdateTime = item.Parameter.LastUpdateTime.AddSeconds(-remainder);
            Store(stack, item);
            return true;
        }

        private static void Store(OfflineItemStack stack, HomeAutoIncreaseItem item)
        {
            stack.Count = item.StoredCount;
            var source = item.Parameter;
            var target = stack.AutoIncrease;
            target.Decimal = source.Decimal;
            target.LastUpdateTimeTicks = source.LastUpdateTime.Ticks;
            target.IncreaseSpeed = source.IncreaseSpeed;
            target.BonusSeconds = source.BonusSeconds;
            target.MaxCount = source.MaxCount;
        }

        private static HomeAutoIncreaseItem Bind(OfflineItemStack stack, DateTime now)
        {
            var value = stack.AutoIncrease;
            return new HomeAutoIncreaseItem(stack.Count, new ItemParamAutoIncrease
            {
                Decimal = value.Decimal,
                LastUpdateTime = new DateTime(value.LastUpdateTimeTicks, DateTimeKind.Utc),
                IncreaseSpeed = value.IncreaseSpeed,
                BonusSeconds = value.BonusSeconds,
                MaxCount = value.MaxCount
            }, value.ItemCountLimit, () => now) { LogException = Debug.LogException };
        }
    }
}
