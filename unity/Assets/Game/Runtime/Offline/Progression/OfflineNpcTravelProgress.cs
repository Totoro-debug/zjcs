using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineNpcTravelProgress
    {
        public int LocalDayKey;
        public int MapGroupId;
        public int RegionId;
        public long RandomState;
        public int DailyStartedCount;
        public int AutoRefreshedCount;
        public int ManualRefreshedCount;
        public List<OfflineNpcTravelPosition> Positions = new List<OfflineNpcTravelPosition>();
        public List<OfflineNpcTravelPity> Pity = new List<OfflineNpcTravelPity>();
        public List<OfflineNpcTravelDailyCount> NpcDailyCounts = new List<OfflineNpcTravelDailyCount>();
        public List<OfflineNpcTravelTarget> Targets = new List<OfflineNpcTravelTarget>();

        public static void Normalize(OfflineNpcTravelProgress value)
        {
            if (value == null) return;
            if (value.Positions == null) value.Positions = new List<OfflineNpcTravelPosition>();
            if (value.Pity == null) value.Pity = new List<OfflineNpcTravelPity>();
            if (value.NpcDailyCounts == null) value.NpcDailyCounts = new List<OfflineNpcTravelDailyCount>();
            if (value.Targets == null) value.Targets = new List<OfflineNpcTravelTarget>();
            foreach (OfflineNpcTravelTarget target in value.Targets)
            {
                if (target == null) continue;
                if (target.NpcIds == null) target.NpcIds = new List<int>();
                if (target.BaseRewards == null) target.BaseRewards = new List<OfflineNpcTravelReward>();
                if (target.ExtraRewards == null) target.ExtraRewards = new List<OfflineNpcTravelReward>();
            }
        }

        public static bool IsValid(OfflineNpcTravelProgress value)
        {
            if (value == null || value.MapGroupId < 0 || value.RegionId < 0
                || value.RandomState < 0 || value.DailyStartedCount < 0 || value.DailyStartedCount > 100
                || value.AutoRefreshedCount < 0 || value.ManualRefreshedCount < 0
                || value.Positions == null || value.Positions.Count > 4
                || value.Pity == null || value.Pity.Count > 16
                || value.NpcDailyCounts == null || value.NpcDailyCounts.Count > 1000
                || value.Targets == null || value.Targets.Count > 1000) return false;
            if (value.Positions.Exists(item => item == null || item.PositionId < 1 || item.PositionId > 4
                    || item.TravelId < 0 || item.BaseReward < 0 || item.ExtraReward < 0)
                || value.Positions.ConvertAll(item => item.PositionId).Distinct().Count() != value.Positions.Count)
                return false;
            if (value.Pity.Exists(item => item == null || item.PositionId < 1 || item.PositionId > 4
                    || item.Quality < 1 || item.Quality > 5 || item.Count < 0)
                || value.Pity.ConvertAll(item => item.PositionId + ":" + item.Quality).Distinct().Count()
                    != value.Pity.Count) return false;
            if (value.NpcDailyCounts.Exists(item => item == null || item.NpcId <= 0 || item.Count < 0)
                || value.NpcDailyCounts.ConvertAll(item => item.NpcId).Distinct().Count() != value.NpcDailyCounts.Count)
                return false;
            return !value.Targets.Exists(item => item == null || item.TravelId <= 0
                || item.PositionId < 1 || item.PositionId > 4 || item.Quality < 1 || item.Quality > 5
                || item.DurationMinutes < 1 || item.StartedAtUnixSeconds < 0 || item.NpcIds == null
                || item.LocalDayKey < 0
                || item.NpcIds.Count > 5 || item.NpcIds.Exists(id => id <= 0)
                || item.NpcIds.Distinct().Count() != item.NpcIds.Count || item.RewardItemId <= 0
                || item.BaseReward < 0 || item.ExtraReward < 0 || item.ExtraProbability < 0
                || item.MeetConditionCount < 0 || !ValidRewards(item.BaseRewards) || !ValidRewards(item.ExtraRewards));
        }

        private static bool ValidRewards(List<OfflineNpcTravelReward> rewards)
        {
            return rewards != null && rewards.Count <= 100
                && rewards.All(value => value != null && value.ItemId > 0 && value.Count > 0)
                && rewards.Select(value => value.ItemId).Distinct().Count() == rewards.Count;
        }
    }

    [Serializable] public sealed class OfflineNpcTravelPosition
    {
        public int PositionId;
        public int TravelId;
        public bool Used;
        public long BaseReward;
        public long ExtraReward;
        public int RewardItemId;
    }

    [Serializable] public sealed class OfflineNpcTravelPity { public int PositionId; public int Quality; public int Count; }
    [Serializable] public sealed class OfflineNpcTravelDailyCount { public int NpcId; public int Count; }

    [Serializable] public sealed class OfflineNpcTravelTarget
    {
        public int TravelId;
        public int PositionId;
        public int Quality;
        public int DurationMinutes;
        public long StartedAtUnixSeconds;
        public int LocalDayKey;
        public bool Received;
        public int ExtraRewardType;
        public List<int> NpcIds = new List<int>();
        public long BaseReward;
        public long ExtraReward;
        public int RewardItemId;
        public int ExtraProbability;
        public int MeetConditionCount;
        public int RegionId;
        public int MapGroupId;
        public List<OfflineNpcTravelReward> BaseRewards = new List<OfflineNpcTravelReward>();
        public List<OfflineNpcTravelReward> ExtraRewards = new List<OfflineNpcTravelReward>();
    }

    [Serializable]
    public sealed class OfflineNpcTravelReward
    {
        public int ItemId;
        public long Count;
    }
}
